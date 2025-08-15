using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using VmmSharpEx.Internal;
using VmmSharpEx.Refresh;

namespace VmmSharpEx;

/// <summary>
///     MemProcFS public API
/// </summary>
public sealed class Vmm : IDisposable
{
    #region Base Functionality

    public static implicit operator IntPtr(Vmm x)
    {
        return x?._h ?? IntPtr.Zero;
    }

    private IntPtr _h;

    /// <summary>
    ///     Underlying LeechCore handle.
    /// </summary>
    public LeechCore LeechCore { get; }

    private readonly bool _enableMemoryWriting = true;

    /// <summary>
    ///     Set to FALSE if you would like to disable all Memory Writing in this High Level API.
    ///     Attempts to Write Memory will throw a VmmException.
    ///     This setting is immutable after initialization.
    /// </summary>
    public bool EnableMemoryWriting
    {
        get => _enableMemoryWriting;
        init
        {
            _enableMemoryWriting = value;
            if (!_enableMemoryWriting)
                Log("Memory Writing Disabled!");
        }
    }

    /// <summary>
    ///     ToString() override.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return _h == IntPtr.Zero ? "Vmm:NotValid" : "Vmm";
    }

    /// <summary>
    ///     Internal initialization factory method.
    /// </summary>
    private static IntPtr Create(out LeechCore.LCConfigErrorInfo configErrorInfo, params string[] args)
    {
        var cbERROR_INFO = Marshal.SizeOf<Lci.LC_CONFIG_ERRORINFO>();
        var hVMM = Vmmi.VMMDLL_InitializeEx(args.Length, args, out var pLcErrorInfo);
        var vaLcCreateErrorInfo = pLcErrorInfo.ToInt64();
        configErrorInfo = new LeechCore.LCConfigErrorInfo
        {
            strUserText = ""
        };
        if (hVMM.ToInt64() == 0) throw new VmmException("VMM INIT FAILED.");
        if (vaLcCreateErrorInfo == 0) return hVMM;
        var e = Marshal.PtrToStructure<Lci.LC_CONFIG_ERRORINFO>(pLcErrorInfo);
        if (e.dwVersion == LeechCore.LC_CONFIG_ERRORINFO_VERSION)
        {
            configErrorInfo.fValid = true;
            configErrorInfo.fUserInputRequest = e.fUserInputRequest;
            if (e.cwszUserText > 0) configErrorInfo.strUserText = Marshal.PtrToStringUni((IntPtr)(vaLcCreateErrorInfo + cbERROR_INFO));
        }

        return hVMM;
    }

    /// <summary>
    ///     Private zero-argument constructor to prevent instantiation.
    /// </summary>
    private Vmm()
    {
    }

    /// <summary>
    ///     Initialize a new Vmm instance with command line arguments.
    ///     Also retrieve the extended error information (if there is an error).
    /// </summary>
    /// <param name="configErrorInfo">Error information in case of an error.</param>
    /// <param name="args">MemProcFS/Vmm command line arguments.</param>
    public Vmm(out LeechCore.LCConfigErrorInfo configErrorInfo, params string[] args)
    {
        _h = Create(out configErrorInfo, args);
        LeechCore = new LeechCore(this);
        Log("VmmSharpEx Initialized.");
    }

    /// <summary>
    ///     Initialize a new Vmm instance with command line arguments.
    /// </summary>
    /// <param name="args">MemProcFS/Vmm command line arguments.</param>
    public Vmm(params string[] args)
        : this(out _, args)
    {
    }

    /// <summary>
    ///     Manually initialize plugins.
    ///     By default plugins are not initialized during Vmm Init.
    /// </summary>
    public void InitializePlugins()
    {
        Vmmi.VMMDLL_InitializePlugins(_h);
    }

    ~Vmm()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _h, IntPtr.Zero) is IntPtr h && h != IntPtr.Zero)
        {
            if (disposing)
            {
                LeechCore.Dispose();
                RefreshManager.UnregisterAll(this);
            }

            Vmmi.VMMDLL_Close(h);
        }
    }

    /// <summary>
    ///     Close all Vmm instances in the native layer.
    /// </summary>
    public static void CloseAll()
    {
        Vmmi.VMMDLL_CloseAll();
    }

    #endregion

    #region Config Get/Set

    public enum MemoryModelType
    {
        MEMORYMODEL_NA = 0,
        MEMORYMODEL_X86 = 1,
        MEMORYMODEL_X86PAE = 2,
        MEMORYMODEL_X64 = 3,
        MEMORYMODEL_ARM64 = 4
    }

    public enum SystemType
    {
        SYSTEM_UNKNOWN_X64 = 1,
        SYSTEM_WINDOWS_X64 = 2,
        SYSTEM_UNKNOWN_X86 = 3,
        SYSTEM_WINDOWS_X86 = 4
    }

    public const ulong CONFIG_OPT_CORE_PRINTF_ENABLE = 0x4000000100000000; // RW
    public const ulong CONFIG_OPT_CORE_VERBOSE = 0x4000000200000000; // RW
    public const ulong CONFIG_OPT_CORE_VERBOSE_EXTRA = 0x4000000300000000; // RW
    public const ulong CONFIG_OPT_CORE_VERBOSE_EXTRA_TLP = 0x4000000400000000; // RW
    public const ulong CONFIG_OPT_CORE_MAX_NATIVE_ADDRESS = 0x4000000800000000; // R
    public const ulong CONFIG_OPT_CORE_LEECHCORE_HANDLE = 0x4000001000000000; // R - underlying leechcore handle (do not close).
    public const ulong CONFIG_OPT_CORE_VMM_ID = 0x4000002000000000; // R - use with startup option '-create-from-vmmid' to create a thread-safe duplicate VMM instance.

    public const ulong CONFIG_OPT_CORE_SYSTEM = 0x2000000100000000; // R
    public const ulong CONFIG_OPT_CORE_MEMORYMODEL = 0x2000000200000000; // R

    public const ulong CONFIG_OPT_CONFIG_IS_REFRESH_ENABLED = 0x2000000300000000; // R - 1/0
    public const ulong CONFIG_OPT_CONFIG_TICK_PERIOD = 0x2000000400000000; // RW - base tick period in ms
    public const ulong CONFIG_OPT_CONFIG_READCACHE_TICKS = 0x2000000500000000; // RW - memory cache validity period (in ticks)
    public const ulong CONFIG_OPT_CONFIG_TLBCACHE_TICKS = 0x2000000600000000; // RW - page table (tlb) cache validity period (in ticks)
    public const ulong CONFIG_OPT_CONFIG_PROCCACHE_TICKS_PARTIAL = 0x2000000700000000; // RW - process refresh (partial) period (in ticks)
    public const ulong CONFIG_OPT_CONFIG_PROCCACHE_TICKS_TOTAL = 0x2000000800000000; // RW - process refresh (full) period (in ticks)
    public const ulong CONFIG_OPT_CONFIG_VMM_VERSION_MAJOR = 0x2000000900000000; // R
    public const ulong CONFIG_OPT_CONFIG_VMM_VERSION_MINOR = 0x2000000A00000000; // R
    public const ulong CONFIG_OPT_CONFIG_VMM_VERSION_REVISION = 0x2000000B00000000; // R
    public const ulong CONFIG_OPT_CONFIG_STATISTICS_FUNCTIONCALL = 0x2000000C00000000; // RW - enable function call statistics (.status/statistics_fncall file)
    public const ulong CONFIG_OPT_CONFIG_IS_PAGING_ENABLED = 0x2000000D00000000; // RW - 1/0
    public const ulong CONFIG_OPT_CONFIG_DEBUG = 0x2000000E00000000; // W
    public const ulong CONFIG_OPT_CONFIG_YARA_RULES = 0x2000000F00000000; // R

    public const ulong CONFIG_OPT_WIN_VERSION_MAJOR = 0x2000010100000000; // R
    public const ulong CONFIG_OPT_WIN_VERSION_MINOR = 0x2000010200000000; // R
    public const ulong CONFIG_OPT_WIN_VERSION_BUILD = 0x2000010300000000; // R
    public const ulong CONFIG_OPT_WIN_SYSTEM_UNIQUE_ID = 0x2000010400000000; // R

    public const ulong CONFIG_OPT_FORENSIC_MODE = 0x2000020100000000; // RW - enable/retrieve forensic mode type [0-4].

    // REFRESH OPTIONS:
    public const ulong CONFIG_OPT_REFRESH_ALL = 0x2001ffff00000000; // W - refresh all caches
    public const ulong CONFIG_OPT_REFRESH_FREQ_MEM = 0x2001100000000000; // W - refresh memory cache (excl. TLB) [fully]
    public const ulong CONFIG_OPT_REFRESH_FREQ_MEM_PARTIAL = 0x2001000200000000; // W - refresh memory cache (excl. TLB) [partial 33%/call]
    public const ulong CONFIG_OPT_REFRESH_FREQ_TLB = 0x2001080000000000; // W - refresh page table (TLB) cache [fully]
    public const ulong CONFIG_OPT_REFRESH_FREQ_TLB_PARTIAL = 0x2001000400000000; // W - refresh page table (TLB) cache [partial 33%/call]
    public const ulong CONFIG_OPT_REFRESH_FREQ_FAST = 0x2001040000000000; // W - refresh fast frequency - incl. partial process refresh
    public const ulong CONFIG_OPT_REFRESH_FREQ_MEDIUM = 0x2001000100000000; // W - refresh medium frequency - incl. full process refresh
    public const ulong CONFIG_OPT_REFRESH_FREQ_SLOW = 0x2001001000000000; // W - refresh slow frequency.

    // PROCESS OPTIONS: [LO-DWORD: Process PID]
    public const ulong CONFIG_OPT_PROCESS_DTB = 0x2002000100000000; // W - force set process directory table base.
    public const ulong CONFIG_OPT_PROCESS_DTB_FAST_LOWINTEGRITY = 0x2002000200000000; // W - force set process directory table base (fast, low integrity mode, with less checks) - use at own risk!.

    //---------------------------------------------------------------------
    // CONFIG GET/SET:
    //---------------------------------------------------------------------

    /// <summary>
    ///     Get a configuration option given by a Vmm.CONFIG_* constant.
    /// </summary>
    /// <param name="fOption">The a Vmm.CONFIG_* option to get.</param>
    /// <returns>The config value retrieved on success. NULL on fail.</returns>
    public ulong? GetConfig(ulong fOption)
    {
        if (!Vmmi.VMMDLL_ConfigGet(_h, fOption, out var value))
            return null;
        return value;
    }

    /// <summary>
    ///     Set a configuration option given by a Vmm.CONFIG_* constant.
    /// </summary>
    /// <param name="fOption">The Vmm.CONFIG_* option to set.</param>
    /// <param name="qwValue">The value to set.</param>
    /// <returns></returns>
    public bool SetConfig(ulong fOption, ulong qwValue)
    {
        return Vmmi.VMMDLL_ConfigSet(_h, fOption, qwValue);
    }

    /// <summary>
    ///     Perform Common Memory Map Setup.
    /// </summary>
    /// <param name="strMap">Memory map result in String Format.</param>
    /// <param name="applyMap">(Optional) True if you would like to apply the Memory Map to the current Vmm/LeechCore instance.</param>
    /// <param name="outputFile">(Optional) If Non-Null, will write the Memory Map to disk at the specified output location.</param>
    /// <exception cref="VmmException"></exception>
    public void SetupMemoryMap(
        out string strMap,
        bool applyMap = false,
        string outputFile = null)
    {
        var map = MapMemory();
        if (map.Length == 0)
            throw new VmmException("Failed to get memory map.");
        var sb = new StringBuilder();
        var leftLength = map.Max(x => x.pa).ToString("x").Length;
        for (var i = 0; i < map.Length; i++)
            sb.AppendFormat($"{{0,{-leftLength}}}", map[i].pa.ToString("x"))
                .Append($" - {(map[i].pa + map[i].cb - 1).ToString("x")}")
                .AppendLine();
        strMap = sb.ToString();
        if (applyMap)
            if (!LeechCore.ExecuteCommand(LeechCore.LC_CMD_MEMMAP_SET, Encoding.UTF8.GetBytes(strMap), out _))
                throw new VmmException("LC_CMD_MEMMAP_SET FAIL");
        if (outputFile is not null) File.WriteAllBytes(outputFile, Encoding.UTF8.GetBytes(strMap));
    }

    #endregion

    #region Memory Read/Write

    //---------------------------------------------------------------------
    // MEMORY READ/WRITE FUNCTIONALITY BELOW:
    //---------------------------------------------------------------------

    public const uint PID_PHYSICALMEMORY = unchecked((uint)-1); // Pass as a PID Parameter to read Physical Memory
    public const uint PID_PROCESS_WITH_KERNELMEMORY = 0x80000000; // Combine with dwPID to enable process kernel memory (NB! use with extreme care).

    public const uint FLAG_NOCACHE = 0x0001; // do not use the data cache (force reading from memory acquisition device)
    public const uint FLAG_ZEROPAD_ON_FAIL = 0x0002; // zero pad failed physical memory reads and report success if read within range of physical memory.
    public const uint FLAG_FORCECACHE_READ = 0x0008; // force use of cache - fail non-cached pages - only valid for reads, invalid with VMM_FLAG_NOCACHE/VMM_FLAG_ZEROPAD_ON_FAIL.
    public const uint FLAG_NOPAGING = 0x0010; // do not try to retrieve memory from paged out memory from pagefile/compressed (even if possible)
    public const uint FLAG_NOPAGING_IO = 0x0020; // do not try to retrieve memory from paged out memory if read would incur additional I/O (even if possible).
    public const uint FLAG_NOCACHEPUT = 0x0100; // do not write back to the data cache upon successful read from memory acquisition device.
    public const uint FLAG_CACHE_RECENT_ONLY = 0x0200; // only fetch from the most recent active cache region when reading.
    public const uint FLAG_NO_PREDICTIVE_READ = 0x0400; // do not use predictive read-ahead when reading memory.
    public const uint FLAG_FORCECACHE_READ_DISABLE = 0x0800; // this flag is only recommended for local files. improves forensic artifact order.
    public const uint FLAG_SCATTER_PREPAREEX_NOMEMZERO = 0x1000; // (not used by the C# API).
    public const uint FLAG_NOMEMCALLBACK = 0x2000; // (not used by the C# API).
    public const uint FLAG_SCATTER_FORCE_PAGEREAD = 0x4000; // (not used by the C# API).

    // !!! For Physical Memory R/W use LeechCore Module

    #endregion

    #region VFS (Virtual File System) functionality

    //---------------------------------------------------------------------
    // VFS (VIRTUAL FILE SYSTEM) FUNCTIONALITY BELOW:
    //---------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_VFS_FILELIST_EXINFO
    {
        public uint dwVersion;
        public bool fCompressed;
        public ulong ftCreationTime;
        public ulong ftLastAccessTime;
        public ulong ftLastWriteTime;
    }

    public struct VfsEntry
    {
        public string name;
        public bool isDirectory;
        public ulong size;
        public VMMDLL_VFS_FILELIST_EXINFO info;
    }

    /// <summary>
    ///     VFS list callback function for adding files.
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="name"></param>
    /// <param name="cb"></param>
    /// <param name="pExInfo"></param>
    /// <returns></returns>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool VfsCallBack_AddFile(ulong ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, ulong cb, IntPtr pExInfo);

    /// <summary>
    ///     VFS list callback function for adding directories.
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="name"></param>
    /// <param name="pExInfo"></param>
    /// <returns></returns>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool VfsCallBack_AddDirectory(ulong ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, IntPtr pExInfo);

    private static bool VfsList_AddFileCB(ulong h, [MarshalAs(UnmanagedType.LPUTF8Str)] string sName, ulong cb, IntPtr pExInfo)
    {
        var gcHandle = (GCHandle)new IntPtr((long)h);
        var ctx = (List<VfsEntry>)gcHandle.Target;
        var e = new VfsEntry
        {
            name = sName,
            isDirectory = false,
            size = cb
        };
        if (pExInfo != IntPtr.Zero) e.info = Marshal.PtrToStructure<VMMDLL_VFS_FILELIST_EXINFO>(pExInfo);
        ctx!.Add(e);
        return true;
    }

    private static bool VfsList_AddDirectoryCB(ulong h, [MarshalAs(UnmanagedType.LPUTF8Str)] string sName, IntPtr pExInfo)
    {
        var gcHandle = (GCHandle)new IntPtr((long)h);
        var ctx = (List<VfsEntry>)gcHandle.Target;
        var e = new VfsEntry
        {
            name = sName,
            isDirectory = true,
            size = 0
        };
        if (pExInfo != IntPtr.Zero) e.info = Marshal.PtrToStructure<VMMDLL_VFS_FILELIST_EXINFO>(pExInfo);
        ctx!.Add(e);
        return true;
    }

    /// <summary>
    ///     VFS list files and directories in a virtual file system path using callback functions.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="ctx">A user-supplied context which will be passed on to the callback functions.</param>
    /// <param name="CallbackFile"></param>
    /// <param name="CallbackDirectory"></param>
    /// <returns></returns>
    public bool VfsList(string path, ulong ctx, VfsCallBack_AddFile CallbackFile, VfsCallBack_AddDirectory CallbackDirectory)
    {
        Vmmi.VMMDLL_VFS_FILELIST FileList;
        FileList.dwVersion = Vmmi.VMMDLL_VFS_FILELIST_VERSION;
        FileList.h = ctx;
        FileList._Reserved = 0;
        FileList.pfnAddFile = Marshal.GetFunctionPointerForDelegate(CallbackFile);
        FileList.pfnAddDirectory = Marshal.GetFunctionPointerForDelegate(CallbackDirectory);
        return Vmmi.VMMDLL_VfsList(_h, path.Replace('/', '\\'), ref FileList);
    }

    /// <summary>
    ///     VFS list files and directories in a virtual file system path.
    /// </summary>
    /// <param name="path"></param>
    /// <returns>A list with file and directory entries on success. An empty list on fail.</returns>
    public List<VfsEntry> VfsList(string path)
    {
        var ctx = new List<VfsEntry>();
        var gcHandle = GCHandle.Alloc(ctx);
        var nativeHandle = (ulong)((IntPtr)gcHandle).ToInt64();
        VfsList(path, nativeHandle, VfsList_AddFileCB, VfsList_AddDirectoryCB);
        return ctx;
    }

    /// <summary>
    ///     VFS read data from a virtual file.
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="ntStatus">The NTSTATUS value of the operation (success = 0).</param>
    /// <param name="size">The maximum number of bytes to read. (0 = default = 16MB).</param>
    /// <param name="offset"></param>
    /// <returns>The data read on success. Zero-length data on fail. NB! data read may be shorter than size!</returns>
    public unsafe byte[] VfsRead(string fileName, out uint ntStatus, uint size = 0, ulong offset = 0)
    {
        uint cbRead = 0;
        if (size == 0) size = 0x01000000; // 16MB
        var data = new byte[size];
        fixed (byte* pb = data)
        {
            ntStatus = Vmmi.VMMDLL_VfsRead(_h, fileName.Replace('/', '\\'), pb, size, out cbRead, offset);
            var pbData = new byte[cbRead];
            if (cbRead > 0) Buffer.BlockCopy(data, 0, pbData, 0, (int)cbRead);
            return pbData;
        }
    }

    /// <summary>
    ///     VFS read data from a virtual file.
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="size"></param>
    /// <param name="offset"></param>
    /// <returns>The data read on success. Zero-length data on fail. NB! data read may be shorter than size!</returns>
    public byte[] VfsRead(string fileName, uint size = 0, ulong offset = 0)
    {
        return VfsRead(fileName, out _, size, offset);
    }

    /// <summary>
    ///     VFS write data to a virtual file.
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="data"></param>
    /// <param name="offset"></param>
    /// <returns>The NTSTATUS value of the operation (success = 0).</returns>
    public unsafe uint VfsWrite(string fileName, byte[] data, ulong offset = 0)
    {
        ThrowIfMemWritesDisabled();
        uint cbRead = 0;
        fixed (byte* pb = data)
        {
            return Vmmi.VMMDLL_VfsWrite(_h, fileName.Replace('/', '\\'), pb, (uint)data.Length, out cbRead, offset);
        }
    }

    #endregion

    #region Process functionality

    //---------------------------------------------------------------------
    // PROCESS FUNCTIONALITY BELOW:
    //---------------------------------------------------------------------

    /// <summary>
    ///     Lookup a process by its name.
    ///     Validation is also performed to ensure the process is valid.
    /// </summary>
    /// <param name="sProcName">Process name to get.</param>
    /// <returns>A VmmProcess if successful, if unsuccessful null.</returns>
    public VmmProcess GetProcessByName(string sProcName)
    {
        if (Vmmi.VMMDLL_PidGetFromName(_h, sProcName, out var pdwPID)) return new VmmProcess(this, pdwPID);
        return null;
    }

    /// <summary>
    ///     Lookup a Process by its Process ID.
    ///     Validation is also performed to ensure the process is valid.
    /// </summary>
    /// <param name="pid">Process ID to get.</param>
    /// <returns>A VmmProcess if successful, if unsuccessful null.</returns>
    public VmmProcess GetProcessByPID(uint pid)
    {
        var process = new VmmProcess(this, pid);
        if (process.GetInfo() is VmmProcess.ProcessInfo info && info.fValid)
            return process;
        return null;
    }

    /// <summary>
    ///     Returns All Processes on the Target System.
    /// </summary>
    public VmmProcess[] Processes =>
        PIDs.Select(pid => new VmmProcess(this, pid)).ToArray();

    /// <summary>
    ///     Returns All Process IDs on the Target System.
    /// </summary>
    public unsafe uint[] PIDs
    {
        get
        {
            bool result;
            ulong c = 0;
            result = Vmmi.VMMDLL_PidList(_h, null, ref c);
            if (!result || c == 0) return Array.Empty<uint>();
            fixed (byte* pb = new byte[c * 4])
            {
                result = Vmmi.VMMDLL_PidList(_h, pb, ref c);
                if (!result || c == 0) return Array.Empty<uint>();
                var m = new uint[c];
                for (ulong i = 0; i < c; i++) m[i] = (uint)Marshal.ReadInt32((IntPtr)(pb + i * 4));
                return m;
            }
        }
    }

    #endregion

    #region Registry functionality

    //---------------------------------------------------------------------
    // REGISTRY FUNCTIONALITY BELOW:
    //---------------------------------------------------------------------

    public struct RegHiveEntry
    {
        public ulong vaCMHIVE;
        public ulong vaHBASE_BLOCK;
        public uint cbLength;
        public string sName;
        public string sNameShort;
        public string sHiveRootPath;
    }

    public struct RegEnumKeyEntry
    {
        public string sName;
        public ulong ftLastWriteTime;
    }

    public struct RegEnumValueEntry
    {
        public string sName;
        public uint type;
        public uint size;
    }

    public struct RegEnumEntry
    {
        public string sKeyFullPath;
        public List<RegEnumKeyEntry> KeyList;
        public List<RegEnumValueEntry> ValueList;
    }

    /// <summary>
    ///     List the registry hives.
    /// </summary>
    /// <returns></returns>
    public unsafe RegHiveEntry[] RegHiveList()
    {
        bool result;
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_REGISTRY_HIVE_INFORMATION>();
        result = Vmmi.VMMDLL_WinReg_HiveList(_h, null, 0, out var cHives);
        if (!result || cHives == 0) return Array.Empty<RegHiveEntry>();
        fixed (byte* pb = new byte[cHives * cbENTRY])
        {
            result = Vmmi.VMMDLL_WinReg_HiveList(_h, pb, cHives, out cHives);
            if (!result) return Array.Empty<RegHiveEntry>();
            var m = new RegHiveEntry[cHives];
            for (var i = 0; i < cHives; i++)
            {
                var n = Marshal.PtrToStructure<Vmmi.VMMDLL_REGISTRY_HIVE_INFORMATION>((IntPtr)(pb + i * cbENTRY));
                RegHiveEntry e;
                if (n.wVersion != Vmmi.VMMDLL_REGISTRY_HIVE_INFORMATION_VERSION) return Array.Empty<RegHiveEntry>();
                e.vaCMHIVE = n.vaCMHIVE;
                e.vaHBASE_BLOCK = n.vaHBASE_BLOCK;
                e.cbLength = n.cbLength;
                e.sName = Encoding.UTF8.GetString(n.uszName);
                e.sName = e.sName.Substring(0, e.sName.IndexOf((char)0));
                e.sNameShort = Encoding.UTF8.GetString(n.uszNameShort);
                e.sHiveRootPath = Encoding.UTF8.GetString(n.uszHiveRootPath);
                m[i] = e;
            }

            return m;
        }
    }

    /// <summary>
    ///     Read from a registry hive.
    /// </summary>
    /// <param name="vaCMHIVE">The virtual address of the registry hive.</param>
    /// <param name="ra">The hive registry address (ra).</param>
    /// <param name="cb"></param>
    /// <param name="flags"></param>
    /// <returns>Read data on success (length may differ from requested read size). Zero-length array on fail.</returns>
    public unsafe byte[] RegHiveRead(ulong vaCMHIVE, uint ra, uint cb, uint flags = 0)
    {
        uint cbRead;
        var data = new byte[cb];
        fixed (byte* pb = data)
        {
            if (!Vmmi.VMMDLL_WinReg_HiveReadEx(_h, vaCMHIVE, ra, pb, cb, out cbRead, flags)) return Array.Empty<byte>();
        }

        if (cbRead != cb) Array.Resize(ref data, (int)cbRead);
        return data;
    }

    /// <summary>
    ///     Write to a registry hive. NB! This is a very dangerous operation and is not recommended!
    /// </summary>
    /// <param name="vaCMHIVE">>The virtual address of the registry hive.</param>
    /// <param name="ra">The hive registry address (ra).</param>
    /// <param name="data"></param>
    /// <returns></returns>
    public unsafe bool RegHiveWrite(ulong vaCMHIVE, uint ra, byte[] data)
    {
        ThrowIfMemWritesDisabled();
        fixed (byte* pb = data)
        {
            return Vmmi.VMMDLL_WinReg_HiveWrite(_h, vaCMHIVE, ra, pb, (uint)data.Length);
        }
    }

    /// <summary>
    ///     Enumerate a registry key for subkeys and values.
    /// </summary>
    /// <param name="sKeyFullPath"></param>
    /// <returns></returns>
    public unsafe RegEnumEntry RegEnum(string sKeyFullPath)
    {
        uint i, cchName, cbData = 0;
        var re = new RegEnumEntry
        {
            sKeyFullPath = sKeyFullPath,
            KeyList = new List<RegEnumKeyEntry>(),
            ValueList = new List<RegEnumValueEntry>()
        };
        fixed (byte* pb = new byte[0x1000])
        {
            i = 0;
            cchName = 0x800;
            while (Vmmi.VMMDLL_WinReg_EnumKeyEx(_h, sKeyFullPath, i, pb, ref cchName, out var ftLastWriteTime))
            {
                var e = new RegEnumKeyEntry
                {
                    ftLastWriteTime = ftLastWriteTime,
                    sName = new string((sbyte*)pb, 0, 2 * (int)Math.Max(1, cchName) - 2, Encoding.UTF8)
                };
                re.KeyList.Add(e);
                i++;
                cchName = 0x800;
            }

            i = 0;
            cchName = 0x800;
            while (Vmmi.VMMDLL_WinReg_EnumValue(_h, sKeyFullPath, i, pb, ref cchName, out var lpType, null, ref cbData))
            {
                var e = new RegEnumValueEntry
                {
                    type = lpType,
                    size = cbData,
                    sName = new string((sbyte*)pb, 0, 2 * (int)Math.Max(1, cchName) - 2, Encoding.UTF8)
                };
                re.ValueList.Add(e);
                i++;
                cchName = 0x800;
            }
        }

        return re;
    }

    /// <summary>
    ///     Read a registry value.
    /// </summary>
    /// <param name="sValueFullPath"></param>
    /// <param name="tp"></param>
    /// <returns></returns>
    public unsafe byte[] RegValueRead(string sValueFullPath, out uint tp)
    {
        bool result;
        uint cb = 0;
        result = Vmmi.VMMDLL_WinReg_QueryValueEx(_h, sValueFullPath, out tp, null, ref cb);
        if (!result) return null;
        var data = new byte[cb];
        fixed (byte* pb = data)
        {
            result = Vmmi.VMMDLL_WinReg_QueryValueEx(_h, sValueFullPath, out tp, pb, ref cb);
            return result ? data : null;
        }
    }

    #endregion // Registry functionality

    #region Map functionality

    //---------------------------------------------------------------------
    // "MAP" FUNCTIONALITY BELOW:
    //---------------------------------------------------------------------

    public const ulong MEMMAP_FLAG_PAGE_W = 0x0000000000000002;
    public const ulong MEMMAP_FLAG_PAGE_NS = 0x0000000000000004;
    public const ulong MEMMAP_FLAG_PAGE_NX = 0x8000000000000000;
    public const ulong MEMMAP_FLAG_PAGE_MASK = 0x8000000000000006;

    public struct NetEntryAddress
    {
        public bool fValid;
        public ushort port;
        public byte[] pbAddr;
        public string sText;
    }

    public struct NetEntry
    {
        public uint dwPID;
        public uint dwState;
        public uint dwPoolTag;
        public ushort AF;
        public NetEntryAddress src;
        public NetEntryAddress dst;
        public ulong vaObj;
        public ulong ftTime;
        public string sText;
    }

    public struct MemoryEntry
    {
        public ulong pa;
        public ulong cb;
    }

    public struct KDeviceEntry
    {
        public ulong va;
        public uint iDepth;
        public uint dwDeviceType;
        public string sDeviceType;
        public ulong vaDriverObject;
        public ulong vaAttachedDevice;
        public ulong vaFileSystemDevice;
        public string sVolumeInfo;
    }

    public struct KDriverEntry
    {
        public ulong va;
        public ulong vaDriverStart;
        public ulong cbDriverSize;
        public ulong vaDeviceObject;
        public string sName;
        public string sPath;
        public string sServiceKeyName;
        public ulong[] MajorFunction;
    }

    public struct KObjectEntry
    {
        public ulong va;
        public ulong vaParent;
        public ulong[] vaChild;
        public string sName;
        public string sType;
    }

    public struct PoolEntry
    {
        public ulong va;
        public uint cb;
        public uint fAlloc;
        public uint tpPool;
        public uint tpSS;
        public uint dwTag;
        public string sTag;
    }

    public struct UserEntry
    {
        public string sSID;
        public string sText;
        public ulong vaRegHive;
    }

    public struct VirtualMachineEntry
    {
        public ulong hVM;
        public string sName;
        public ulong gpaMax;
        public uint tp;
        public bool fActive;
        public bool fReadOnly;
        public bool fPhysicalOnly;
        public uint dwPartitionID;
        public uint dwVersionBuild;
        public uint tpSystem;
        public uint dwParentVmmMountID;
        public uint dwVmMemPID;
    }

    public struct ServiceEntry
    {
        public ulong vaObj;
        public uint dwPID;
        public uint dwOrdinal;
        public string sServiceName;
        public string sDisplayName;
        public string sPath;
        public string sUserTp;
        public string sUserAcct;
        public string sImagePath;
        public uint dwStartType;
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
    }

    public enum PfnType
    {
        Zero = 0,
        Free = 1,
        Standby = 2,
        Modified = 3,
        ModifiedNoWrite = 4,
        Bad = 5,
        Active = 6,
        Transition = 7
    }

    public enum PfnTypeExtended
    {
        Unknown = 0,
        Unused = 1,
        ProcessPrivate = 2,
        PageTable = 3,
        LargePage = 4,
        DriverLocked = 5,
        Shareable = 6,
        File = 7
    }

    public struct PfnEntry
    {
        public uint dwPfn;
        public PfnType tp;
        public PfnTypeExtended tpExtended;
        public ulong va;
        public ulong vaPte;
        public ulong OriginalPte;
        public uint dwPID;
        public bool fPrototype;
        public bool fModified;
        public bool fReadInProgress;
        public bool fWriteInProgress;
        public byte priority;
    }

    public unsafe NetEntry[] MapNet()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_NET>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_NETENTRY>();
        var m = Array.Empty<NetEntry>();
        if (!Vmmi.VMMDLL_Map_GetNet(_h, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_NET>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_NET_VERSION) goto fail;
        m = new NetEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_NETENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            NetEntry e;
            e.dwPID = n.dwPID;
            e.dwState = n.dwState;
            e.dwPoolTag = n.dwPoolTag;
            e.AF = n.AF;
            e.src.fValid = n.src_fValid;
            e.src.port = n.src_port;
            e.src.pbAddr = n.src_pbAddr;
            e.src.sText = n.src_uszText;
            e.dst.fValid = n.dst_fValid;
            e.dst.port = n.dst_port;
            e.dst.pbAddr = n.dst_pbAddr;
            e.dst.sText = n.dst_uszText;
            e.vaObj = n.vaObj;
            e.ftTime = n.ftTime;
            e.sText = n.uszText;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    ///     Retrieve the physical memory map.
    /// </summary>
    /// <returns>An array of MemoryEntry elements.</returns>
    public unsafe MemoryEntry[] MapMemory()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_PHYSMEM>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_PHYSMEMENTRY>();
        var m = Array.Empty<MemoryEntry>();
        if (!Vmmi.VMMDLL_Map_GetPhysMem(_h, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_PHYSMEM>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_PHYSMEM_VERSION) goto fail;
        m = new MemoryEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_PHYSMEMENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            MemoryEntry e;
            e.pa = n.pa;
            e.cb = n.cb;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    ///     Retrieve the kernel devices on the system.
    /// </summary>
    /// <returns>An array of KDeviceEntry elements.</returns>
    public unsafe KDeviceEntry[] MapKDevice()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_KDEVICE>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_KDEVICEENTRY>();
        var m = Array.Empty<KDeviceEntry>();
        if (!Vmmi.VMMDLL_Map_GetKDevice(_h, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_KDEVICE>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_KDEVICE_VERSION) goto fail;
        m = new KDeviceEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_KDEVICEENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            KDeviceEntry e;
            e.va = n.va;
            e.iDepth = n.iDepth;
            e.dwDeviceType = n.dwDeviceType;
            e.sDeviceType = n.uszDeviceType;
            e.vaDriverObject = n.vaDriverObject;
            e.vaAttachedDevice = n.vaAttachedDevice;
            e.vaFileSystemDevice = n.vaFileSystemDevice;
            e.sVolumeInfo = n.uszVolumeInfo;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    ///     Retrieve the kernel drivers on the system.
    /// </summary>
    /// <returns>An array of KDriverEntry elements.</returns>
    public unsafe KDriverEntry[] MapKDriver()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_KDRIVER>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_KDRIVERENTRY>();
        var m = Array.Empty<KDriverEntry>();
        if (!Vmmi.VMMDLL_Map_GetKDriver(_h, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_KDRIVER>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_KDRIVER_VERSION) goto fail;
        m = new KDriverEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_KDRIVERENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            KDriverEntry e;
            e.va = n.va;
            e.vaDriverStart = n.vaDriverStart;
            e.cbDriverSize = n.cbDriverSize;
            e.vaDeviceObject = n.vaDeviceObject;
            e.sName = n.uszName;
            e.sPath = n.uszPath;
            e.sServiceKeyName = n.uszServiceKeyName;
            e.MajorFunction = new ulong[28];
            for (var j = 0; j < 28; j++) e.MajorFunction[j] = n.MajorFunction[j];
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    ///     Retrieve the kernel named objects on the system.
    /// </summary>
    /// <returns>An array of KObjectEntry elements.</returns>
    public unsafe KObjectEntry[] MapKObject()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_KOBJECT>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_KOBJECTENTRY>();
        var m = Array.Empty<KObjectEntry>();
        if (!Vmmi.VMMDLL_Map_GetKObject(_h, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_KOBJECT>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_KOBJECT_VERSION) goto fail;
        m = new KObjectEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_KOBJECTENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            KObjectEntry e;
            e.va = n.va;
            e.vaParent = n.vaParent;
            e.vaChild = new ulong[n.cvaChild];
            for (var j = 0; j < n.cvaChild; j++) e.vaChild[j] = (ulong)Marshal.ReadInt64(n.pvaChild, j * 8);
            e.sName = n.uszName;
            e.sType = n.uszType;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    ///     Retrieve entries from the kernel pool.
    /// </summary>
    /// <param name="isBigPoolOnly">
    ///     Set to true to only retrieve big pool allocations (= faster). Default is to retrieve all
    ///     allocations.
    /// </param>
    /// <returns>An array of PoolEntry elements.</returns>
    public unsafe PoolEntry[] MapPool(bool isBigPoolOnly = false)
    {
        byte[] tag = { 0, 0, 0, 0 };
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_POOL>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_POOLENTRY>();
        var flags = isBigPoolOnly ? Vmmi.VMMDLL_POOLMAP_FLAG_BIG : Vmmi.VMMDLL_POOLMAP_FLAG_ALL;
        if (!Vmmi.VMMDLL_Map_GetPool(_h, out var pN, flags)) return Array.Empty<PoolEntry>();
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_POOL>(pN);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_POOL_VERSION)
        {
            Vmmi.VMMDLL_MemFree((byte*)pN.ToPointer());
            return Array.Empty<PoolEntry>();
        }

        var eM = new PoolEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var nE = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_POOLENTRY>((IntPtr)(pN.ToInt64() + cbMAP + i * cbENTRY));
            eM[i].va = nE.va;
            eM[i].cb = nE.cb;
            eM[i].tpPool = nE.tpPool;
            eM[i].tpSS = nE.tpSS;
            eM[i].dwTag = nE.dwTag;
            tag[0] = (byte)((nE.dwTag >> 00) & 0xff);
            tag[1] = (byte)((nE.dwTag >> 08) & 0xff);
            tag[2] = (byte)((nE.dwTag >> 16) & 0xff);
            tag[3] = (byte)((nE.dwTag >> 24) & 0xff);
            eM[i].sTag = Encoding.ASCII.GetString(tag);
        }

        Vmmi.VMMDLL_MemFree((byte*)pN.ToPointer());
        return eM;
    }

    /// <summary>
    ///     Retrieve the detected users on the system.
    /// </summary>
    /// <returns>An array of UserEntry elements.</returns>
    public unsafe UserEntry[] MapUser()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_USER>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_USERENTRY>();
        var m = Array.Empty<UserEntry>();
        if (!Vmmi.VMMDLL_Map_GetUsers(_h, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_USER>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_USER_VERSION) goto fail;
        m = new UserEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_USERENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            UserEntry e;
            e.sSID = n.uszSID;
            e.sText = n.uszText;
            e.vaRegHive = n.vaRegHive;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    ///     Retrieve the detected virtual machines on the system. This includes Hyper-V, WSL and other virtual machines running
    ///     on top of the Windows Hypervisor Platform.
    /// </summary>
    /// <returns>An array of VirtualMachineEntry elements.</returns>
    public unsafe VirtualMachineEntry[] MapVirtualMachine()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_VM>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_VMENTRY>();
        var m = Array.Empty<VirtualMachineEntry>();
        if (!Vmmi.VMMDLL_Map_GetVM(_h, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_VM>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_VM_VERSION) goto fail;
        m = new VirtualMachineEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_VMENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            VirtualMachineEntry e;
            e.hVM = n.hVM;
            e.sName = n.uszName;
            e.gpaMax = n.gpaMax;
            e.tp = n.tp;
            e.fActive = n.fActive;
            e.fReadOnly = n.fReadOnly;
            e.fPhysicalOnly = n.fPhysicalOnly;
            e.dwPartitionID = n.dwPartitionID;
            e.dwVersionBuild = n.dwVersionBuild;
            e.tpSystem = n.tpSystem;
            e.dwParentVmmMountID = n.dwParentVmmMountID;
            e.dwVmMemPID = n.dwVmMemPID;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    ///     Retrieve the services on the system.
    /// </summary>
    /// <returns>An array of ServiceEntry elements.</returns>
    public unsafe ServiceEntry[] MapService()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_SERVICE>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_SERVICEENTRY>();
        var m = Array.Empty<ServiceEntry>();
        if (!Vmmi.VMMDLL_Map_GetServices(_h, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_SERVICE>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_SERVICE_VERSION) goto fail;
        m = new ServiceEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_SERVICEENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            ServiceEntry e;
            e.vaObj = n.vaObj;
            e.dwPID = n.dwPID;
            e.dwOrdinal = n.dwOrdinal;
            e.sServiceName = n.uszServiceName;
            e.sDisplayName = n.uszDisplayName;
            e.sPath = n.uszPath;
            e.sUserTp = n.uszUserTp;
            e.sUserAcct = n.uszUserAcct;
            e.sImagePath = n.uszImagePath;
            e.dwStartType = n.dwStartType;
            e.dwServiceType = n.dwServiceType;
            e.dwCurrentState = n.dwCurrentState;
            e.dwControlsAccepted = n.dwControlsAccepted;
            e.dwWin32ExitCode = n.dwWin32ExitCode;
            e.dwServiceSpecificExitCode = n.dwServiceSpecificExitCode;
            e.dwCheckPoint = n.dwCheckPoint;
            e.dwWaitHint = n.dwWaitHint;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    ///     Retrieve the PFN entries for the specified PFNs.
    /// </summary>
    /// <param name="pfns">the pfn numbers of the pfns to retrieve.</param>
    /// <returns></returns>
    public unsafe PfnEntry[] MapPfn(params uint[] pfns)
    {
        bool result;
        uint cbPfns;
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_PFN>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_PFNENTRY>();
        if (pfns.Length == 0) return Array.Empty<PfnEntry>();
        var dataPfns = new byte[pfns.Length * sizeof(uint)];
        Buffer.BlockCopy(pfns, 0, dataPfns, 0, dataPfns.Length);
        fixed (byte* pbPfns = dataPfns)
        {
            cbPfns = (uint)(cbMAP + pfns.Length * cbENTRY);
            fixed (byte* pb = new byte[cbPfns])
            {
                result =
                    Vmmi.VMMDLL_Map_GetPfn(_h, pbPfns, (uint)pfns.Length, null, ref cbPfns) &&
                    Vmmi.VMMDLL_Map_GetPfn(_h, pbPfns, (uint)pfns.Length, pb, ref cbPfns);
                if (!result) return Array.Empty<PfnEntry>();
                var pm = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_PFN>((IntPtr)pb);
                if (pm.dwVersion != Vmmi.VMMDLL_MAP_PFN_VERSION) return Array.Empty<PfnEntry>();
                var m = new PfnEntry[pm.cMap];
                for (var i = 0; i < pm.cMap; i++)
                {
                    var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_PFNENTRY>((IntPtr)(pb + cbMAP + i * cbENTRY));
                    var e = new PfnEntry
                    {
                        dwPfn = n.dwPfn,
                        tp = (PfnType)((n._u3 >> 16) & 0x07),
                        tpExtended = (PfnTypeExtended)n.tpExtended,
                        vaPte = n.vaPte,
                        OriginalPte = n.OriginalPte,
                        fModified = ((n._u3 >> 20) & 1) == 1,
                        fReadInProgress = ((n._u3 >> 21) & 1) == 1,
                        fWriteInProgress = ((n._u3 >> 19) & 1) == 1,
                        priority = (byte)((n._u3 >> 24) & 7),
                        fPrototype = ((n._u4 >> 57) & 1) == 1
                    };
                    if (e.tp == PfnType.Active && !e.fPrototype)
                    {
                        e.va = n.va;
                        e.dwPID = n.dwPfnPte[0];
                    }

                    m[i] = e;
                }

                return m;
            }
        }
    }

    #endregion // Map functionality

    #region Utility functionality

    /// <summary>
    ///     Convert a byte array to a hexdump formatted string. (static method).
    /// </summary>
    /// <param name="pbData">The data to convert.</param>
    /// <param name="initialOffset">The iniital offset (default = 0).</param>
    /// <returns>A string in hexdump format representing the binary data pbData.</returns>
    public static unsafe string UtilFillHexAscii(byte[] pbData, uint initialOffset = 0)
    {
        bool result;
        var cbIn = (uint)pbData.Length;
        uint cbOut = 0;
        fixed (byte* pbIn = pbData)
        {
            result = Vmmi.VMMDLL_UtilFillHexAscii(pbIn, cbIn, initialOffset, null, ref cbOut);
            if (!result) return null;
            var dataOut = new byte[cbOut];
            fixed (byte* pbOut = dataOut)
            {
                result = Vmmi.VMMDLL_UtilFillHexAscii(pbIn, cbIn, initialOffset, pbOut, ref cbOut);
                return result ? Encoding.ASCII.GetString(dataOut) : null;
            }
        }
    }


    /// <summary>
    ///     Enum used to specify the log level.
    /// </summary>
    public enum LogLevel
    {
        Critical = 1, // critical stopping error
        Warning = 2, // severe warning error
        Info = 3, // normal/info message
        Verbose = 4, // verbose message (visible with -v)
        Debug = 5, // debug message (visible with -vv)
        Trace = 6 // trace message
    }

    /// <summary>
    ///     Log a string to the VMM log.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="logLevel">The log level (default INFO).</param>
    /// <param name="MID">Module ID (default = API).</param>
    public void Log(string message, LogLevel logLevel = LogLevel.Info, uint MID = 0x80000011)
    {
        Vmmi.VMMDLL_Log(_h, MID, (uint)logLevel, "%s", message);
    }

    private VmmKernel _kernel;

    /// <summary>
    ///     VmmKernel convenience object.
    /// </summary>
    /// <returns>The VmmKernel object.</returns>
    public VmmKernel Kernel => _kernel ??= new VmmKernel(this);

    /// <summary>
    ///     Throw an exception if memory writing is disabled.
    /// </summary>
    /// <exception cref="VmmException">Memory writing is disabled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ThrowIfMemWritesDisabled()
    {
        if (!EnableMemoryWriting)
            throw new VmmException("Memory Writing is Disabled! This operation may not proceed.");
    }

    #endregion // Utility functionality

    #region Custom Refresh Functionality

    /// <summary>
    ///     Force a 'Full' Vmm Refresh.
    /// </summary>
    public void ForceFullRefresh()
    {
        if (!SetConfig(CONFIG_OPT_REFRESH_ALL, 1))
            Log("WARNING: Vmm Full Refresh Failed!", LogLevel.Warning);
    }

    /// <summary>
    ///     Registers an Auto Refresher with a specified interval.
    ///     This is potentially useful if you initialized with -norefresh, and want to control refreshing more closely.
    ///     Minimum interval resolution ~10-15ms.
    /// </summary>
    /// <param name="option">Vmm Refresh Option</param>
    /// <param name="interval">Interval in which to fire a refresh operation.</param>
    public void RegisterAutoRefresh(RefreshOptions option, TimeSpan interval)
    {
        RefreshManager.Register(this, option, interval);
    }

    /// <summary>
    ///     Unregisters an Auto Refresher.
    /// </summary>
    /// <param name="option">Option to unregister.</param>
    public void UnregisterAutoRefresh(RefreshOptions option)
    {
        RefreshManager.Unregister(this, option);
    }

    #endregion
}