using VmmSharpEx;
using VmmSharpEx.Options;
using VmmSharpEx.Scatter;

namespace Issue14
{
    internal class Program
    {
        private static Vmm? _vmm;
        private static List<PhysMemEntry> _physMemPages = new();
        private static readonly object _lock = new();

        static void Main()
        {
            try
            {
                Console.WriteLine("Starting up Issue #14 Native P/Invoke Test...");

                // Initialize VMM
                string[] args = ["-device", "fpga", "-norefresh", "-waitinitialize", "-printf", "-v"];
                _vmm = new Vmm(args);
                Console.WriteLine($"VMM Initialized: {_vmm}");

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
                _vmm?.Dispose();
                Console.WriteLine("Exiting Issue #14 Test Environment; Press any key to exit.");
                Console.ReadKey(intercept: true);
            }
        }

        private static void ApplyMemoryMap()
        {
            if (_vmm is null)
                return;

            // For simplicity, do not apply the map. Just verify we can fetch it.
            _ = _vmm.GetMemoryMap(applyMap: false);
            Console.WriteLine("Memory map retrieved successfully");
        }

        private static void GetPhysicalMemoryPages()
        {
            if (_vmm is null)
                return;

            var map = _vmm.Map_GetPhysMem();
            if (map is null)
            {
                Console.WriteLine("Failed to get physical memory map");
                return;
            }

            for (int i = 0; i < map.Length; i++)
            {
                var entry = map[i];
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

        private static void DoReads()
        {
            if (_vmm is null)
                return;

            uint flags = (uint)(Random.Shared.Next(0, 2) == 0 ? 0 : 1); // 0 = NONE, 1 = NOCACHE
            var vmmFlags = flags == 0 ? VmmFlags.NONE : VmmFlags.NOCACHE;

            // Use the pattern that was crashing: 4-4096 pages with Prepare + Execute + Read
            int pageCount = Random.Shared.Next(4, 4096);

            using var scatter = _vmm.CreateScatter(Vmm.PID_PHYSICALMEMORY, vmmFlags);

            var pageAddrs = new ulong[pageCount];
            var cbWants = new uint[pageCount];

            // STEP 1: Prepare reads (no buffer provided upfront)
            for (int i = 0; i < pageCount; i++)
            {
                var page = GetRandomPage();
                uint cbWant = (uint)Random.Shared.Next(4, 0x01E00000);

                pageAddrs[i] = page.PageBase;
                cbWants[i] = cbWant;

                scatter.PrepareRead(page.PageBase, cbWant);
            }

            // STEP 2: Execute
            scatter.Execute();

            // STEP 3: Read results
            for (int i = 0; i < pageCount; i++)
            {
                uint cbWant = Math.Min(cbWants[i], 0x1000);
                Span<byte> tmp = cbWant <= 1024 ? stackalloc byte[(int)cbWant] : new byte[(int)cbWant];
                if (scatter.ReadSpan(pageAddrs[i], tmp))
                {
                    byte sink = tmp[0];
                    _ = sink;
                }
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
