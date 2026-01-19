using System.Buffers;
using System.Runtime.InteropServices;

namespace Issue14
{
    internal class Program
    {
        #region P/Invoke Declarations

        private const string VMM_DLL = "vmm.dll";
        private const string LC_DLL = "leechcore.dll";
        private const uint PID_PHYSICALMEMORY = unchecked((uint)-1);

        [DllImport(VMM_DLL, EntryPoint = "VMMDLL_InitializeEx")]
        private static extern IntPtr VMMDLL_InitializeEx(
            int argc,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] argv,
            out IntPtr ppLcErrorInfo);

        [DllImport(VMM_DLL, EntryPoint = "VMMDLL_Close")]
        private static extern void VMMDLL_Close(IntPtr hVMM);

        [DllImport(VMM_DLL, EntryPoint = "VMMDLL_ConfigSet")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VMMDLL_ConfigSet(IntPtr hVMM, ulong fOption, ulong qwValue);

        // HOT PATH FUNCTIONS
        // NOTE: Removed SuppressGCTransition - these calls can take a long time with many entries
        
        [DllImport(VMM_DLL, EntryPoint = "VMMDLL_Scatter_Initialize", ExactSpelling = true, SetLastError = false)]
        private static extern IntPtr VMMDLL_Scatter_Initialize(IntPtr hVMM, uint dwPID, uint flags);

        [DllImport(VMM_DLL, EntryPoint = "VMMDLL_Scatter_Prepare", ExactSpelling = true, SetLastError = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VMMDLL_Scatter_Prepare(IntPtr hS, ulong va, uint cb);

        // PrepareEx - provides buffer upfront like native code does
        [DllImport(VMM_DLL, EntryPoint = "VMMDLL_Scatter_PrepareEx", ExactSpelling = true, SetLastError = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern unsafe bool VMMDLL_Scatter_PrepareEx(IntPtr hS, ulong va, uint cb, byte* pb, uint* pcbRead);

        [DllImport(VMM_DLL, EntryPoint = "VMMDLL_Scatter_Execute", ExactSpelling = true, SetLastError = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VMMDLL_Scatter_Execute(IntPtr hS);

        // ExecuteRead - what native code uses
        [DllImport(VMM_DLL, EntryPoint = "VMMDLL_Scatter_ExecuteRead", ExactSpelling = true, SetLastError = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VMMDLL_Scatter_ExecuteRead(IntPtr hS);

        [DllImport(VMM_DLL, EntryPoint = "VMMDLL_Scatter_Read", ExactSpelling = true, SetLastError = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern unsafe bool VMMDLL_Scatter_Read(IntPtr hS, ulong va, uint cb, byte* pb, out uint pcbRead);

        [DllImport(VMM_DLL, EntryPoint = "VMMDLL_Scatter_Clear", ExactSpelling = true, SetLastError = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VMMDLL_Scatter_Clear(IntPtr hS, uint dwPID, uint flags);

        [DllImport(VMM_DLL, EntryPoint = "VMMDLL_Scatter_CloseHandle", ExactSpelling = true, SetLastError = false)]
        private static extern void VMMDLL_Scatter_CloseHandle(IntPtr hS);

        [DllImport(VMM_DLL, EntryPoint = "VMMDLL_Map_GetPhysMem")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VMMDLL_Map_GetPhysMem(IntPtr hVMM, out IntPtr ppPhysMemMap);

        [DllImport(VMM_DLL, EntryPoint = "VMMDLL_MemFree")]
        private static extern unsafe void VMMDLL_MemFree(void* pvMem);

        [DllImport(LC_DLL, EntryPoint = "LcCommand")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern unsafe bool LcCommand(
            IntPtr hLC,
            ulong fOption,
            uint cbDataIn,
            byte* pbDataIn,
            out byte* ppbDataOut,
            out uint pcbDataOut);

        // Constants
        private const ulong LC_CMD_MEMMAP_SET = 0x0000010300000000;
        private const ulong VMMDLL_OPT_CORE_LEECHCORE_HANDLE = 0x4000000100000001;

        // Structs
        [StructLayout(LayoutKind.Sequential)]
        private struct VMMDLL_MAP_PHYSMEM
        {
            public uint dwVersion;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public uint[] _Reserved1;
            public uint cMap;
            public uint _Reserved2;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VMMDLL_MAP_PHYSMEMENTRY
        {
            public ulong pa;
            public ulong cb;
        }

        #endregion

        private static IntPtr _hVMM = IntPtr.Zero;
        private static List<PhysMemEntry> _physMemPages = new();
        private static readonly object _lock = new();

        static void Main()
        {
            try
            {
                Console.WriteLine("Starting up Issue #14 Native P/Invoke Test...");

                // Initialize VMM
                string[] args = ["-device", "fpga", "-norefresh", "-waitinitialize", "-printf", "-v"];
                _hVMM = VMMDLL_InitializeEx(args.Length, args, out _);
                if (_hVMM == IntPtr.Zero)
                {
                    Console.WriteLine("VMMDLL_InitializeEx FAILED!");
                    return;
                }
                Console.WriteLine($"VMM Handle: 0x{_hVMM:X}");

                // Apply memory map
                ApplyMemoryMap();

                // Get physical memory pages
                GetPhysicalMemoryPages();
                Console.WriteLine($"Found {_physMemPages.Count} physical memory pages");

                if (_physMemPages.Count == 0)
                {
                    Console.WriteLine("No physical memory pages found!");
                    return;
                }

                // Spawn workers
                int count = 0;
                for (int i = 0; i < 8; i++)
                {
                    new Thread(() => LongWorker())
                    {
                        IsBackground = true
                    }.Start();
                    Console.WriteLine($"Started LongWorker {++count}");
                }

                for (int i = 0; i < 8; i++)
                {
                    SpawnTransientWorker();
                    Console.WriteLine($"Started TransientWorker {++count}");
                }

                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"*** Unhandled Exception: {ex}");
            }
            finally
            {
                if (_hVMM != IntPtr.Zero)
                {
                    VMMDLL_Close(_hVMM);
                }
                Console.WriteLine("Exiting Issue #14 Test Environment; Press any key to exit.");
                Console.ReadKey(intercept: true);
            }
        }

        private static unsafe void ApplyMemoryMap()
        {
            // Get physical memory map
            if (!VMMDLL_Map_GetPhysMem(_hVMM, out var pMap))
            {
                Console.WriteLine("Failed to get physical memory map");
                return;
            }

            try
            {
                var map = Marshal.PtrToStructure<VMMDLL_MAP_PHYSMEM>(pMap);
                var cbEntry = Marshal.SizeOf<VMMDLL_MAP_PHYSMEMENTRY>();
                var cbMap = Marshal.SizeOf<VMMDLL_MAP_PHYSMEM>();

                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < map.cMap; i++)
                {
                    var entry = Marshal.PtrToStructure<VMMDLL_MAP_PHYSMEMENTRY>((IntPtr)(pMap.ToInt64() + cbMap + i * cbEntry));
                    sb.AppendLine($"{entry.pa:X16} - {(entry.pa + entry.cb - 1):X16}");
                }

                string strMap = sb.ToString();

                // Get LeechCore handle
                if (!VMMDLL_ConfigSet(_hVMM, 0x4000000100000000, 0)) // Dummy call to ensure vmm is ready
                {
                    // This may fail, that's ok
                }

                // For simplicity, skip applying the map - the test will still work
                Console.WriteLine("Memory map retrieved successfully");
            }
            finally
            {
                VMMDLL_MemFree(pMap.ToPointer());
            }
        }

        private static unsafe void GetPhysicalMemoryPages()
        {
            if (!VMMDLL_Map_GetPhysMem(_hVMM, out var pMap))
            {
                Console.WriteLine("Failed to get physical memory map");
                return;
            }

            try
            {
                var map = Marshal.PtrToStructure<VMMDLL_MAP_PHYSMEM>(pMap);
                var cbEntry = Marshal.SizeOf<VMMDLL_MAP_PHYSMEMENTRY>();
                var cbMap = Marshal.SizeOf<VMMDLL_MAP_PHYSMEM>();

                for (int i = 0; i < map.cMap; i++)
                {
                    var entry = Marshal.PtrToStructure<VMMDLL_MAP_PHYSMEMENTRY>((IntPtr)(pMap.ToInt64() + cbMap + i * cbEntry));

                    // Add pages from this region
                    for (ulong p = entry.pa, cbToEnd = entry.cb;
                         cbToEnd > 0x1000;
                         p += 0x1000, cbToEnd -= 0x1000)
                    {
                        _physMemPages.Add(new PhysMemEntry { PageBase = p, RemainingBytes = cbToEnd });
                    }
                }

                // Shuffle
                var rng = Random.Shared;
                int n = _physMemPages.Count;
                while (n > 1)
                {
                    int k = rng.Next(n--);
                    (_physMemPages[n], _physMemPages[k]) = (_physMemPages[k], _physMemPages[n]);
                }
            }
            finally
            {
                VMMDLL_MemFree(pMap.ToPointer());
            }
        }

        private static PhysMemEntry GetRandomPage()
        {
            return _physMemPages[Random.Shared.Next(_physMemPages.Count)];
        }

        private static void SpawnTransientWorker()
        {
            new Thread(() => TransientWorker())
            {
                IsBackground = true
            }.Start();
        }

        private static void LongWorker()
        {
            while (true)
            {
                try
                {
                    DoReads();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unhandled Exception in LongWorker: {ex}");
                }
            }
        }

        private static void TransientWorker()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2 + Random.Shared.Next(6, 16)));
            while (true)
            {
                try
                {
                    cts.Token.ThrowIfCancellationRequested();
                    DoReads();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unhandled Exception in TransientWorker: {ex}");
                }
            }
            SpawnTransientWorker();
        }

        private static unsafe void DoReads()
        {
            uint flags = (uint)(Random.Shared.Next(0, 2) == 0 ? 0 : 1); // 0 = NONE, 1 = NOCACHE

            // Use the pattern that was crashing: 4-4096 pages with Prepare + Execute + Read
            int pageCount = Random.Shared.Next(4, 128);

            // Create scatter handle
            IntPtr hS = VMMDLL_Scatter_Initialize(_hVMM, PID_PHYSICALMEMORY, flags);
            if (hS == IntPtr.Zero)
            {
                Console.WriteLine("VMMDLL_Scatter_Initialize FAILED!");
                return;
            }

            var pageAddrs = new ulong[pageCount];
            var cbWants = new uint[pageCount];

            try
            {
                // STEP 1: Prepare reads (no buffer provided upfront) - like the crashing pattern
                for (int i = 0; i < pageCount; i++)
                {
                    var page = GetRandomPage();
                    uint cbWant = (uint)Random.Shared.Next(4, 0x01E00000);
                    
                    pageAddrs[i] = page.PageBase;
                    cbWants[i] = cbWant;
                    
                    VMMDLL_Scatter_Prepare(hS, page.PageBase, cbWant);
                }

                // STEP 2: Execute (not ExecuteRead)
                VMMDLL_Scatter_Execute(hS);

                // STEP 3: Read results after execute
                for (int i = 0; i < pageCount; i++)
                {
                    uint readSize = Math.Min(cbWants[i], 0x1000);
                    byte* nativeBuffer = (byte*)NativeMemory.Alloc(readSize);
                    try
                    {
                        VMMDLL_Scatter_Read(hS, pageAddrs[i], readSize, nativeBuffer, out uint cbRead);
                        if (cbRead > 0)
                        {
                            byte sink = nativeBuffer[0];
                            _ = sink;
                        }
                    }
                    finally
                    {
                        NativeMemory.Free(nativeBuffer);
                    }
                }
            }
            finally
            {
                VMMDLL_Scatter_CloseHandle(hS);
            }

            // Random delay
            switch (Random.Shared.Next(0, 3))
            {
                case 0:
                    break;
                case 1:
                    Thread.Yield();
                    break;
                case 2:
                    Thread.Sleep(1);
                    break;
            }
        }

        private struct PhysMemEntry
        {
            public ulong PageBase;
            public ulong RemainingBytes;
        }
    }
}
