using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using VmmSharpEx.Internal;
using VmmSharpEx.Options;
using VmmSharpEx.Refresh;

namespace VmmSharpEx;

/// <summary>
/// MemProcFS public API
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
    /// Underlying LeechCore handle.
    /// </summary>
    public LeechCore LeechCore { get; }

    private readonly bool _enableMemoryWriting = true;

    /// <summary>
    /// Set to FALSE if you would like to disable all Memory Writing in this High Level API.
    /// Attempts to Write Memory will throw a VmmException.
    /// This setting is immutable after initialization.
    /// </summary>
    public bool EnableMemoryWriting
    {
        get => _enableMemoryWriting;
        init
        {
            _enableMemoryWriting = value;
            if (!_enableMemoryWriting)
            {
                Log("Memory Writing Disabled!");
            }
        }
    }

    /// <summary>
    /// ToString() override.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return _h == IntPtr.Zero ? "Vmm:NULL" : $"Vmm:{_h:X}";
    }

    /// <summary>
    /// Internal initialization factory method.
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
        if (hVMM.ToInt64() == 0)
        {
            throw new VmmException("VMM INIT FAILED.");
        }

        if (vaLcCreateErrorInfo == 0)
        {
            return hVMM;
        }

        var e = Marshal.PtrToStructure<Lci.LC_CONFIG_ERRORINFO>(pLcErrorInfo);
        if (e.dwVersion == LeechCore.LC_CONFIG_ERRORINFO_VERSION)
        {
            configErrorInfo.fValid = true;
            configErrorInfo.fUserInputRequest = e.fUserInputRequest;
            if (e.cwszUserText > 0)
            {
                configErrorInfo.strUserText = Marshal.PtrToStringUni((IntPtr)(vaLcCreateErrorInfo + cbERROR_INFO));
            }
        }

        return hVMM;
    }

    /// <summary>
    /// Private zero-argument constructor to prevent instantiation.
    /// </summary>
    private Vmm() { }

    /// <summary>
    /// Initialize a new Vmm instance with command line arguments.
    /// Also retrieve the extended error information (if there is an error).
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
    /// Initialize a new Vmm instance with command line arguments.
    /// </summary>
    /// <param name="args">MemProcFS/Vmm command line arguments.</param>
    public Vmm(params string[] args)
        : this(out _, args) { }

    /// <summary>
    /// Manually initialize plugins.
    /// By default plugins are not initialized during Vmm Init.
    /// </summary>
    /// <returns>TRUE if plugins are loaded successfully, otherwise FALSE.</returns>
    public bool InitializePlugins() => Vmmi.VMMDLL_InitializePlugins(_h);

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
    /// Close all Vmm instances in the native layer.
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



    //---------------------------------------------------------------------
    // CONFIG GET/SET:
    //---------------------------------------------------------------------

    /// <summary>
    /// Get a configuration option given by a Vmm.CONFIG_* constant.
    /// </summary>
    /// <param name="fOption">The a Vmm.CONFIG_* option to get.</param>
    /// <returns>The config value retrieved on success. NULL on fail.</returns>
    public ulong? ConfigGet(VmmOption fOption)
    {
        if (!Vmmi.VMMDLL_ConfigGet(_h, fOption, out var value))
        {
            return null;
        }

        return value;
    }

    /// <summary>
    /// Set a configuration option given by a Vmm.CONFIG_* constant.
    /// </summary>
    /// <param name="fOption">The Vmm.CONFIG_* option to set.</param>
    /// <param name="qwValue">The value to set.</param>
    /// <returns></returns>
    public bool ConfigSet(VmmOption fOption, ulong qwValue)
    {
        return Vmmi.VMMDLL_ConfigSet(_h, fOption, qwValue);
    }

    /// <summary>
    /// Returns physical memory map in string format, with additional optional setup parameters.
    /// </summary>
    /// <param name="applyMap">(Optional) True if you would like to apply the Memory Map to the current Vmm/LeechCore instance.</param>
    /// <param name="outputFile">(Optional) If Non-Null, will write the Memory Map to disk at the specified output location.</param>
    /// <returns>Memory map result in String Format.</returns>
    /// <exception cref="VmmException"></exception>
    public string GetMemoryMap(
        bool applyMap = false,
        string outputFile = null)
    {
        var map = Map_GetPhysMem();
        if (map.Length == 0)
        {
            throw new VmmException("Failed to get memory map.");
        }

        var sb = new StringBuilder();
        for (var i = 0; i < map.Length; i++)
        {
            sb.AppendLine($"{map[i].pa:X16} - {(map[i].pa + map[i].cb - 1):X16}");
        }

        string strMap = sb.ToString();
        if (applyMap)
        {
            if (!LeechCore.ExecuteCommand(LcCmd.MEMMAP_SET, Encoding.UTF8.GetBytes(strMap), out _))
            {
                throw new VmmException("LC_CMD_MEMMAP_SET FAIL");
            }
        }

        if (outputFile is not null)
        {
            File.WriteAllBytes(outputFile, Encoding.UTF8.GetBytes(strMap));
        }

        return strMap;
    }

    #endregion

    #region Memory Read/Write

    //---------------------------------------------------------------------
    // MEMORY READ/WRITE FUNCTIONALITY BELOW:
    //---------------------------------------------------------------------

    public const uint PID_PHYSICALMEMORY = unchecked((uint)-1); // Pass as a PID Parameter to read Physical Memory
    public const uint PID_PROCESS_WITH_KERNELMEMORY = 0x80000000; // Combine with dwPID to enable process kernel memory (NB! use with extreme care).

    /// <summary>
    /// Perform a scatter read of multiple page-sized virtual memory ranges.
    /// Does not copy the read memory to a managed byte buffer, but instead allows direct access to the native memory via a
    /// Span view.
    /// </summary>
    /// <param name="pid">Process ID (PID) this operation will take place within.</param>
    /// <param name="flags">Vmm Flags.</param>
    /// <param name="vas">Array of page-aligned Memory Addresses.</param>
    /// <returns>SCATTER_HANDLE</returns>
    /// <exception cref="VmmException"></exception>
    public unsafe LeechCore.LcScatterHandle MemReadScatter(uint pid, VmmFlags flags, params Span<ulong> vas)
    {
        if (!Lci.LcAllocScatter1((uint)vas.Length, out var pppMEMs))
        {
            throw new VmmException("LcAllocScatter1 FAIL");
        }

        var ppMEMs = (LeechCore.LcMemScatter**)pppMEMs.ToPointer();
        for (var i = 0; i < vas.Length; i++)
        {
            var pMEM = ppMEMs[i];
            pMEM->qwA = vas[i] & ~(ulong)0xfff;
        }

        var results = new Dictionary<ulong, LeechCore.ScatterData>(vas.Length);
        _ = Vmmi.VMMDLL_MemReadScatter(_h, pid, pppMEMs, (uint)vas.Length, flags);
        for (var i = 0; i < vas.Length; i++)
        {
            var pMEM = ppMEMs[i];
            if (pMEM->f)
            {
                results[pMEM->qwA] = new LeechCore.ScatterData(pMEM->pb, pMEM->cb);
            }
        }

        return new LeechCore.LcScatterHandle(results, pppMEMs);
    }

    /// <summary>
    /// Read Memory from a Virtual Address into unmanaged memory.
    /// </summary>
    /// <param name="pid">Process ID (PID) this operation will take place within.</param>
    /// <param name="va">Virtual Address to read from.</param>
    /// <param name="pb">Pointer to buffer to receive read.</param>
    /// <param name="cb">Count of bytes to read.</param>
    /// <param name="cbRead">Count of bytes successfully read.</param>
    /// <param name="flags">VMM Flags.</param>
    /// <returns>True if successful, otherwise False. Be sure to check cbRead count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool MemRead(uint pid, ulong va, IntPtr pb, uint cb, out uint cbRead, VmmFlags flags = VmmFlags.NONE)
    {
        return MemRead(pid, va, pb.ToPointer(), cb, out cbRead, flags);
    }

    /// <summary>
    /// Read Memory from a Virtual Address into unmanaged memory.
    /// </summary>
    /// <param name="pid">Process ID (PID) this operation will take place within.</param>
    /// <param name="va">Virtual Address to read from.</param>
    /// <param name="pb">Pointer to buffer to receive read.</param>
    /// <param name="cb">Count of bytes to read.</param>
    /// <param name="cbRead">Count of bytes successfully read.</param>
    /// <param name="flags">VMM Flags.</param>
    /// <returns>True if successful, otherwise False. Be sure to check cbRead count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool MemRead(uint pid, ulong va, void* pb, uint cb, out uint cbRead, VmmFlags flags = VmmFlags.NONE)
    {
        return Vmmi.VMMDLL_MemReadEx(_h, pid, va, (byte*)pb, cb, out cbRead, flags);
    }

    /// <summary>
    /// Read Memory from a Virtual Address into a ref struct of Type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">Struct/Ref Struct Type.</typeparam>
    /// <param name="pid">Process ID (PID) this operation will take place within.</param>
    /// <param name="va">Virtual Address to read from.</param>
    /// <param name="result">Memory read result.</param>
    /// <param name="flags">VMM Flags.</param>
    /// <returns>TRUE if successful, otherwise FALSE.</returns>
    public unsafe bool MemReadValue<T>(uint pid, ulong va, out T result, VmmFlags flags = VmmFlags.NONE)
        where T : unmanaged, allows ref struct
    {
        var cb = (uint)sizeof(T);
        result = default;
        fixed (void* pb = &result)
        {
            return Vmmi.VMMDLL_MemReadEx(_h, pid, va, (byte*)pb, cb, out var cbRead, flags) && cbRead == cb;
        }
    }

    /// <summary>
    /// Read Memory from a Virtual Address into an Array of Type <typeparamref name="T" />.
    /// WARNING: This incurs a heap allocation for the array. Recommend using <see cref="MemReadPooledArray{T}(uint, ulong, int, VmmFlags)"/> instead.
    /// </summary>
    /// <typeparam name="T">Value Type.</typeparam>
    /// <param name="pid">Process ID (PID) this operation will take place within.</param>
    /// <param name="va">Virtual Address to read from.</param>
    /// <param name="count">Number of elements to read.</param>
    /// <param name="flags">VMM Flags.</param>
    /// <returns>Managed <typeparamref name="T" /> array, NULL if failed.</returns>
    public unsafe T[] MemReadArray<T>(uint pid, ulong va, int count, VmmFlags flags = VmmFlags.NONE)
        where T : unmanaged
    {
        var cb = (uint)(sizeof(T) * count);
        var data = new T[count];
        fixed (T* pb = data)
        {
            if (!Vmmi.VMMDLL_MemReadEx(_h, pid, va, (byte*)pb, cb, out var cbRead, flags) || cbRead != cb)
            {
                return null;
            }
        }

        return data;
    }

    /// <summary>
    /// Read Memory from a Virtual Address into a Pooled Array of Type <typeparamref name="T" />.
    /// NOTE: You must dispose the returned <see cref="IMemoryOwner{T}"/> when finished with it.
    /// </summary>
    /// <typeparam name="T">Value Type.</typeparam>
    /// <param name="pid">Process ID (PID) this operation will take place within.</param>
    /// <param name="va">Virtual Address to read from.</param>
    /// <param name="count">Number of elements to read.</param>
    /// <param name="flags">VMM Flags.</param>
    /// <returns><see cref="IMemoryOwner{T}"/> lease, NULL if failed.</returns>
    public unsafe IMemoryOwner<T> MemReadPooledArray<T>(uint pid, ulong va, int count, VmmFlags flags = VmmFlags.NONE)
        where T : unmanaged
    {
        var owner = new PooledArray<T>(count);
        var cb = (uint)(sizeof(T) * count);
        fixed (T* pb = owner.Memory.Span)
        {
            if (!Vmmi.VMMDLL_MemReadEx(_h, pid, va, (byte*)pb, cb, out var cbRead, flags) || cbRead != cb)
            {
                owner.Dispose();
                return null;
            }
        }
        return owner;
    }

    /// <summary>
    /// Read memory into a Span of <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">Value Type</typeparam>
    /// <param name="pid">Process ID (PID) this operation will take place within.</param>
    /// <param name="va">Memory address to read from.</param>
    /// <param name="span">Span to receive the memory read.</param>
    /// <param name="flags">Read flags.</param>
    /// <returns>
    /// True if successful, otherwise False.
    /// </returns>
    public unsafe bool MemReadSpan<T>(uint pid, ulong va, Span<T> span, VmmFlags flags = VmmFlags.NONE)
        where T : unmanaged
    {
        var cb = (uint)(sizeof(T) * span.Length);
        fixed (T* pb = span)
        {
            return Vmmi.VMMDLL_MemReadEx(_h, pid, va, (byte*)pb, cb, out var cbRead, flags) && cbRead == cb;
        }
    }

    /// <summary>
    /// Read Memory from a Virtual Address into a Managed String.
    /// </summary>
    /// <param name="pid">Process ID (PID) this operation will take place within.</param>
    /// <param name="va">Virtual Address to read from.</param>
    /// <param name="cb">Number of bytes to read. Keep in mind some string encodings are 2-4 bytes per character.</param>
    /// <param name="encoding">String Encoding for this read.</param>
    /// <param name="flags">VMM Flags.</param>
    /// <returns>C# Managed System.String. Null if failed.</returns>
    public unsafe string MemReadString(uint pid, ulong va, int cb, Encoding encoding,
        VmmFlags flags = VmmFlags.NONE)
    {
        byte[] rentedBytes = null;
        char[] rentedChars = null;
        try
        {
            Span<byte> bytesSource = cb <= 256 ?
                stackalloc byte[cb] : (rentedBytes = ArrayPool<byte>.Shared.Rent(cb));
            var bytes = bytesSource.Slice(0, cb); // Rented Pool can have more than cb
            if (!MemReadSpan(pid, va, bytes, flags))
            {
                return null;
            }

            int charCount = encoding.GetCharCount(bytes);
            Span<char> charsSource = charCount <= 128 ?
                stackalloc char[charCount] : (rentedChars = ArrayPool<char>.Shared.Rent(charCount));
            var chars = charsSource.Slice(0, charCount);
            encoding.GetChars(bytes, chars);
            int nt = chars.IndexOf('\0');
            return nt != -1 ?
                chars.Slice(0, nt).ToString() : chars.ToString(); // Only one string allocation
        }
        finally
        {
            if (rentedBytes is not null)
                ArrayPool<byte>.Shared.Return(rentedBytes);
            if (rentedChars is not null)
                ArrayPool<char>.Shared.Return(rentedChars);
        }
    }

    /// <summary>
    /// Prefetch pages into the MemProcFS internal cache.
    /// </summary>
    /// <param name="pid">Process ID (PID) this operation will take place within.</param>
    /// <param name="va">An array of the virtual addresses to prefetch.</param>
    /// <returns></returns>
    public unsafe bool MemPrefetchPages(uint pid, ulong[] va)
    {
        fixed (void* pb = va)
        {
            return Vmmi.VMMDLL_MemPrefetchPages(_h, pid, (byte*)pb, (uint)va.Length);
        }
    }

    /// <summary>
    /// Write Memory from unmanaged memory to a given Virtual Address.
    /// </summary>
    /// <param name="pid">Process ID (PID) this operation will take place within.</param>
    /// <param name="va">Virtual Address to write to.</param>
    /// <param name="pb">Pointer to buffer to write from.</param>
    /// <param name="cb">Count of bytes to write.</param>
    /// <returns>True if write successful, otherwise False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool MemWrite(uint pid, ulong va, IntPtr pb, uint cb)
    {
        return MemWrite(pid, va, pb.ToPointer(), cb);
    }

    /// <summary>
    /// Write Memory from unmanaged memory to a given Virtual Address.
    /// </summary>
    /// <param name="pid">Process ID (PID) this operation will take place within.</param>
    /// <param name="va">Virtual Address to write to.</param>
    /// <param name="pb">Pointer to buffer to write from.</param>
    /// <param name="cb">Count of bytes to write.</param>
    /// <returns>True if write successful, otherwise False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool MemWrite(uint pid, ulong va, void* pb, uint cb)
    {
        ThrowIfMemWritesDisabled();
        return Vmmi.VMMDLL_MemWrite(_h, pid, va, (byte*)pb, cb);
    }

    /// <summary>
    /// Write Memory from a struct value <typeparamref name="T" /> to a given Virtual Address.
    /// </summary>
    /// <typeparam name="T">Value Type.</typeparam>
    /// <param name="pid">Process ID (PID) this operation will take place within.</param>
    /// <param name="va">Virtual Address to write to.</param>
    /// <param name="value"><typeparamref name="T" /> Value to write.</param>
    /// <returns>True if write successful, otherwise False.</returns>
    public unsafe bool MemWriteValue<T>(uint pid, ulong va, T value)
        where T : unmanaged, allows ref struct
    {
        ThrowIfMemWritesDisabled();
        var cb = (uint)sizeof(T);
        return Vmmi.VMMDLL_MemWrite(_h, pid, va, (byte*)&value, cb);
    }

    /// <summary>
    /// Write Memory from a managed <typeparamref name="T" /> Array to a given Virtual Address.
    /// </summary>
    /// <typeparam name="T">Value Type.</typeparam>
    /// <param name="pid">Process ID (PID) this operation will take place within.</param>
    /// <param name="va">Virtual Address to write to.</param>
    /// <param name="data">Managed <typeparamref name="T" /> array to write.</param>
    /// <returns>True if write successful, otherwise False.</returns>
    public unsafe bool MemWriteArray<T>(uint pid, ulong va, T[] data)
        where T : unmanaged
    {
        ThrowIfMemWritesDisabled();
        var cb = (uint)sizeof(T) * (uint)data.Length;
        fixed (T* pb = data)
        {
            return Vmmi.VMMDLL_MemWrite(_h, pid, va, (byte*)pb, cb);
        }
    }

    /// <summary>
    /// Write memory from a Span of <typeparamref name="T" /> to a specified memory address.
    /// </summary>
    /// <typeparam name="T">Value Type</typeparam>
    /// <param name="pid">Process ID (PID) this operation will take place within.</param>
    /// <param name="va">Memory address to write to.</param>
    /// <param name="span">Span to write from.</param>
    /// <returns>True if successful, otherwise False.</returns>
    public unsafe bool MemWriteSpan<T>(uint pid, ulong va, Span<T> span)
        where T : unmanaged
    {
        ThrowIfMemWritesDisabled();
        var cb = (uint)(sizeof(T) * span.Length);
        fixed (T* pb = span)
        {
            return Vmmi.VMMDLL_MemWrite(_h, pid, va, (byte*)pb, cb);
        }
    }

    /// <summary>
    /// Translate a virtual address to a physical address.
    /// </summary>
    /// <param name="pid">Process ID (PID) this operation will take place within.</param>
    /// <param name="va">Virtual address to translate from.</param>
    /// <returns>Physical address if successful, zero on fail.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong MemVirt2Phys(uint pid, ulong va)
    {
        _ = Vmmi.VMMDLL_MemVirt2Phys(_h, pid, va, out var pa);
        return pa;
    }

    /// <summary>
    /// Initialize a Scatter handle used to read/write multiple virtual memory regions in a single call.
    /// </summary>
    /// <param name="pid">PID to create VmmScatter over.</param>
    /// <param name="flags">Vmm Flags.</param>
    /// <returns>A VmmScatterMemory handle.</returns>
    public VmmScatter CreateScatter(uint pid, VmmFlags flags = VmmFlags.NONE)
    {
        return new VmmScatter(this, pid, flags);
    }

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
    /// VFS list callback function for adding files.
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="name"></param>
    /// <param name="cb"></param>
    /// <param name="pExInfo"></param>
    /// <returns></returns>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool VfsCallBack_AddFile(ulong ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, ulong cb, IntPtr pExInfo);

    /// <summary>
    /// VFS list callback function for adding directories.
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
        if (pExInfo != IntPtr.Zero)
        {
            e.info = Marshal.PtrToStructure<VMMDLL_VFS_FILELIST_EXINFO>(pExInfo);
        }

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
        if (pExInfo != IntPtr.Zero)
        {
            e.info = Marshal.PtrToStructure<VMMDLL_VFS_FILELIST_EXINFO>(pExInfo);
        }

        ctx!.Add(e);
        return true;
    }

    /// <summary>
    /// VFS list files and directories in a virtual file system path using callback functions.
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
    /// VFS list files and directories in a virtual file system path.
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
    /// VFS read data from a virtual file.
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="ntStatus">The NTSTATUS value of the operation (success = 0).</param>
    /// <param name="size">The maximum number of bytes to read. (0 = default = 16MB).</param>
    /// <param name="offset"></param>
    /// <returns>The data read on success. Zero-length data on fail. NB! data read may be shorter than size!</returns>
    public unsafe byte[] VfsRead(string fileName, out uint ntStatus, uint size = 0, ulong offset = 0)
    {
        uint cbRead = 0;
        if (size == 0)
        {
            size = 0x01000000; // 16MB
        }

        var data = new byte[size];
        fixed (byte* pb = data)
        {
            ntStatus = Vmmi.VMMDLL_VfsRead(_h, fileName.Replace('/', '\\'), pb, size, out cbRead, offset);
            var pbData = new byte[cbRead];
            if (cbRead > 0)
            {
                Buffer.BlockCopy(data, 0, pbData, 0, (int)cbRead);
            }

            return pbData;
        }
    }

    /// <summary>
    /// VFS read data from a virtual file.
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
    /// VFS write data to a virtual file.
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="data"></param>
    /// <param name="offset"></param>
    /// <returns>The NTSTATUS value of the operation (success = 0).</returns>
    public unsafe uint VfsWrite(string fileName, byte[] data, ulong offset = 0)
    {
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
    /// Get all Process IDs (PIDs) currently running on the target system.
    /// </summary>
    /// <returns>Array of PIDs, empty array if failed.</returns>
    public unsafe uint[] PidGetList()
    {
        bool result;
        ulong c = 0;
        result = Vmmi.VMMDLL_PidList(_h, null, ref c);
        if (!result || c == 0)
        {
            return Array.Empty<uint>();
        }

        fixed (byte* pb = new byte[c * 4])
        {
            result = Vmmi.VMMDLL_PidList(_h, pb, ref c);
            if (!result || c == 0)
            {
                return Array.Empty<uint>();
            }

            var m = new uint[c];
            for (ulong i = 0; i < c; i++)
            {
                m[i] = (uint)Marshal.ReadInt32((IntPtr)(pb + i * 4));
            }

            return m;
        }
    }

    /// <summary>
    /// Get the Process ID (PID) for a given process name.
    /// NOTE: This only returns the first PID found for the process name. For multiple PIDs, use <see cref="PidGetAllFromName"/>/>.
    /// </summary>
    /// <param name="sProcName">Name of the process to look up.</param>
    /// <param name="pdwPID">PID result.</param>
    /// <returns>TRUE if successful, otherwise FALSE.</returns>
    public bool PidGetFromName(string sProcName, out uint pdwPID)
    {
        return Vmmi.VMMDLL_PidGetFromName(_h, sProcName, out pdwPID);
    }

    /// <summary>
    /// Get all Process IDs (PIDs) for a given process name.
    /// </summary>
    /// <param name="sProcName">Name of the process to look up.</param>
    /// <returns>Array of PIDs that match, or empty array if no matches.</returns>
    /// <exception cref="VmmException"></exception>
    public uint[] PidGetAllFromName(string sProcName)
    {
        var pids = new List<uint>();
        var procInfo = ProcessGetInformationAll();
        if (procInfo.Length == 0)
        {
            throw new VmmException("ProcessGetInformationAll FAIL");
        }
        for (var i = 0; i < procInfo.Length; i++)
        {
            if (procInfo[i].sName.Equals(sProcName, StringComparison.OrdinalIgnoreCase))
            {
                pids.Add(procInfo[i].dwPID);
            }
        }
        return pids.ToArray();
    }

    /// <summary>
    /// PTE (Page Table Entry) information.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="fIdentifyModules"></param>
    /// <returns>Array of PTEs on success. Zero-length array on fail.</returns>
    public unsafe PteEntry[] Map_GetPTE(uint pid, bool fIdentifyModules = true)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_PTE>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_PTEENTRY>();
        var m = Array.Empty<PteEntry>();
        if (!Vmmi.VMMDLL_Map_GetPte(_h, pid, fIdentifyModules, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_PTE>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_PTE_VERSION)
        {
            goto fail;
        }

        m = new PteEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_PTEENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            PteEntry e;
            e.vaBase = n.vaBase;
            e.vaEnd = n.vaBase + (n.cPages << 12) - 1;
            e.cbSize = n.cPages << 12;
            e.cPages = n.cPages;
            e.fPage = n.fPage;
            e.fWoW64 = n.fWoW64;
            e.sText = n.uszText;
            e.cSoftware = n.cSoftware;
            e.fR = true;
            e.fW = 0 != (e.fPage & 0x0000000000000002);
            e.fS = 0 == (e.fPage & 0x0000000000000004);
            e.fX = 0 == (e.fPage & 0x8000000000000000);
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    /// VAD (Virtual Address Descriptor) information.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="fIdentifyModules"></param>
    /// <returns></returns>
    public unsafe VadEntry[] Map_GetVad(uint pid, bool fIdentifyModules = true)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_VAD>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_VADENTRY>();
        var m = Array.Empty<VadEntry>();
        if (!Vmmi.VMMDLL_Map_GetVad(_h, pid, fIdentifyModules, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_VAD>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_VAD_VERSION)
        {
            goto fail;
        }

        m = new VadEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_VADENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            VadEntry e;
            e.vaStart = n.vaStart;
            e.vaEnd = n.vaEnd;
            e.cbSize = n.vaEnd + 1 - n.vaStart;
            e.vaVad = n.vaVad;
            e.VadType = n.dw0 & 0x07;
            e.Protection = (n.dw0 >> 3) & 0x1f;
            e.fImage = ((n.dw0 >> 8) & 1) == 1;
            e.fFile = ((n.dw0 >> 9) & 1) == 1;
            e.fPageFile = ((n.dw0 >> 10) & 1) == 1;
            e.fPrivateMemory = ((n.dw0 >> 11) & 1) == 1;
            e.fTeb = ((n.dw0 >> 12) & 1) == 1;
            e.fStack = ((n.dw0 >> 13) & 1) == 1;
            e.fSpare = (n.dw0 >> 14) & 0x03;
            e.HeapNum = (n.dw0 >> 16) & 0x1f;
            e.fHeap = ((n.dw0 >> 23) & 1) == 1;
            e.cwszDescription = (n.dw0 >> 24) & 0xff;
            e.CommitCharge = n.dw1 & 0x7fffffff;
            e.MemCommit = ((n.dw1 >> 31) & 1) == 1;
            e.u2 = n.u2;
            e.cbPrototypePte = n.cbPrototypePte;
            e.vaPrototypePte = n.vaPrototypePte;
            e.vaSubsection = n.vaSubsection;
            e.sText = n.uszText;
            e.vaFileObject = n.vaFileObject;
            e.cVadExPages = n.cVadExPages;
            e.cVadExPagesBase = n.cVadExPagesBase;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    /// Extended VAD (Virtual Address Descriptor) information.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="oPages"></param>
    /// <param name="cPages"></param>
    /// <returns></returns>
    public unsafe VadExEntry[] Map_GetVadEx(uint pid, uint oPages, uint cPages)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_VADEX>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_VADEXENTRY>();
        var m = Array.Empty<VadExEntry>();
        if (!Vmmi.VMMDLL_Map_GetVadEx(_h, pid, oPages, cPages, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_VADEX>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_VADEX_VERSION)
        {
            goto fail;
        }

        m = new VadExEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_VADEXENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            VadExEntry e;
            e.tp = n.tp;
            e.iPML = n.iPML;
            e.pteFlags = n.pteFlags;
            e.va = n.va;
            e.pa = n.pa;
            e.pte = n.pte;
            e.proto.tp = n.proto_tp;
            e.proto.pa = n.proto_pa;
            e.proto.pte = n.proto_pte;
            e.vaVadBase = n.vaVadBase;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    /// Module (loaded DLLs) information.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="fExtendedInfo"></param>
    /// <returns></returns>
    public unsafe ModuleEntry[] Map_GetModule(uint pid, bool fExtendedInfo = false)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_MODULE>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_MODULEENTRY>();
        var m = Array.Empty<ModuleEntry>();
        var flags = fExtendedInfo ? (uint)0xff : 0;
        if (!Vmmi.VMMDLL_Map_GetModule(_h, pid, out var pMap, flags))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_MODULE>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_MODULE_VERSION)
        {
            goto fail;
        }

        m = new ModuleEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_MODULEENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            ModuleEntry e;
            ModuleEntryDebugInfo eDbg;
            ModuleEntryVersionInfo eVer;
            e.fValid = true;
            e.vaBase = n.vaBase;
            e.vaEntry = n.vaEntry;
            e.cbImageSize = n.cbImageSize;
            e.fWow64 = n.fWow64;
            e.sText = n.uszText;
            e.sFullName = n.uszFullName;
            e.tp = n.tp;
            e.cbFileSizeRaw = n.cbFileSizeRaw;
            e.cSection = n.cSection;
            e.cEAT = n.cEAT;
            e.cIAT = n.cIAT;
            // Extended Debug Information:
            if (n.pExDebugInfo.ToInt64() == 0)
            {
                eDbg.fValid = false;
                eDbg.dwAge = 0;
                eDbg.sGuid = "";
                eDbg.sPdbFilename = "";
            }
            else
            {
                var nDbg = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_MODULEENTRY_DEBUGINFO>(n.pExDebugInfo);
                eDbg.fValid = true;
                eDbg.dwAge = nDbg.dwAge;
                eDbg.sGuid = nDbg.uszGuid;
                eDbg.sPdbFilename = nDbg.uszPdbFilename;
            }

            e.DebugInfo = eDbg;
            // Extended Version Information
            if (n.pExDebugInfo.ToInt64() == 0)
            {
                eVer.fValid = false;
                eVer.sCompanyName = "";
                eVer.sFileDescription = "";
                eVer.sFileVersion = "";
                eVer.sInternalName = "";
                eVer.sLegalCopyright = "";
                eVer.sFileOriginalFilename = "";
                eVer.sProductName = "";
                eVer.sProductVersion = "";
            }
            else
            {
                var nVer = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_MODULEENTRY_VERSIONINFO>(n.pExVersionInfo);
                eVer.fValid = true;
                eVer.sCompanyName = nVer.uszCompanyName;
                eVer.sFileDescription = nVer.uszFileDescription;
                eVer.sFileVersion = nVer.uszFileVersion;
                eVer.sInternalName = nVer.uszInternalName;
                eVer.sLegalCopyright = nVer.uszLegalCopyright;
                eVer.sFileOriginalFilename = nVer.uszFileOriginalFilename;
                eVer.sProductName = nVer.uszProductName;
                eVer.sProductVersion = nVer.uszProductVersion;
            }

            e.VersionInfo = eVer;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    /// Get a module from its name. If more than one module with the same name is loaded, the first one is returned.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="module">Module to lookup.</param>
    /// <param name="result">Result if successful.</param>
    /// <returns>TRUE if successful, otherwise FALSE.</returns>
    public unsafe bool Map_GetModuleFromName(uint pid, string module, out ModuleEntry result)
    {
        bool f = false;
        result = default;
        if (!Vmmi.VMMDLL_Map_GetModuleFromName(_h, pid, module, out var pMap, 0))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_MODULEENTRY>(pMap);
        result.fValid = true;
        result.vaBase = nM.vaBase;
        result.vaEntry = nM.vaEntry;
        result.cbImageSize = nM.cbImageSize;
        result.fWow64 = nM.fWow64;
        result.sText = module;
        result.sFullName = nM.uszFullName;
        result.tp = nM.tp;
        result.cbFileSizeRaw = nM.cbFileSizeRaw;
        result.cSection = nM.cSection;
        result.cEAT = nM.cEAT;
        result.cIAT = nM.cIAT;
        f = true;
        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return f;
    }

    /// <summary>
    /// Unloaded module information.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <returns></returns>
    public unsafe UnloadedModuleEntry[] Map_GetUnloadedModule(uint pid)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_UNLOADEDMODULE>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_UNLOADEDMODULEENTRY>();
        var m = Array.Empty<UnloadedModuleEntry>();
        if (!Vmmi.VMMDLL_Map_GetUnloadedModule(_h, pid, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_UNLOADEDMODULE>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_UNLOADEDMODULE_VERSION)
        {
            goto fail;
        }

        m = new UnloadedModuleEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_UNLOADEDMODULEENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            UnloadedModuleEntry e;
            e.vaBase = n.vaBase;
            e.cbImageSize = n.cbImageSize;
            e.fWow64 = n.fWow64;
            e.wText = n.uszText;
            e.dwCheckSum = n.dwCheckSum;
            e.dwTimeDateStamp = n.dwTimeDateStamp;
            e.ftUnload = n.ftUnload;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    /// EAT (Export Address Table) information.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="module"></param>
    /// <param name="info"></param>
    /// <returns></returns>
    public unsafe EATEntry[] Map_GetEAT(uint pid, string module, out EATInfo info)
    {
        info = new EATInfo();
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_EAT>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_EATENTRY>();
        var m = Array.Empty<EATEntry>();
        if (!Vmmi.VMMDLL_Map_GetEAT(_h, pid, module, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_EAT>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_EAT_VERSION)
        {
            goto fail;
        }

        m = new EATEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_EATENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            EATEntry e;
            e.vaFunction = n.vaFunction;
            e.dwOrdinal = n.dwOrdinal;
            e.oFunctionsArray = n.oFunctionsArray;
            e.oNamesArray = n.oNamesArray;
            e.sFunction = n.uszFunction;
            e.sForwardedFunction = n.uszForwardedFunction;
            m[i] = e;
        }

        info.fValid = true;
        info.vaModuleBase = nM.vaModuleBase;
        info.vaAddressOfFunctions = nM.vaAddressOfFunctions;
        info.vaAddressOfNames = nM.vaAddressOfNames;
        info.cNumberOfFunctions = nM.cNumberOfFunctions;
        info.cNumberOfForwardedFunctions = nM.cNumberOfForwardedFunctions;
        info.cNumberOfNames = nM.cNumberOfNames;
        info.dwOrdinalBase = nM.dwOrdinalBase;
        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    /// IAT (Import Address Table) information.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="module"></param>
    /// <returns></returns>
    public unsafe IATEntry[] Map_GetIAT(uint pid, string module)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_IAT>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_IATENTRY>();
        var m = Array.Empty<IATEntry>();
        if (!Vmmi.VMMDLL_Map_GetIAT(_h, pid, module, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_IAT>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_IAT_VERSION)
        {
            goto fail;
        }

        m = new IATEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_IATENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            IATEntry e;
            e.vaFunction = n.vaFunction;
            e.sFunction = n.uszFunction;
            e.sModule = n.uszModule;
            e.f32 = n.f32;
            e.wHint = n.wHint;
            e.rvaFirstThunk = n.rvaFirstThunk;
            e.rvaOriginalFirstThunk = n.rvaOriginalFirstThunk;
            e.rvaNameModule = n.rvaNameModule;
            e.rvaNameFunction = n.rvaNameFunction;
            e.vaModule = nM.vaModuleBase;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    /// Heap information.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="result">Result if successful.</param>
    /// <returns>TRUE if successful, otherwise FALSE.</returns>
    public unsafe bool Map_GetHeap(uint pid, out HeapMap result)
    {
        bool f = false;
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_HEAP>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_HEAPENTRY>();
        var cbSEGENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_HEAPSEGMENTENTRY>();
        result = default;
        result.heaps = Array.Empty<HeapEntry>();
        result.segments = Array.Empty<HeapSegmentEntry>();
        if (!Vmmi.VMMDLL_Map_GetHeap(_h, pid, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_HEAP>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_HEAP_VERSION)
        {
            goto fail;
        }

        result.heaps = new HeapEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var nH = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_HEAPENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            result.heaps[i].va = nH.va;
            result.heaps[i].f32 = nH.f32;
            result.heaps[i].tpHeap = nH.tp;
            result.heaps[i].iHeapNum = nH.dwHeapNum;
        }

        result.segments = new HeapSegmentEntry[nM.cSegments];
        for (var i = 0; i < nM.cMap; i++)
        {
            var nH = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_HEAPSEGMENTENTRY>((IntPtr)(nM.pSegments.ToInt64() + i * cbSEGENTRY));
            result.segments[i].va = nH.va;
            result.segments[i].cb = nH.cb;
            result.segments[i].tpHeapSegment = nH.tp;
            result.segments[i].iHeapNum = nH.iHeap;
        }
        f = true;
        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return f;
    }

    /// <summary>
    /// Heap allocated entries information.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="vaHeapOrHeapNum"></param>
    /// <returns></returns>
    public unsafe HeapAllocEntry[] Map_GetHeapAlloc(uint pid, ulong vaHeapOrHeapNum)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_HEAPALLOC>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_HEAPALLOCENTRY>();
        if (!Vmmi.VMMDLL_Map_GetHeapAlloc(_h, pid, vaHeapOrHeapNum, out var pHeapAllocMap))
        {
            return Array.Empty<HeapAllocEntry>();
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_HEAPALLOC>(pHeapAllocMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_HEAPALLOC_VERSION)
        {
            Vmmi.VMMDLL_MemFree((byte*)pHeapAllocMap.ToPointer());
            return Array.Empty<HeapAllocEntry>();
        }

        var m = new HeapAllocEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_HEAPALLOCENTRY>((IntPtr)(pHeapAllocMap.ToInt64() + cbMAP + i * cbENTRY));
            m[i].va = n.va;
            m[i].cb = n.cb;
            m[i].tp = n.tp;
        }

        Vmmi.VMMDLL_MemFree((byte*)pHeapAllocMap.ToPointer());
        return m;
    }

    /// <summary>
    /// Thread information.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <returns></returns>
    public unsafe ThreadEntry[] Map_GetThread(uint pid)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_THREAD>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_THREADENTRY>();
        var m = Array.Empty<ThreadEntry>();
        if (!Vmmi.VMMDLL_Map_GetThread(_h, pid, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_THREAD>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_THREAD_VERSION)
        {
            goto fail;
        }

        m = new ThreadEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_THREADENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            ThreadEntry e;
            e.dwTID = n.dwTID;
            e.dwPID = n.dwPID;
            e.dwExitStatus = n.dwExitStatus;
            e.bState = n.bState;
            e.bRunning = n.bRunning;
            e.bPriority = n.bPriority;
            e.bBasePriority = n.bBasePriority;
            e.vaETHREAD = n.vaETHREAD;
            e.vaTeb = n.vaTeb;
            e.ftCreateTime = n.ftCreateTime;
            e.ftExitTime = n.ftExitTime;
            e.vaStartAddress = n.vaStartAddress;
            e.vaWin32StartAddress = n.vaWin32StartAddress;
            e.vaStackBaseUser = n.vaStackBaseUser;
            e.vaStackLimitUser = n.vaStackLimitUser;
            e.vaStackBaseKernel = n.vaStackBaseKernel;
            e.vaStackLimitKernel = n.vaStackLimitKernel;
            e.vaImpersonationToken = n.vaImpersonationToken;
            e.vaTrapFrame = n.vaTrapFrame;
            e.vaRIP = n.vaRIP;
            e.vaRSP = n.vaRSP;
            e.qwAffinity = n.qwAffinity;
            e.dwUserTime = n.dwUserTime;
            e.dwKernelTime = n.dwKernelTime;
            e.bSuspendCount = n.bSuspendCount;
            e.bWaitReason = n.bWaitReason;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    /// Thread callstack information.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="tid">The thread id to retrieve the callstack for.</param>
    /// <param name="flags">Supported flags: 0, FLAG_NOCACHE, FLAG_FORCECACHE_READ</param>
    /// <returns></returns>
    public unsafe ThreadCallstackEntry[] Map_GetThread_Callstack(uint pid, uint tid, VmmFlags flags = VmmFlags.NONE)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_THREAD_CALLSTACK>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_THREAD_CALLSTACKENTRY>();
        var m = Array.Empty<ThreadCallstackEntry>();
        if (!Vmmi.VMMDLL_Map_GetThread_Callstack(_h, pid, tid, flags, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_THREAD_CALLSTACK>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_THREAD_CALLSTACK_VERSION)
        {
            goto fail;
        }

        m = new ThreadCallstackEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_THREAD_CALLSTACKENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            ThreadCallstackEntry e;
            e.dwPID = pid;
            e.dwTID = tid;
            e.i = n.i;
            e.fRegPresent = n.fRegPresent;
            e.vaRetAddr = n.vaRetAddr;
            e.vaRSP = n.vaRSP;
            e.vaBaseSP = n.vaBaseSP;
            e.cbDisplacement = (int)n.cbDisplacement;
            e.sModule = n.uszModule;
            e.sFunction = n.uszFunction;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    /// Handle information.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <returns></returns>
    public unsafe HandleEntry[] Map_GetHandle(uint pid)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_HANDLE>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_HANDLEENTRY>();
        var m = Array.Empty<HandleEntry>();
        if (!Vmmi.VMMDLL_Map_GetHandle(_h, pid, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_HANDLE>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_HANDLE_VERSION)
        {
            goto fail;
        }

        m = new HandleEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_HANDLEENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            HandleEntry e;
            e.vaObject = n.vaObject;
            e.dwHandle = n.dwHandle;
            e.dwGrantedAccess = n.dwGrantedAccess_iType & 0x00ffffff;
            e.iType = n.dwGrantedAccess_iType >> 24;
            e.qwHandleCount = n.qwHandleCount;
            e.qwPointerCount = n.qwPointerCount;
            e.vaObjectCreateInfo = n.vaObjectCreateInfo;
            e.vaSecurityDescriptor = n.vaSecurityDescriptor;
            e.sText = n.uszText;
            e.dwPID = n.dwPID;
            e.dwPoolTag = n.dwPoolTag;
            e.sType = n.uszType;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    /// User mode path of the process image.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <returns></returns>
    public string GetProcessPathUser(uint pid)
    {
        return GetProcessInformationString(pid, VMMDLL_PROCESS_INFORMATION_OPT_STRING_PATH_USER_IMAGE);
    }

    /// <summary>
    /// Kernel mode path of the process image.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <returns></returns>
    public string GetProcessPathKernel(uint pid)
    {
        return GetProcessInformationString(pid, VMMDLL_PROCESS_INFORMATION_OPT_STRING_PATH_KERNEL);
    }

    /// <summary>
    /// Process command line.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <returns></returns>
    public string GetProcessCmdline(uint pid)
    {
        return GetProcessInformationString(pid, VMMDLL_PROCESS_INFORMATION_OPT_STRING_CMDLINE);
    }

    /// <summary>
    /// Get the string representation of an option value.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="fOptionString">VmmProcess.VMMDLL_PROCESS_INFORMATION_OPT_*</param>
    /// <returns></returns>
    public unsafe string GetProcessInformationString(uint pid, uint fOptionString)
    {
        var pb = Vmmi.VMMDLL_ProcessGetInformationString(_h, pid, fOptionString);
        if (pb == null)
        {
            return "";
        }

        var s = Marshal.PtrToStringAnsi((IntPtr)pb);
        Vmmi.VMMDLL_MemFree(pb);
        return s;
    }

    /// <summary>
    /// IMAGE_DATA_DIRECTORY information for the specified module.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="sModule"></param>
    /// <returns></returns>
    public unsafe IMAGE_DATA_DIRECTORY[] ProcessGetDirectories(uint pid, string sModule)
    {
        var PE_DATA_DIRECTORIES = new string[16] { "EXPORT", "IMPORT", "RESOURCE", "EXCEPTION", "SECURITY", "BASERELOC", "DEBUG", "ARCHITECTURE", "GLOBALPTR", "TLS", "LOAD_CONFIG", "BOUND_IMPORT", "IAT", "DELAY_IMPORT", "COM_DESCRIPTOR", "RESERVED" };
        bool result;
        var cbENTRY = (uint)Marshal.SizeOf<Vmmi.VMMDLL_IMAGE_DATA_DIRECTORY>();
        fixed (byte* pb = new byte[16 * cbENTRY])
        {
            result = Vmmi.VMMDLL_ProcessGetDirectories(_h, pid, sModule, pb);
            if (!result)
            {
                return Array.Empty<IMAGE_DATA_DIRECTORY>();
            }

            var m = new IMAGE_DATA_DIRECTORY[16];
            for (var i = 0; i < 16; i++)
            {
                var n = Marshal.PtrToStructure<Vmmi.VMMDLL_IMAGE_DATA_DIRECTORY>((IntPtr)(pb + i * cbENTRY));
                IMAGE_DATA_DIRECTORY e;
                e.name = PE_DATA_DIRECTORIES[i];
                e.VirtualAddress = n.VirtualAddress;
                e.Size = n.Size;
                m[i] = e;
            }

            return m;
        }
    }

    /// <summary>
    /// IMAGE_SECTION_HEADER information for the specified module.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="sModule"></param>
    /// <returns></returns>
    public unsafe IMAGE_SECTION_HEADER[] ProcessGetSections(uint pid, string sModule)
    {
        bool result;
        var cbENTRY = (uint)Marshal.SizeOf<Vmmi.VMMDLL_IMAGE_SECTION_HEADER>();
        result = Vmmi.VMMDLL_ProcessGetSections(_h, pid, sModule, null, 0, out var cData);
        if (!result || cData == 0)
        {
            return Array.Empty<IMAGE_SECTION_HEADER>();
        }

        fixed (byte* pb = new byte[cData * cbENTRY])
        {
            result = Vmmi.VMMDLL_ProcessGetSections(_h, pid, sModule, pb, cData, out cData);
            if (!result || cData == 0)
            {
                return Array.Empty<IMAGE_SECTION_HEADER>();
            }

            var m = new IMAGE_SECTION_HEADER[cData];
            for (var i = 0; i < cData; i++)
            {
                var n = Marshal.PtrToStructure<Vmmi.VMMDLL_IMAGE_SECTION_HEADER>((IntPtr)(pb + i * cbENTRY));
                IMAGE_SECTION_HEADER e;
                e.Name = n.Name;
                e.MiscPhysicalAddressOrVirtualSize = n.MiscPhysicalAddressOrVirtualSize;
                e.VirtualAddress = n.VirtualAddress;
                e.SizeOfRawData = n.SizeOfRawData;
                e.PointerToRawData = n.PointerToRawData;
                e.PointerToRelocations = n.PointerToRelocations;
                e.PointerToLinenumbers = n.PointerToLinenumbers;
                e.NumberOfRelocations = n.NumberOfRelocations;
                e.NumberOfLinenumbers = n.NumberOfLinenumbers;
                e.Characteristics = n.Characteristics;
                m[i] = e;
            }

            return m;
        }
    }

    /// <summary>
    /// Function address of a function in a loaded module.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="wszModuleName"></param>
    /// <param name="szFunctionName"></param>
    /// <returns></returns>
    public ulong ProcessGetProcAddress(uint pid, string wszModuleName, string szFunctionName)
    {
        return Vmmi.VMMDLL_ProcessGetProcAddress(_h, pid, wszModuleName, szFunctionName);
    }

    /// <summary>
    /// Base address of a loaded module.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="wszModuleName"></param>
    /// <returns></returns>
    public ulong ProcessGetModuleBase(uint pid, string wszModuleName)
    {
        return Vmmi.VMMDLL_ProcessGetModuleBase(_h, pid, wszModuleName);
    }

    /// <summary>
    /// Get process information.
    /// </summary>
    /// <param name="pid">Process ID (PID) for this operation.</param>
    /// <param name="result">Result if successful.</param>
    /// <returns>TRUE if successful, otherwise FALSE.</returns>
    public unsafe bool ProcessGetInformation(uint pid, out ProcessInfo result)
    {
        result = default;
        var cbENTRY = (ulong)Marshal.SizeOf<Vmmi.VMMDLL_PROCESS_INFORMATION>();
        fixed (byte* pb = new byte[cbENTRY])
        {
            Marshal.WriteInt64(new IntPtr(pb + 0), unchecked((long)Vmmi.VMMDLL_PROCESS_INFORMATION_MAGIC));
            Marshal.WriteInt16(new IntPtr(pb + 8), unchecked((short)Vmmi.VMMDLL_PROCESS_INFORMATION_VERSION));
            if (!Vmmi.VMMDLL_ProcessGetInformation(_h, pid, pb, ref cbENTRY))
            {
                return false;
            }

            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_PROCESS_INFORMATION>((IntPtr)pb);
            if (n.wVersion != Vmmi.VMMDLL_PROCESS_INFORMATION_VERSION)
            {
                return false;
            }

            result.fValid = true;
            result.tpMemoryModel = n.tpMemoryModel;
            result.tpSystem = n.tpSystem;
            result.fUserOnly = n.fUserOnly;
            result.dwPID = n.dwPID;
            result.dwPPID = n.dwPPID;
            result.dwState = n.dwState;
            result.sName = n.szName;
            result.sNameLong = n.szNameLong;
            result.paDTB = n.paDTB;
            result.paDTB_UserOpt = n.paDTB_UserOpt;
            result.vaEPROCESS = n.vaEPROCESS;
            result.vaPEB = n.vaPEB;
            result.fWow64 = n.fWow64;
            result.vaPEB32 = n.vaPEB32;
            result.dwSessionId = n.dwSessionId;
            result.qwLUID = n.qwLUID;
            result.sSID = n.szSID;
            result.IntegrityLevel = n.IntegrityLevel;

            return true;
        }
    }

    /// <summary>
    /// Get process information for all processes.
    /// </summary>
    /// <returns>ProcessInformation array, Empty Array if failed.</returns>
    public unsafe ProcessInfo[] ProcessGetInformationAll()
    {
        var m = Array.Empty<ProcessInfo>();
        var cbENTRY = (uint)Marshal.SizeOf<Vmmi.VMMDLL_PROCESS_INFORMATION>();
        if (!Vmmi.VMMDLL_ProcessGetInformationAll(_h, out var pMap, out uint pc))
        {
            goto fail;
        }
        m = new ProcessInfo[pc];
        for (var i = 0; i < pc; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_PROCESS_INFORMATION>((IntPtr)(pMap + i * cbENTRY));
            if (i == 0 && n.wVersion != Vmmi.VMMDLL_PROCESS_INFORMATION_VERSION)
            {
                goto fail;
            }

            ProcessInfo e;
            e.fValid = true;
            e.tpMemoryModel = n.tpMemoryModel;
            e.tpSystem = n.tpSystem;
            e.fUserOnly = n.fUserOnly;
            e.dwPID = n.dwPID;
            e.dwPPID = n.dwPPID;
            e.dwState = n.dwState;
            e.sName = n.szName;
            e.sNameLong = n.szNameLong;
            e.paDTB = n.paDTB;
            e.paDTB_UserOpt = n.paDTB_UserOpt;
            e.vaEPROCESS = n.vaEPROCESS;
            e.vaPEB = n.vaPEB;
            e.fWow64 = n.fWow64;
            e.vaPEB32 = n.vaPEB32;
            e.dwSessionId = n.dwSessionId;
            e.qwLUID = n.qwLUID;
            e.sSID = n.szSID;
            e.IntegrityLevel = n.IntegrityLevel;
            m[i] = e;
        }
    fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    public struct ProcessInfo
    {
        public bool fValid;
        public uint tpMemoryModel;
        public uint tpSystem;
        public bool fUserOnly;
        public uint dwPID;
        public uint dwPPID;
        public uint dwState;
        public string sName;
        public string sNameLong;
        public ulong paDTB;
        public ulong paDTB_UserOpt;
        public ulong vaEPROCESS;
        public ulong vaPEB;
        public bool fWow64;
        public uint vaPEB32;
        public uint dwSessionId;
        public ulong qwLUID;
        public string sSID;
        public uint IntegrityLevel;
    }

    public struct PteEntry
    {
        public ulong vaBase;
        public ulong vaEnd;
        public ulong cbSize;
        public ulong cPages;
        public ulong fPage;
        public bool fWoW64;
        public string sText;
        public uint cSoftware;
        public bool fS;
        public bool fR;
        public bool fW;
        public bool fX;
    }

    public struct VadEntry
    {
        public ulong vaStart;
        public ulong vaEnd;
        public ulong vaVad;
        public ulong cbSize;
        public uint VadType;
        public uint Protection;
        public bool fImage;
        public bool fFile;
        public bool fPageFile;
        public bool fPrivateMemory;
        public bool fTeb;
        public bool fStack;
        public uint fSpare;
        public uint HeapNum;
        public bool fHeap;
        public uint cwszDescription;
        public uint CommitCharge;
        public bool MemCommit;
        public uint u2;
        public uint cbPrototypePte;
        public ulong vaPrototypePte;
        public ulong vaSubsection;
        public string sText;
        public ulong vaFileObject;
        public uint cVadExPages;
        public uint cVadExPagesBase;
    }

    public struct VadExEntryPrototype
    {
        public uint tp;
        public ulong pa;
        public ulong pte;
    }

    public struct VadExEntry
    {
        public uint tp;
        public uint iPML;
        public ulong va;
        public ulong pa;
        public ulong pte;
        public uint pteFlags;
        public VadExEntryPrototype proto;
        public ulong vaVadBase;
    }

    public const uint MAP_MODULEENTRY_TP_NORMAL = 0;
    public const uint VMMDLL_MODULE_TP_DATA = 1;
    public const uint VMMDLL_MODULE_TP_NOTLINKED = 2;
    public const uint VMMDLL_MODULE_TP_INJECTED = 3;

    public struct ModuleEntryDebugInfo
    {
        public bool fValid;
        public uint dwAge;
        public string sGuid;
        public string sPdbFilename;
    }

    public struct ModuleEntryVersionInfo
    {
        public bool fValid;
        public string sCompanyName;
        public string sFileDescription;
        public string sFileVersion;
        public string sInternalName;
        public string sLegalCopyright;
        public string sFileOriginalFilename;
        public string sProductName;
        public string sProductVersion;
    }

    public struct ModuleEntry
    {
        public bool fValid;
        public ulong vaBase;
        public ulong vaEntry;
        public uint cbImageSize;
        public bool fWow64;
        public string sText;
        public string sFullName;
        public uint tp;
        public uint cbFileSizeRaw;
        public uint cSection;
        public uint cEAT;
        public uint cIAT;
        public ModuleEntryDebugInfo DebugInfo;
        public ModuleEntryVersionInfo VersionInfo;
    }

    public struct UnloadedModuleEntry
    {
        public ulong vaBase;
        public uint cbImageSize;
        public bool fWow64;
        public string wText;
        public uint dwCheckSum; // user-mode only
        public uint dwTimeDateStamp; // user-mode only
        public ulong ftUnload; // kernel-mode only
    }

    public struct EATInfo
    {
        public bool fValid;
        public ulong vaModuleBase;
        public ulong vaAddressOfFunctions;
        public ulong vaAddressOfNames;
        public uint cNumberOfFunctions;
        public uint cNumberOfForwardedFunctions;
        public uint cNumberOfNames;
        public uint dwOrdinalBase;
    }

    public struct EATEntry
    {
        public ulong vaFunction;
        public uint dwOrdinal;
        public uint oFunctionsArray;
        public uint oNamesArray;
        public string sFunction;
        public string sForwardedFunction;
    }

    public struct IATEntry
    {
        public ulong vaFunction;
        public ulong vaModule;
        public string sFunction;
        public string sModule;
        public bool f32;
        public ushort wHint;
        public uint rvaFirstThunk;
        public uint rvaOriginalFirstThunk;
        public uint rvaNameModule;
        public uint rvaNameFunction;
    }

    public struct HeapEntry
    {
        public ulong va;
        public uint tpHeap;
        public bool f32;
        public uint iHeapNum;
    }

    public struct HeapSegmentEntry
    {
        public ulong va;
        public uint cb;
        public uint tpHeapSegment;
        public uint iHeapNum;
    }

    public struct HeapMap
    {
        public HeapEntry[] heaps;
        public HeapSegmentEntry[] segments;
    }

    public struct HeapAllocEntry
    {
        public ulong va;
        public uint cb;
        public uint tp;
    }

    public struct ThreadEntry
    {
        public uint dwTID;
        public uint dwPID;
        public uint dwExitStatus;
        public byte bState;
        public byte bRunning;
        public byte bPriority;
        public byte bBasePriority;
        public ulong vaETHREAD;
        public ulong vaTeb;
        public ulong ftCreateTime;
        public ulong ftExitTime;
        public ulong vaStartAddress;
        public ulong vaWin32StartAddress;
        public ulong vaStackBaseUser;
        public ulong vaStackLimitUser;
        public ulong vaStackBaseKernel;
        public ulong vaStackLimitKernel;
        public ulong vaTrapFrame;
        public ulong vaImpersonationToken;
        public ulong vaRIP;
        public ulong vaRSP;
        public ulong qwAffinity;
        public uint dwUserTime;
        public uint dwKernelTime;
        public byte bSuspendCount;
        public byte bWaitReason;
    }

    public struct ThreadCallstackEntry
    {
        public uint dwPID;
        public uint dwTID;
        public uint i;
        public bool fRegPresent;
        public ulong vaRetAddr;
        public ulong vaRSP;
        public ulong vaBaseSP;
        public int cbDisplacement;
        public string sModule;
        public string sFunction;
    }

    public struct HandleEntry
    {
        public ulong vaObject;
        public uint dwHandle;
        public uint dwGrantedAccess;
        public uint iType;
        public ulong qwHandleCount;
        public ulong qwPointerCount;
        public ulong vaObjectCreateInfo;
        public ulong vaSecurityDescriptor;
        public string sText;
        public uint dwPID;
        public uint dwPoolTag;
        public string sType;
    }

    public const uint VMMDLL_PROCESS_INFORMATION_OPT_STRING_PATH_KERNEL = 1;
    public const uint VMMDLL_PROCESS_INFORMATION_OPT_STRING_PATH_USER_IMAGE = 2;
    public const uint VMMDLL_PROCESS_INFORMATION_OPT_STRING_CMDLINE = 3;

    /// <summary>
    /// Struct corresponding to the native PE IMAGE_SECTION_HEADER.
    /// </summary>
    public struct IMAGE_SECTION_HEADER
    {
        public string Name;
        public uint MiscPhysicalAddressOrVirtualSize;
        public uint VirtualAddress;
        public uint SizeOfRawData;
        public uint PointerToRawData;
        public uint PointerToRelocations;
        public uint PointerToLinenumbers;
        public ushort NumberOfRelocations;
        public ushort NumberOfLinenumbers;
        public uint Characteristics;
    }

    /// <summary>
    /// Struct corresponding to the native PE IMAGE_DATA_DIRECTORY.
    /// </summary>
    public struct IMAGE_DATA_DIRECTORY
    {
        public string name;
        public uint VirtualAddress;
        public uint Size;
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
    /// List the registry hives.
    /// </summary>
    /// <returns></returns>
    public unsafe RegHiveEntry[] WinReg_HiveList()
    {
        bool result;
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_REGISTRY_HIVE_INFORMATION>();
        result = Vmmi.VMMDLL_WinReg_HiveList(_h, null, 0, out var cHives);
        if (!result || cHives == 0)
        {
            return Array.Empty<RegHiveEntry>();
        }

        fixed (byte* pb = new byte[cHives * cbENTRY])
        {
            result = Vmmi.VMMDLL_WinReg_HiveList(_h, pb, cHives, out cHives);
            if (!result)
            {
                return Array.Empty<RegHiveEntry>();
            }

            var m = new RegHiveEntry[cHives];
            for (var i = 0; i < cHives; i++)
            {
                var n = Marshal.PtrToStructure<Vmmi.VMMDLL_REGISTRY_HIVE_INFORMATION>((IntPtr)(pb + i * cbENTRY));
                RegHiveEntry e;
                if (n.wVersion != Vmmi.VMMDLL_REGISTRY_HIVE_INFORMATION_VERSION)
                {
                    return Array.Empty<RegHiveEntry>();
                }

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
    /// Read from a registry hive.
    /// </summary>
    /// <param name="vaCMHIVE">The virtual address of the registry hive.</param>
    /// <param name="ra">The hive registry address (ra).</param>
    /// <param name="cb"></param>
    /// <param name="flags"></param>
    /// <returns>Read data on success (length may differ from requested read size). Zero-length array on fail.</returns>
    public unsafe byte[] WinReg_HiveReadEx(ulong vaCMHIVE, uint ra, uint cb, VmmFlags flags = VmmFlags.NONE)
    {
        uint cbRead;
        var data = new byte[cb];
        fixed (byte* pb = data)
        {
            if (!Vmmi.VMMDLL_WinReg_HiveReadEx(_h, vaCMHIVE, ra, pb, cb, out cbRead, flags))
            {
                return Array.Empty<byte>();
            }
        }

        if (cbRead != cb)
        {
            Array.Resize(ref data, (int)cbRead);
        }

        return data;
    }

    /// <summary>
    /// Write to a registry hive. NB! This is a very dangerous operation and is not recommended!
    /// </summary>
    /// <param name="vaCMHIVE">>The virtual address of the registry hive.</param>
    /// <param name="ra">The hive registry address (ra).</param>
    /// <param name="data"></param>
    /// <returns></returns>
    public unsafe bool WinReg_HiveWrite(ulong vaCMHIVE, uint ra, byte[] data)
    {
        ThrowIfMemWritesDisabled();
        fixed (byte* pb = data)
        {
            return Vmmi.VMMDLL_WinReg_HiveWrite(_h, vaCMHIVE, ra, pb, (uint)data.Length);
        }
    }

    /// <summary>
    /// Enumerate a registry key for subkeys and values.
    /// </summary>
    /// <param name="sKeyFullPath"></param>
    /// <returns></returns>
    public unsafe RegEnumEntry WinReg_Enum(string sKeyFullPath)
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
    /// Read a registry value.
    /// </summary>
    /// <param name="sValueFullPath"></param>
    /// <param name="tp"></param>
    /// <returns></returns>
    public unsafe byte[] WinReg_QueryValue(string sValueFullPath, out uint tp)
    {
        bool result;
        uint cb = 0;
        result = Vmmi.VMMDLL_WinReg_QueryValueEx(_h, sValueFullPath, out tp, null, ref cb);
        if (!result)
        {
            return null;
        }

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

    public unsafe NetEntry[] Map_GetNet()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_NET>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_NETENTRY>();
        var m = Array.Empty<NetEntry>();
        if (!Vmmi.VMMDLL_Map_GetNet(_h, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_NET>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_NET_VERSION)
        {
            goto fail;
        }

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
    /// Retrieve the physical memory map.
    /// </summary>
    /// <returns>An array of MemoryEntry elements.</returns>
    public unsafe MemoryEntry[] Map_GetPhysMem()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_PHYSMEM>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_PHYSMEMENTRY>();
        var m = Array.Empty<MemoryEntry>();
        if (!Vmmi.VMMDLL_Map_GetPhysMem(_h, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_PHYSMEM>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_PHYSMEM_VERSION)
        {
            goto fail;
        }

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
    /// Retrieve the kernel devices on the system.
    /// </summary>
    /// <returns>An array of KDeviceEntry elements.</returns>
    public unsafe KDeviceEntry[] Map_GetKDevice()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_KDEVICE>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_KDEVICEENTRY>();
        var m = Array.Empty<KDeviceEntry>();
        if (!Vmmi.VMMDLL_Map_GetKDevice(_h, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_KDEVICE>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_KDEVICE_VERSION)
        {
            goto fail;
        }

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
    /// Retrieve the kernel drivers on the system.
    /// </summary>
    /// <returns>An array of KDriverEntry elements.</returns>
    public unsafe KDriverEntry[] Map_GetKDriver()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_KDRIVER>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_KDRIVERENTRY>();
        var m = Array.Empty<KDriverEntry>();
        if (!Vmmi.VMMDLL_Map_GetKDriver(_h, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_KDRIVER>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_KDRIVER_VERSION)
        {
            goto fail;
        }

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
            for (var j = 0; j < 28; j++)
            {
                e.MajorFunction[j] = n.MajorFunction[j];
            }

            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    /// Retrieve the kernel named objects on the system.
    /// </summary>
    /// <returns>An array of KObjectEntry elements.</returns>
    public unsafe KObjectEntry[] Map_GetKObject()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_KOBJECT>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_KOBJECTENTRY>();
        var m = Array.Empty<KObjectEntry>();
        if (!Vmmi.VMMDLL_Map_GetKObject(_h, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_KOBJECT>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_KOBJECT_VERSION)
        {
            goto fail;
        }

        m = new KObjectEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_KOBJECTENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            KObjectEntry e;
            e.va = n.va;
            e.vaParent = n.vaParent;
            e.vaChild = new ulong[n.cvaChild];
            for (var j = 0; j < n.cvaChild; j++)
            {
                e.vaChild[j] = (ulong)Marshal.ReadInt64(n.pvaChild, j * 8);
            }

            e.sName = n.uszName;
            e.sType = n.uszType;
            m[i] = e;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return m;
    }

    /// <summary>
    /// Retrieve entries from the kernel pool.
    /// </summary>
    /// <param name="isBigPoolOnly">
    /// Set to true to only retrieve big pool allocations (= faster). Default is to retrieve all
    /// allocations.
    /// </param>
    /// <returns>An array of PoolEntry elements.</returns>
    public unsafe PoolEntry[] Map_GetPool(bool isBigPoolOnly = false)
    {
        byte[] tag = { 0, 0, 0, 0 };
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_POOL>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_POOLENTRY>();
        var flags = isBigPoolOnly ? VmmPoolMapFlags.BIG : VmmPoolMapFlags.ALL;
        if (!Vmmi.VMMDLL_Map_GetPool(_h, out var pN, flags))
        {
            return Array.Empty<PoolEntry>();
        }

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
    /// Retrieve the detected users on the system.
    /// </summary>
    /// <returns>An array of UserEntry elements.</returns>
    public unsafe UserEntry[] Map_GetUsers()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_USER>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_USERENTRY>();
        var m = Array.Empty<UserEntry>();
        if (!Vmmi.VMMDLL_Map_GetUsers(_h, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_USER>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_USER_VERSION)
        {
            goto fail;
        }

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
    /// Retrieve the detected virtual machines on the system. This includes Hyper-V, WSL and other virtual machines running
    /// on top of the Windows Hypervisor Platform.
    /// </summary>
    /// <returns>An array of VirtualMachineEntry elements.</returns>
    public unsafe VirtualMachineEntry[] Map_GetVM()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_VM>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_VMENTRY>();
        var m = Array.Empty<VirtualMachineEntry>();
        if (!Vmmi.VMMDLL_Map_GetVM(_h, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_VM>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_VM_VERSION)
        {
            goto fail;
        }

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
    /// Retrieve the services on the system.
    /// </summary>
    /// <returns>An array of ServiceEntry elements.</returns>
    public unsafe ServiceEntry[] Map_GetServices()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_SERVICE>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_SERVICEENTRY>();
        var m = Array.Empty<ServiceEntry>();
        if (!Vmmi.VMMDLL_Map_GetServices(_h, out var pMap))
        {
            goto fail;
        }

        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_SERVICE>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_SERVICE_VERSION)
        {
            goto fail;
        }

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
    /// Retrieve the PFN entries for the specified PFNs.
    /// </summary>
    /// <param name="pfns">the pfn numbers of the pfns to retrieve.</param>
    /// <returns></returns>
    public unsafe PfnEntry[] Map_GetPfn(params uint[] pfns)
    {
        bool result;
        uint cbPfns;
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_PFN>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_PFNENTRY>();
        if (pfns.Length == 0)
        {
            return Array.Empty<PfnEntry>();
        }

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
                if (!result)
                {
                    return Array.Empty<PfnEntry>();
                }

                var pm = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_PFN>((IntPtr)pb);
                if (pm.dwVersion != Vmmi.VMMDLL_MAP_PFN_VERSION)
                {
                    return Array.Empty<PfnEntry>();
                }

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

    #region PDB Functionality

    /// <summary>
    /// Load a .pdb symbol file and return its associated module name upon success.
    /// </summary>
    /// <param name="pid"></param>
    /// <param name="vaModuleBase"></param>
    /// <param name="szModuleName"></param>
    /// <returns></returns>
    public unsafe bool PdbLoad(uint pid, ulong vaModuleBase, out string szModuleName)
    {
        szModuleName = "";
        var data = new byte[260];
        fixed (byte* pb = data)
        {
            var result = Vmmi.VMMDLL_PdbLoad(_h, pid, vaModuleBase, pb);
            if (!result)
            {
                return false;
            }

            szModuleName = Encoding.UTF8.GetString(data);
            szModuleName = szModuleName.Substring(0, szModuleName.IndexOf((char)0));
        }

        return true;
    }

    /// <summary>
    /// Get the symbol name given an address or offset.
    /// </summary>
    /// <param name="szModule"></param>
    /// <param name="cbSymbolAddressOrOffset"></param>
    /// <param name="szSymbolName"></param>
    /// <param name="pdwSymbolDisplacement"></param>
    /// <returns></returns>
    public unsafe bool PdbSymbolName(string szModule, ulong cbSymbolAddressOrOffset, out string szSymbolName, out uint pdwSymbolDisplacement)
    {
        szSymbolName = "";
        pdwSymbolDisplacement = 0;
        var data = new byte[260];
        fixed (byte* pb = data)
        {
            var result = Vmmi.VMMDLL_PdbSymbolName(_h, szModule, cbSymbolAddressOrOffset, pb, out pdwSymbolDisplacement);
            if (!result)
            {
                return false;
            }

            szSymbolName = Encoding.UTF8.GetString(data);
            szSymbolName = szSymbolName.Substring(0, szSymbolName.IndexOf((char)0));
        }

        return true;
    }

    /// <summary>
    /// Get the symbol address given a symbol name.
    /// </summary>
    /// <param name="szModule"></param>
    /// <param name="szSymbolName"></param>
    /// <param name="pvaSymbolAddress"></param>
    /// <returns></returns>
    public bool PdbSymbolAddress(string szModule, string szSymbolName, out ulong pvaSymbolAddress)
    {
        return Vmmi.VMMDLL_PdbSymbolAddress(_h, szModule, szSymbolName, out pvaSymbolAddress);
    }

    /// <summary>
    /// Get the size of a type.
    /// </summary>
    /// <param name="szModule"></param>
    /// <param name="szTypeName"></param>
    /// <param name="pcbTypeSize"></param>
    /// <returns></returns>
    public bool PdbTypeSize(string szModule, string szTypeName, out uint pcbTypeSize)
    {
        return Vmmi.VMMDLL_PdbTypeSize(_h, szModule, szTypeName, out pcbTypeSize);
    }

    /// <summary>
    /// Get the child offset of a type.
    /// </summary>
    /// <param name="szModule"></param>
    /// <param name="szTypeName"></param>
    /// <param name="wszTypeChildName"></param>
    /// <param name="pcbTypeChildOffset"></param>
    /// <returns></returns>
    public bool PdbTypeChildOffset(string szModule, string szTypeName, string wszTypeChildName, out uint pcbTypeChildOffset)
    {
        return Vmmi.VMMDLL_PdbTypeChildOffset(_h, szModule, szTypeName, wszTypeChildName, out pcbTypeChildOffset);
    }

    #endregion

    #region Utility functionality

    /// <summary>
    /// Convert a byte array to a hexdump formatted string. (static method).
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
            if (!result)
            {
                return null;
            }

            var dataOut = new byte[cbOut];
            fixed (byte* pbOut = dataOut)
            {
                result = Vmmi.VMMDLL_UtilFillHexAscii(pbIn, cbIn, initialOffset, pbOut, ref cbOut);
                return result ? Encoding.ASCII.GetString(dataOut) : null;
            }
        }
    }


    /// <summary>
    /// Enum used to specify the log level.
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
    /// Log a string to the VMM log.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="logLevel">The log level (default INFO).</param>
    /// <param name="MID">Module ID (default = API).</param>
    public void Log(string message, LogLevel logLevel = LogLevel.Info, uint MID = 0x80000011)
    {
        Vmmi.VMMDLL_Log(_h, MID, (uint)logLevel, "%s", message);
    }

    /// <summary>
    /// Create a VmmSearch object for searching memory.
    /// </summary>
    /// <param name="pid"></param>
    /// <param name="addr_min"></param>
    /// <param name="addr_max"></param>
    /// <param name="cMaxResult"></param>
    /// <param name="readFlags"></param>
    /// <returns></returns>
    public VmmSearch CreateSearch(uint pid, ulong addr_min = 0, ulong addr_max = ulong.MaxValue, uint cMaxResult = 0, uint readFlags = 0)
    {
        return new VmmSearch(this, pid, addr_min, addr_max, cMaxResult, readFlags);
    }

    /// <summary>
    /// Throw an exception if memory writing is disabled.
    /// </summary>
    /// <exception cref="VmmException">Memory writing is disabled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ThrowIfMemWritesDisabled()
    {
        if (!EnableMemoryWriting)
        {
            throw new VmmException("Memory Writing is Disabled! This operation may not proceed.");
        }
    }

    #endregion // Utility functionality

    #region Custom Refresh Functionality

    /// <summary>
    /// Force a 'Full' Vmm Refresh.
    /// </summary>
    public void ForceFullRefresh()
    {
        if (!ConfigSet(VmmOption.REFRESH_ALL, 1))
        {
            Log("WARNING: Vmm Full Refresh Failed!", LogLevel.Warning);
        }
    }

    /// <summary>
    /// Registers an Auto Refresher with a specified interval.
    /// This is potentially useful if you initialized with -norefresh, and want to control refreshing more closely.
    /// Minimum interval resolution ~10-15ms.
    /// </summary>
    /// <param name="option">Vmm Refresh Option</param>
    /// <param name="interval">Interval in which to fire a refresh operation.</param>
    public void RegisterAutoRefresh(RefreshOption option, TimeSpan interval)
    {
        RefreshManager.Register(this, option, interval);
    }

    /// <summary>
    /// Unregisters an Auto Refresher.
    /// </summary>
    /// <param name="option">Option to unregister.</param>
    public void UnregisterAutoRefresh(RefreshOption option)
    {
        RefreshManager.Unregister(this, option);
    }

    #endregion

    #region Vmm Mem Callbacks

    /// <summary>
    /// MEM callback function definition.
    /// </summary>
    /// <param name="ctxUser">user context pointer.</param>
    /// <param name="dwPID">PID of target process, (DWORD)-1 for physical memory.</param>
    /// <param name="cpMEMs">count of pMEMs.</param>
    /// <param name="ppMEMs">array of pointers to MEM scatter read headers.</param>
    public unsafe delegate void VmmMemCallbackFn(
        IntPtr ctxUser,
        uint dwPID,
        uint cpMEMs,
        LeechCore.LcMemScatter** ppMEMs
    );

    /// <summary>
    /// Register a memory callback function.
    /// Can only have one callback of each type registered at a time.
    /// </summary>
    /// <param name="type">Callback type</param>
    /// <param name="callback">Callback delegate.</param>
    /// <param name="context">User context pointer to be passed to the callback function.</param>
    /// <returns><see cref="VmmMemCallback"/> instance. Dispose of it when you would like to unregister the callback.</returns>
    public VmmMemCallback CreateMemCallback(VmmMemCallbackType type, VmmMemCallbackFn callback, IntPtr context) =>
        new VmmMemCallback(this, type, callback, context);

    #endregion
}