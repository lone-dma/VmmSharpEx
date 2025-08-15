using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VmmSharpEx.Internal;

namespace VmmSharpEx
{
    /// <summary>
    /// LeechCore public API
    /// </summary>
    public sealed class LeechCore : IDisposable
    {
        public static implicit operator IntPtr(LeechCore x) => x?._h ?? IntPtr.Zero;

        private readonly Vmm _parent;
        private IntPtr _h;

        #region Constants/Types
        //---------------------------------------------------------------------
        // LEECHCORE: PUBLIC API CONSTANTS BELOW:
        //---------------------------------------------------------------------
        public const uint LC_CONFIG_VERSION                     = 0xc0fd0002;
        public const uint LC_CONFIG_ERRORINFO_VERSION           = 0xc0fe0002;

        public const uint LC_CONFIG_PRINTF_ENABLED              = 0x01;
        public const uint LC_CONFIG_PRINTF_V                    = 0x02;
        public const uint LC_CONFIG_PRINTF_VV                   = 0x04;
        public const uint LC_CONFIG_PRINTF_VVV                  = 0x08;

        public const ulong LC_OPT_CORE_PRINTF_ENABLE            = 0x4000000100000000;  // RW
        public const ulong LC_OPT_CORE_VERBOSE                  = 0x4000000200000000;  // RW
        public const ulong LC_OPT_CORE_VERBOSE_EXTRA            = 0x4000000300000000;  // RW
        public const ulong LC_OPT_CORE_VERBOSE_EXTRA_TLP        = 0x4000000400000000;  // RW
        public const ulong LC_OPT_CORE_VERSION_MAJOR            = 0x4000000500000000;  // R
        public const ulong LC_OPT_CORE_VERSION_MINOR            = 0x4000000600000000;  // R
        public const ulong LC_OPT_CORE_VERSION_REVISION         = 0x4000000700000000;  // R
        public const ulong LC_OPT_CORE_ADDR_MAX                 = 0x1000000800000000;  // R
        public const ulong LC_OPT_CORE_STATISTICS_CALL_COUNT    = 0x4000000900000000;  // R [lo-dword: LC_STATISTICS_ID_*]
        public const ulong LC_OPT_CORE_STATISTICS_CALL_TIME     = 0x4000000a00000000;  // R [lo-dword: LC_STATISTICS_ID_*]
        public const ulong LC_OPT_CORE_VOLATILE                 = 0x1000000b00000000;  // R
        public const ulong LC_OPT_CORE_READONLY                 = 0x1000000c00000000;  // R

        public const ulong LC_OPT_MEMORYINFO_VALID              = 0x0200000100000000;  // R
        public const ulong LC_OPT_MEMORYINFO_FLAG_32BIT         = 0x0200000300000000;  // R
        public const ulong LC_OPT_MEMORYINFO_FLAG_PAE           = 0x0200000400000000;  // R
        public const ulong LC_OPT_MEMORYINFO_ARCH               = 0x0200001200000000;  // R - LC_ARCH_TP
        public const ulong LC_OPT_MEMORYINFO_OS_VERSION_MINOR   = 0x0200000500000000;  // R
        public const ulong LC_OPT_MEMORYINFO_OS_VERSION_MAJOR   = 0x0200000600000000;  // R
        public const ulong LC_OPT_MEMORYINFO_OS_DTB             = 0x0200000700000000;  // R
        public const ulong LC_OPT_MEMORYINFO_OS_PFN             = 0x0200000800000000;  // R
        public const ulong LC_OPT_MEMORYINFO_OS_PsLoadedModuleList = 0x0200000900000000;  // R
        public const ulong LC_OPT_MEMORYINFO_OS_PsActiveProcessHead = 0x0200000a00000000;  // R
        public const ulong LC_OPT_MEMORYINFO_OS_MACHINE_IMAGE_TP = 0x0200000b00000000;  // R
        public const ulong LC_OPT_MEMORYINFO_OS_NUM_PROCESSORS  = 0x0200000c00000000;  // R
        public const ulong LC_OPT_MEMORYINFO_OS_SYSTEMTIME      = 0x0200000d00000000;  // R
        public const ulong LC_OPT_MEMORYINFO_OS_UPTIME          = 0x0200000e00000000;  // R
        public const ulong LC_OPT_MEMORYINFO_OS_KERNELBASE      = 0x0200000f00000000;  // R
        public const ulong LC_OPT_MEMORYINFO_OS_KERNELHINT      = 0x0200001000000000;  // R
        public const ulong LC_OPT_MEMORYINFO_OS_KdDebuggerDataBlock = 0x0200001100000000;  // R

        public const ulong LC_OPT_FPGA_PROBE_MAXPAGES           = 0x0300000100000000;  // RW
        public const ulong LC_OPT_FPGA_MAX_SIZE_RX              = 0x0300000300000000;  // RW
        public const ulong LC_OPT_FPGA_MAX_SIZE_TX              = 0x0300000400000000;  // RW
        public const ulong LC_OPT_FPGA_DELAY_PROBE_READ         = 0x0300000500000000;  // RW - uS
        public const ulong LC_OPT_FPGA_DELAY_PROBE_WRITE        = 0x0300000600000000;  // RW - uS
        public const ulong LC_OPT_FPGA_DELAY_WRITE              = 0x0300000700000000;  // RW - uS
        public const ulong LC_OPT_FPGA_DELAY_READ               = 0x0300000800000000;  // RW - uS
        public const ulong LC_OPT_FPGA_RETRY_ON_ERROR           = 0x0300000900000000;  // RW
        public const ulong LC_OPT_FPGA_DEVICE_ID                = 0x0300008000000000;  // RW - bus:dev:fn (ex: 04:00.0 === 0x0400).
        public const ulong LC_OPT_FPGA_FPGA_ID                  = 0x0300008100000000;  // R
        public const ulong LC_OPT_FPGA_VERSION_MAJOR            = 0x0300008200000000;  // R
        public const ulong LC_OPT_FPGA_VERSION_MINOR            = 0x0300008300000000;  // R
        public const ulong LC_OPT_FPGA_ALGO_TINY                = 0x0300008400000000;  // RW - 1/0 use tiny 128-byte/tlp read algorithm.
        public const ulong LC_OPT_FPGA_ALGO_SYNCHRONOUS         = 0x0300008500000000;  // RW - 1/0 use synchronous (old) read algorithm.
        public const ulong LC_OPT_FPGA_CFGSPACE_XILINX          = 0x0300008600000000;  // RW - [lo-dword: register address in bytes] [bytes: 0-3: data, 4-7: byte_enable(if wr/set); top bit = cfg_mgmt_wr_rw1c_as_rw]
        public const ulong LC_OPT_FPGA_TLP_READ_CB_WITHINFO     = 0x0300009000000000;  // RW - 1/0 call TLP read callback with additional string info in szInfo
        public const ulong LC_OPT_FPGA_TLP_READ_CB_FILTERCPL    = 0x0300009100000000;  // RW - 1/0 call TLP read callback with memory read completions from read calls filtered

        public const ulong LC_CMD_FPGA_PCIECFGSPACE             = 0x0000010300000000;  // R
        public const ulong LC_CMD_FPGA_CFGREGPCIE               = 0x0000010400000000;  // RW - [lo-dword: register address]
        public const ulong LC_CMD_FPGA_CFGREGCFG                = 0x0000010500000000;  // RW - [lo-dword: register address]
        public const ulong LC_CMD_FPGA_CFGREGDRP                = 0x0000010600000000;  // RW - [lo-dword: register address]
        public const ulong LC_CMD_FPGA_CFGREGCFG_MARKWR         = 0x0000010700000000;  // W  - write with mask [lo-dword: register address] [bytes: 0-1: data, 2-3: mask]
        public const ulong LC_CMD_FPGA_CFGREGPCIE_MARKWR        = 0x0000010800000000;  // W  - write with mask [lo-dword: register address] [bytes: 0-1: data, 2-3: mask]
        public const ulong LC_CMD_FPGA_CFGREG_DEBUGPRINT        = 0x0000010a00000000;  // N/A
        public const ulong LC_CMD_FPGA_PROBE                    = 0x0000010b00000000;  // RW
        public const ulong LC_CMD_FPGA_CFGSPACE_SHADOW_RD       = 0x0000010c00000000;  // R
        public const ulong LC_CMD_FPGA_CFGSPACE_SHADOW_WR       = 0x0000010d00000000;  // W  - [lo-dword: config space write base address]
        public const ulong LC_CMD_FPGA_TLP_WRITE_SINGLE         = 0x0000011000000000;  // W  - write single tlp BYTE:s
        public const ulong LC_CMD_FPGA_TLP_WRITE_MULTIPLE       = 0x0000011100000000;  // W  - write multiple LC_TLP:s
        public const ulong LC_CMD_FPGA_TLP_TOSTRING             = 0x0000011200000000;  // RW - convert single TLP to LPSTR; *pcbDataOut includes NULL terminator.

        public const ulong LC_CMD_FPGA_TLP_CONTEXT              = 0x2000011400000000;  // W - set/unset TLP user-defined context to be passed to callback function. (pbDataIn == LPVOID user context). [not remote].
        public const ulong LC_CMD_FPGA_TLP_CONTEXT_RD           = 0x2000011b00000000;  // R - get TLP user-defined context to be passed to callback function. [not remote].
        public const ulong LC_CMD_FPGA_TLP_FUNCTION_CALLBACK    = 0x2000011500000000;  // W - set/unset TLP callback function (pbDataIn == PLC_TLP_CALLBACK). [not remote].
        public const ulong LC_CMD_FPGA_TLP_FUNCTION_CALLBACK_RD = 0x2000011c00000000;  // R - get TLP callback function. [not remote].
        public const ulong LC_CMD_FPGA_BAR_CONTEXT              = 0x2000011800000000;  // W - set/unset BAR user-defined context to be passed to callback function. (pbDataIn == LPVOID user context). [not remote].
        public const ulong LC_CMD_FPGA_BAR_CONTEXT_RD           = 0x2000011d00000000;  // R - get BAR user-defined context to be passed to callback function. [not remote].
        public const ulong LC_CMD_FPGA_BAR_FUNCTION_CALLBACK    = 0x2000011900000000;  // W - set/unset BAR callback function (pbDataIn == PLC_BAR_CALLBACK). [not remote].
        public const ulong LC_CMD_FPGA_BAR_FUNCTION_CALLBACK_RD = 0x2000011e00000000;  // R - get BAR callback function. [not remote].
        public const ulong LC_CMD_FPGA_BAR_INFO                 = 0x0000011a00000000;  // R - get BAR info (pbDataOut == LC_BAR_INFO[6]).

        public const ulong LC_CMD_FILE_DUMPHEADER_GET           = 0x0000020100000000;  // R

        public const ulong LC_CMD_STATISTICS_GET                = 0x4000010000000000;  // R
        public const ulong LC_CMD_MEMMAP_GET                    = 0x4000020000000000;  // R  - MEMMAP as LPSTR
        public const ulong LC_CMD_MEMMAP_SET                    = 0x4000030000000000;  // W  - MEMMAP as LPSTR
        public const ulong LC_CMD_MEMMAP_GET_STRUCT             = 0x4000040000000000;  // R  - MEMMAP as LC_MEMMAP_ENTRY[]
        public const ulong LC_CMD_MEMMAP_SET_STRUCT             = 0x4000050000000000;  // W  - MEMMAP as LC_MEMMAP_ENTRY[]

        public const ulong LC_CMD_AGENT_EXEC_PYTHON             = 0x8000000100000000;  // RW - [lo-dword: optional timeout in ms]
        public const ulong LC_CMD_AGENT_EXIT_PROCESS            = 0x8000000200000000;  //    - [lo-dword: process exit code]
        public const ulong LC_CMD_AGENT_VFS_LIST                = 0x8000000300000000;  // RW
        public const ulong LC_CMD_AGENT_VFS_READ                = 0x8000000400000000;  // RW
        public const ulong LC_CMD_AGENT_VFS_WRITE               = 0x8000000500000000;  // RW
        public const ulong LC_CMD_AGENT_VFS_OPT_GET             = 0x8000000600000000;  // RW
        public const ulong LC_CMD_AGENT_VFS_OPT_SET             = 0x8000000700000000;  // RW
        public const ulong LC_CMD_AGENT_VFS_INITIALIZE          = 0x8000000800000000;  // RW
        public const ulong LC_CMD_AGENT_VFS_CONSOLE             = 0x8000000900000000;  // RW



        //---------------------------------------------------------------------
        // LEECHCORE: CORE FUNCTIONALITY BELOW:
        //---------------------------------------------------------------------

        public struct MemScatter
        {
            public bool f;
            public ulong qwA;
            public byte[] pb;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct LCConfig
        {
            public uint dwVersion;
            public uint dwPrintfVerbosity;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDevice;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szRemote;
            public IntPtr pfn_printf_opt;
            public ulong paMax;
            public bool fVolatile;
            public bool fWritable;
            public bool fRemote;
            public bool fRemoteDisableCompress;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDeviceName;
        }

        public struct LCConfigErrorInfo
        {
            public bool fValid;
            public bool fUserInputRequest;
            public string strUserText;
        }
        #endregion

        /// <summary>
        /// ToString() override.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return _h == IntPtr.Zero ? "LeechCore:NotValid" : "LeechCore";
        }

        /// <summary>
        /// Factory method creating a new LeechCore object taking a LC_CONFIG structure
        /// containing the configuration and optionally return a LC_CONFIG_ERRORINFO
        /// structure containing any error.
        /// Use this when you wish to gain greater control of creating LeechCore objects.
        /// </summary>
        /// <param name="pLcCreateConfig"></param>
        /// <param name="configErrorInfo"></param>
        /// <returns>Initialized LeechCore Instance.</returns>
        public static unsafe LeechCore Create(ref LCConfig pLcCreateConfig, out LCConfigErrorInfo configErrorInfo)
        {
            int cbERROR_INFO = Marshal.SizeOf<Lci.LC_CONFIG_ERRORINFO>();
            IntPtr hLC = Lci.LcCreateEx(ref pLcCreateConfig, out var pLcErrorInfo);
            configErrorInfo = new LCConfigErrorInfo
            {
                strUserText = ""
            };
            if ((pLcErrorInfo != IntPtr.Zero) && (hLC != IntPtr.Zero))
            {
                return new LeechCore(hLC);
            }
            if (hLC != IntPtr.Zero)
            {
                Lci.LcClose(hLC);
            }
            if (pLcErrorInfo != IntPtr.Zero)
            {
                Lci.LC_CONFIG_ERRORINFO e = Marshal.PtrToStructure<Lci.LC_CONFIG_ERRORINFO>(pLcErrorInfo);
                if (e.dwVersion == LeechCore.LC_CONFIG_ERRORINFO_VERSION)
                {
                    configErrorInfo.fValid = true;
                    configErrorInfo.fUserInputRequest = e.fUserInputRequest;
                    if (e.cwszUserText > 0)
                    {
                        configErrorInfo.strUserText = Marshal.PtrToStringUni((System.IntPtr)(pLcErrorInfo.ToInt64() + cbERROR_INFO));
                    }
                }
                Lci.LcMemFree(pLcErrorInfo);
            }
            return null;
        }

        private LeechCore()
        {
        }

        private LeechCore(IntPtr hLC)
        {
            _h = hLC;
        }

        /// <summary>
        /// Create a new inherited LeechCore instance from a given Vmm instance.
        /// </summary>
        /// <param name="vmm"></param>
        /// <exception cref="VmmException"></exception>
        internal LeechCore(Vmm vmm)
        {
            if (vmm.GetConfig(Vmm.CONFIG_OPT_CORE_LEECHCORE_HANDLE) is not ulong pqwValue)
            {
                throw new VmmException("LeechCore: failed retrieving handle from Vmm.");
            }
            string strDevice = string.Format("existing://0x{0:X}", pqwValue);
            var cfg = new LCConfig
            {
                dwVersion = LeechCore.LC_CONFIG_VERSION,
                szDevice = strDevice
            };
            IntPtr hLC = Lci.LcCreate(ref cfg);
            if (hLC == IntPtr.Zero)
            {
                throw new VmmException("LeechCore: failed to create object.");
            }
            _h = hLC;
            _parent = vmm;
        }

        ~LeechCore() => Dispose(disposing: false);

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _h, IntPtr.Zero) is IntPtr h && h != IntPtr.Zero)
            {
                if (_parent is null)
                {
                    Lci.LcClose(h);
                }
            }
        }

        //---------------------------------------------------------------------
        // LEECHCORE: GENERAL FUNCTIONALITY BELOW:
        //---------------------------------------------------------------------

        /// <summary>
        /// Read a single physical memory range.
        /// </summary>
        /// <param name="pa">Physical address to read.</param>
        /// <param name="cb">Number of bytes to read.</param>
        /// <returns>Bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte[] Read(ulong pa, uint cb) =>
            ReadArray<byte>(pa, cb);

        /// <summary>
        /// Read physcial memory into a nullable struct value <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Value Type.</typeparam>
        /// <param name="pa">Physical address to read.</param>
        /// <param name="result">Result of the memory read.</param>
        /// <returns>TRUE if successful, otherwise FALSE.</returns>
        public unsafe bool ReadValue<T>(ulong pa, out T result)
            where T : unmanaged, allows ref struct
        {
            uint cb = (uint)sizeof(T);
            result = default;
            fixed (void* pb = &result)
            {
                return Lci.LcRead(_h, pa, cb, (byte*)pb);
            }
        }

        /// <summary>
        /// Read physical memory into an array of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Value Type.</typeparam>
        /// <param name="pa">Physical address to read.</param>
        /// <param name="count">Number of elements to read.</param>
        /// <returns>Managed Array of type <typeparamref name="T"/>. Null if read failed.</returns>
        public unsafe T[] ReadArray<T>(ulong pa, uint count)
            where T : unmanaged
        {
            uint cb = count * (uint)sizeof(T);
            T[] data = new T[count];
            fixed (T* pb = data)
            {
                bool result = Lci.LcRead(_h, pa, cb, (byte*)pb);
                return result ? data : null;
            }
        }

        /// <summary>
        /// Read memory into a Span of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Value Type</typeparam>
        /// <param name="pa">Memory address to read from.</param>
        /// <param name="span">Span to receive the memory read.</param>
        /// <returns>True if successful, otherwise False.</returns>
        public unsafe bool ReadSpan<T>(ulong pa, Span<T> span)
            where T : unmanaged
        {
            uint cb = (uint)(sizeof(T) * span.Length);
            fixed (T* pb = span)
            {
                return Lci.LcRead(_h, pa, cb, (byte*)pb);
            }
        }

        /// <summary>
        /// Write memory from a Span of <typeparamref name="T"/> to a specified memory address.
        /// </summary>
        /// <typeparam name="T">Value Type</typeparam>
        /// <param name="pa">Memory address to write to.</param>
        /// <param name="span">Span to write from.</param>
        /// <returns>True if successful, otherwise False.</returns>
        public unsafe bool WriteSpan<T>(ulong pa, Span<T> span)
            where T : unmanaged
        {
            _parent?.ThrowIfMemWritesDisabled();
            uint cb = (uint)(sizeof(T) * span.Length);
            fixed (T* pb = span)
            {
                return Lci.LcWrite(_h, pa, cb, (byte*)pb);
            }
        }

        /// <summary>
        /// Read physical memory into unmanaged memory.
        /// </summary>
        /// <param name="pa">Physical address to read.</param>
        /// <param name="pb">Pointer to buffer to read into.</param>
        /// <param name="cb">Counte of bytes to read.</param>
        /// <returns>True if read successful, otherwise False.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Read(ulong pa, IntPtr pb, uint cb) =>
            Read(pa, pb.ToPointer(), cb);

        /// <summary>
        /// Read physical memory into unmanaged memory.
        /// </summary>
        /// <param name="pa">Physical address to read.</param>
        /// <param name="pb">Pointer to buffer to read into.</param>
        /// <param name="cb">Counte of bytes to read.</param>
        /// <returns>True if read successful, otherwise False.</returns>
        public unsafe bool Read(ulong pa, void* pb, uint cb)
        {
            if (!Lci.LcRead(_h, pa, cb, (byte*)pb))
                return false;
            return true;
        }

        /// <summary>
        /// Perform a scatter read of multiple page-sized physical memory ranges.
        /// Does not copy the read memory to a managed byte buffer, but instead allows direct access to the native memory via a Span view.
        /// </summary>
        /// <param name="pas">Array of page-aligned Physical Memory Addresses.</param>
        /// <returns>SCATTER_HANDLE</returns>
        /// <exception cref="VmmException"></exception>
        public unsafe SCATTER_HANDLE ReadScatter(params ulong[] pas)
        {
            if (!Lci.LcAllocScatter1((uint)pas.Length, out IntPtr pppMEMs))
                throw new VmmException("LcAllocScatter1 FAIL");
            var ppMEMs = (Lci.LC_MEM_SCATTER**)pppMEMs.ToPointer();
            for (int i = 0; i < pas.Length; i++)
            {
                var pMEM = ppMEMs[i];
                pMEM->qwA = pas[i] & ~(ulong)0xfff;
            }
            var results = new Dictionary<ulong, SCATTER_PAGE>(pas.Length);
            Lci.LcReadScatter(_h, (uint)pas.Length, pppMEMs);
            for (int i = 0; i < pas.Length; i++)
            {
                var pMEM = ppMEMs[i];
                if (pMEM->f != 0)
                    results[pMEM->qwA] = new SCATTER_PAGE(pMEM->pb);
            }
            return new SCATTER_HANDLE(results, pppMEMs);
        }

        /// <summary>
        /// Wraps native memory from a Scatter Read.
        /// Calls LcMemFree on disposal.
        /// </summary>
        public sealed class SCATTER_HANDLE : IDisposable
        {
            private IntPtr _mems;

            /// <summary>
            /// Scatter Read Results. Only successful reads are contained in this Dictionary. If a read failed, it will not be present.
            /// KEY: Page-aligned Memory Address.
            /// VALUE: SCATTER_PAGE containing the page data.
            /// </summary>
            public IReadOnlyDictionary<ulong, SCATTER_PAGE> Results { get; }

            public SCATTER_HANDLE(Dictionary<ulong, SCATTER_PAGE> results, IntPtr mems)
            {
                Results = results;
                _mems = mems;
            }

            #region IDisposable

            /// <summary>
            /// Calls LcMemFree on native memory resources.
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
                if (Interlocked.Exchange(ref _mems, IntPtr.Zero) is IntPtr h && h != IntPtr.Zero)
                {
                    Lci.LcMemFree(h);
                }
            }

            ~SCATTER_HANDLE() => Dispose(disposing: false);
            
            #endregion
        }

        /// <summary>
        /// Defines a Scatter Read Page.
        /// </summary>
        public readonly struct SCATTER_PAGE
        {
            private readonly IntPtr _pb;

            public SCATTER_PAGE(IntPtr pb)
            {
                _pb = pb;
            }

            /// <summary>
            /// Page for this scatter read entry.
            /// Length is always 4096 bytes (one page).
            /// WARNING: Do not access this memory after the SCATTER_HANDLE is disposed!
            /// </summary>
            public readonly unsafe ReadOnlySpan<byte> Page =>
                new(_pb.ToPointer(), 0x1000);
        }

        /// <summary>
        /// Write a single range of physical memory.
        /// </summary>
        /// <param name="pa">Physical address to write</param>
        /// <param name="data">Data to write starting at pa.</param>
        /// <returns>True if write successful, otherwise False. The write is best-effort and may fail. It's recommended to verify the write with a subsequent read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Write(ulong pa, byte[] data) =>
            WriteArray<byte>(pa, data);

        /// <summary>
        /// Write a single struct <typeparamref name="T"/> into physical memory.
        /// </summary>
        /// <typeparam name="T">Value Type.</typeparam>
        /// <param name="pa">Physical address to write</param>
        /// <param name="value"><typeparamref name="T"/> value to write.</param>
        /// <returns>True if write successful, otherwise False. The write is best-effort and may fail. It's recommended to verify the write with a subsequent read.</returns>
        public unsafe bool WriteValue<T>(ulong pa, T value)
            where T : unmanaged, allows ref struct
        {
            _parent?.ThrowIfMemWritesDisabled();
            uint cb = (uint)sizeof(T);
            return Lci.LcWrite(_h, pa, cb, (byte*)&value);
        }

        /// <summary>
        /// Write a managed <typeparamref name="T"/> array into physical memory.
        /// </summary>
        /// <typeparam name="T">Value Type.</typeparam>
        /// <param name="pa">Physical address to write</param>
        /// <param name="data">Managed <typeparamref name="T"/> array to write.</param>
        /// <returns>True if write successful, otherwise False. The write is best-effort and may fail. It's recommended to verify the write with a subsequent read.</returns>
        public unsafe bool WriteArray<T>(ulong pa, T[] data)
            where T : unmanaged
        {
            _parent?.ThrowIfMemWritesDisabled();
            uint cb = (uint)sizeof(T) * (uint)data.Length;
            fixed (T* pb = data)
            {
                return Lci.LcWrite(_h, pa, cb, (byte*)pb);
            }
        }

        /// <summary>
        /// Write from unmanaged memory into physical memory.
        /// </summary>
        /// <param name="pa">Physical address to write</param>
        /// <param name="pb">Pointer to buffer to write from.</param>
        /// <param name="cb">Count of bytes to write.</param>
        /// <returns>True if write successful, otherwise False. The write is best-effort and may fail. It's recommended to verify the write with a subsequent read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Write(ulong pa, IntPtr pb, uint cb) =>
            Write(pa, pb.ToPointer(), cb);

        /// <summary>
        /// Write from unmanaged memory into physical memory.
        /// </summary>
        /// <param name="pa">Physical address to write</param>
        /// <param name="pb">Pointer to buffer to write from.</param>
        /// <param name="cb">Count of bytes to write.</param>
        /// <returns>True if write successful, otherwise False. The write is best-effort and may fail. It's recommended to verify the write with a subsequent read.</returns>
        public unsafe bool Write(ulong pa, void* pb, uint cb)
        {
            _parent?.ThrowIfMemWritesDisabled();
            return Lci.LcWrite(_h, pa, cb, (byte*)pb);
        }

        /// <summary>
        /// Write multiple page-sized physical memory ranges. The write is best-effort and may fail. It's recommended to verify the writes with subsequent reads.
        /// </summary>
        /// <param name="MEMs">MEMs containing the memory addresses and data to write.</param>
        public unsafe void WriteScatter(params MemScatter[] MEMs)
        {
            _parent?.ThrowIfMemWritesDisabled();
            if (!Lci.LcAllocScatter1((uint)MEMs.Length, out var pppMEMs))
                throw new VmmException("LcAllocScatter1 FAIL");
            try
            {
                var ppMEMs = (Lci.LC_MEM_SCATTER**)pppMEMs.ToPointer();
                for (int i = 0; i < MEMs.Length; i++)
                {
                    var entry = MEMs[i];
                    ArgumentNullException.ThrowIfNull(entry.pb, nameof(entry.pb));
                    ArgumentOutOfRangeException.ThrowIfNotEqual(entry.pb.Length, 0x1000, nameof(entry.pb));
                    var pMEM = ppMEMs[i];
                    var pMEM_pb = new Span<byte>(pMEM->pb.ToPointer(), 0x1000);
                    pMEM->f = entry.f ? 1 : 0;
                    pMEM->qwA = entry.qwA & ~(ulong)0xfff;
                    entry.pb.CopyTo(pMEM_pb);
                }
                Lci.LcWriteScatter(_h, (uint)MEMs.Length, pppMEMs);
                for (int i = 0; i < MEMs.Length; i++)
                {
                    var pMEM = ppMEMs[i];
                    MEMs[i].f = pMEM->f != 0;
                    MEMs[i].qwA = pMEM->qwA;
                }
            }
            finally
            {
                Lci.LcMemFree(pppMEMs);
            }
        }

        /// <summary>
        /// Retrieve a LeechCore option value.
        /// </summary>
        /// <param name="fOption">Parameter LeechCore.LC_OPT_*</param>
        /// <returns>The option value retrieved. NULL on fail.</returns>
        public ulong? GetOption(ulong fOption)
        {
            if (!Lci.GetOption(_h, fOption, out var pqwValue))
                return null;
            return pqwValue;
        }

        /// <summary>
        /// Set a LeechCore option value.
        /// </summary>
        /// <param name="fOption">Parameter LeechCore.LC_OPT_*</param>
        /// <param name="qwValue">The option value to set.</param>
        /// <returns></returns>
        public bool SetOption(ulong fOption, ulong qwValue)
        {
            return Lci.SetOption(_h, fOption, qwValue);
        }

        /// <summary>
        /// Send a command to LeechCore.
        /// </summary>
        /// <param name="fOption">Parameter LeechCore.LC_CMD_*</param>
        /// <param name="dataIn">The data to set (or null).</param>
        /// <param name="dataOut">The data retrieved.</param>
        /// <returns></returns>
        public unsafe bool ExecuteCommand(ulong fOption, byte[] dataIn, out byte[] dataOut)
        {
            uint cbDataOut;
            IntPtr pbDataOut;
            dataOut = null;
            if (dataIn is null)
            {
                if (!Lci.LcCommand(_h, fOption, 0, null, out pbDataOut, out cbDataOut))
                    return false;
            }
            else
            {
                fixed (byte* pbDataIn = dataIn)
                {
                    if (!Lci.LcCommand(_h, fOption, (uint)dataIn.Length, pbDataIn, out pbDataOut, out cbDataOut))
                        return false;
                }
            }
            dataOut = new byte[cbDataOut];
            if (cbDataOut > 0)
            {
                var src = new ReadOnlySpan<byte>(pbDataOut.ToPointer(), (int)cbDataOut);
                src.CopyTo(dataOut);
                Lci.LcMemFree(pbDataOut);
            }
            return true;
        }
    }
}
