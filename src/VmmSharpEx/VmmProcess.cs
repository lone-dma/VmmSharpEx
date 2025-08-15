using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using VmmSharpEx.Internal;

namespace VmmSharpEx;

/// <summary>
/// VmmProcess represents a process in the system.
/// </summary>
public sealed class VmmProcess
{
    #region Base Functionality

    public static implicit operator uint(VmmProcess x) => x.PID;

    private readonly Vmm _vmm;

    /// <summary>
    /// Process ID for this Process.
    /// </summary>
    public uint PID { get; }

    private VmmProcess()
    {
    }

    /// <summary>
    /// Create a new VmmProcess object from a PID.
    /// WARNING: No validation is performed to ensure the process exists.
    /// </summary>
    /// <param name="vmm">Vmm instance.</param>
    /// <param name="pid">Process ID to wrap.</param>
    internal VmmProcess(Vmm vmm, uint pid)
    {
        ArgumentNullException.ThrowIfNull(vmm, nameof(vmm));
        PID = pid;
        _vmm = vmm;
    }

    /// <summary>
    /// ToString() override.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return "VmmProcess:" + PID;
    }

    #endregion

    #region Memory Read/Write

    /// <summary>
    /// Perform a scatter read of multiple page-sized virtual memory ranges.
    /// Does not copy the read memory to a managed byte buffer, but instead allows direct access to the native memory via a
    /// Span view.
    /// </summary>
    /// <param name="flags">Vmm Flags.</param>
    /// <param name="va">Array of page-aligned Memory Addresses.</param>
    /// <returns>SCATTER_HANDLE</returns>
    /// <exception cref="VmmException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe LeechCore.SCATTER_HANDLE MemReadScatter(uint flags, params ulong[] va) =>
        _vmm.MemReadScatter(PID, flags, va);

    /// <summary>
    /// Initialize a Scatter Memory Read handle used to read multiple virtual memory regions in a single call.
    /// </summary>
    /// <param name="flags">Vmm Flags.</param>
    /// <returns>A VmmScatterMemory handle.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VmmScatter CreateScatter(uint flags = 0) =>
        _vmm.CreateScatter(PID, flags);

    /// <summary>
    /// Read Memory from a Virtual Address into a managed byte-array.
    /// WARNING: This incurs a heap allocation for the array. Recommend using MemReadSpan instead.
    /// </summary>
    /// <param name="va">Virtual Address to read from.</param>
    /// <param name="cb">Count of bytes to read.</param>
    /// <param name="flags">VMM Flags.</param>
    /// <returns>Managed byte array containing number of bytes read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] MemRead(ulong va, uint cb, uint flags = 0) =>
        _vmm.MemRead(PID, va, cb, flags);

    /// <summary>
    /// Read Memory from a Virtual Address into unmanaged memory.
    /// </summary>
    /// <param name="va">Virtual Address to read from.</param>
    /// <param name="pb">Pointer to buffer to receive read.</param>
    /// <param name="cb">Count of bytes to read.</param>
    /// <param name="cbRead">Count of bytes successfully read.</param>
    /// <param name="flags">VMM Flags.</param>
    /// <returns>True if successful, otherwise False. Be sure to check cbRead count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool MemRead(ulong va, IntPtr pb, uint cb, out uint cbRead, uint flags = 0) =>
        _vmm.MemRead(PID, va, pb, cb, out cbRead, flags);

    /// <summary>
    /// Read Memory from a Virtual Address into unmanaged memory.
    /// </summary>
    /// <param name="va">Virtual Address to read from.</param>
    /// <param name="pb">Pointer to buffer to receive read.</param>
    /// <param name="cb">Count of bytes to read.</param>
    /// <param name="cbRead">Count of bytes successfully read.</param>
    /// <param name="flags">VMM Flags.</param>
    /// <returns>True if successful, otherwise False. Be sure to check cbRead count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool MemRead(ulong va, void* pb, uint cb, out uint cbRead, uint flags = 0) =>
        _vmm.MemRead(PID, va, pb, cb, out cbRead, flags);

    /// <summary>
    /// Read Memory from a Virtual Address into a ref struct of Type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">Struct/Ref Struct Type.</typeparam>
    /// <param name="va">Virtual Address to read from.</param>
    /// <param name="result">Memory read result.</param>
    /// <param name="flags">VMM Flags.</param>
    /// <returns>TRUE if successful, otherwise FALSE.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool MemReadValue<T>(ulong va, out T result, uint flags = 0)
        where T : unmanaged, allows ref struct =>
        _vmm.MemReadValue(PID, va, out result, flags);

    /// <summary>
    /// Read Memory from a Virtual Address into an Array of Type <typeparamref name="T" />.
    /// WARNING: This incurs a heap allocation for the array. Recommend using MemReadSpan instead.
    /// </summary>
    /// <typeparam name="T">Value Type.</typeparam>
    /// <param name="va">Virtual Address to read from.</param>
    /// <param name="count">Number of elements to read.</param>
    /// <param name="flags">VMM Flags.</param>
    /// <returns>Managed <typeparamref name="T" /> array containing number of elements read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe T[] MemReadArray<T>(ulong va, uint count, uint flags = 0)
        where T : unmanaged =>
        _vmm.MemReadArray<T>(PID, va, count, flags);

    /// <summary>
    /// Read memory into a Span of <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">Value Type</typeparam>
    /// <param name="va">Memory address to read from.</param>
    /// <param name="span">Span to receive the memory read.</param>
    /// <param name="cbRead">Number of bytes successfully read.</param>
    /// <param name="flags">Read flags.</param>
    /// <returns>
    /// True if successful, otherwise False.
    /// Please be sure to also check the cbRead out value.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool MemReadSpan<T>(ulong va, Span<T> span, out uint cbRead, uint flags)
        where T : unmanaged =>
        _vmm.MemReadSpan(PID, va, span, out cbRead, flags);

    /// <summary>
    /// Read Memory from a Virtual Address into a Managed String.
    /// </summary>
    /// <param name="encoding">String Encoding for this read.</param>
    /// <param name="va">Virtual Address to read from.</param>
    /// <param name="cb">Number of bytes to read. Keep in mind some string encodings are 2-4 bytes per character.</param>
    /// <param name="flags">VMM Flags.</param>
    /// <param name="terminateOnNullChar">Terminate the string at the first occurrence of the null character.</param>
    /// <returns>C# Managed System.String. Null if failed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe string MemReadString(Encoding encoding, ulong va, uint cb,
        uint flags = 0, bool terminateOnNullChar = true) =>
        _vmm.MemReadString(encoding, PID, va, cb, flags, terminateOnNullChar);

    /// <summary>
    /// Prefetch pages into the MemProcFS internal cache.
    /// </summary>
    /// <param name="va">An array of the virtual addresses to prefetch.</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool MemPrefetchPages(ulong[] va) =>
        _vmm.MemPrefetchPages(PID, va);

    /// <summary>
    /// Write Memory from a managed byte-array to a given Virtual Address.
    /// </summary>
    /// <param name="va">Virtual Address to write to.</param>
    /// <param name="data">Data to be written.</param>
    /// <returns>True if write successful, otherwise False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MemWrite(ulong va, byte[] data) =>
        _vmm.MemWrite(PID, va, data);

    /// <summary>
    /// Write Memory from unmanaged memory to a given Virtual Address.
    /// </summary>
    /// <param name="va">Virtual Address to write to.</param>
    /// <param name="pb">Pointer to buffer to write from.</param>
    /// <param name="cb">Count of bytes to write.</param>
    /// <returns>True if write successful, otherwise False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool MemWrite(ulong va, IntPtr pb, uint cb) =>
        _vmm.MemWrite(PID, va, pb, cb);

    /// <summary>
    /// Write Memory from unmanaged memory to a given Virtual Address.
    /// </summary>
    /// <param name="va">Virtual Address to write to.</param>
    /// <param name="pb">Pointer to buffer to write from.</param>
    /// <param name="cb">Count of bytes to write.</param>
    /// <returns>True if write successful, otherwise False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool MemWrite(ulong va, void* pb, uint cb) =>
        _vmm.MemWrite(PID, va, pb, cb);

    /// <summary>
    /// Write Memory from a struct value <typeparamref name="T" /> to a given Virtual Address.
    /// </summary>
    /// <typeparam name="T">Value Type.</typeparam>
    /// <param name="va">Virtual Address to write to.</param>
    /// <param name="value"><typeparamref name="T" /> Value to write.</param>
    /// <returns>True if write successful, otherwise False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool MemWriteValue<T>(ulong va, T value)
        where T : unmanaged, allows ref struct =>
        _vmm.MemWriteValue(PID, va, value);

    /// <summary>
    /// Write Memory from a managed <typeparamref name="T" /> Array to a given Virtual Address.
    /// </summary>
    /// <typeparam name="T">Value Type.</typeparam>
    /// <param name="va">Virtual Address to write to.</param>
    /// <param name="data">Managed <typeparamref name="T" /> array to write.</param>
    /// <returns>True if write successful, otherwise False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool MemWriteArray<T>(ulong va, T[] data)
        where T : unmanaged =>
        _vmm.MemWriteArray(PID, va, data);

    /// <summary>
    /// Write memory from a Span of <typeparamref name="T" /> to a specified memory address.
    /// </summary>
    /// <typeparam name="T">Value Type</typeparam>
    /// <param name="va">Memory address to write to.</param>
    /// <param name="span">Span to write from.</param>
    /// <returns>True if successful, otherwise False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool MemWriteSpan<T>(ulong va, Span<T> span)
        where T : unmanaged =>
        _vmm.MemWriteSpan(PID, va, span);

    /// <summary>
    /// Translate a virtual address to a physical address.
    /// </summary>
    /// <param name="va">Virtual address to translate from.</param>
    /// <returns>Physical address if successful, zero on fail.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong MemVirt2Phys(ulong va) =>
        _vmm.MemVirt2Phys(PID, va);

    #endregion

    #region Process Functionality

    /// <summary>
    /// PTE (Page Table Entry) information.
    /// </summary>
    /// <param name="fIdentifyModules"></param>
    /// <returns>Array of PTEs on success. Zero-length array on fail.</returns>
    public unsafe PteEntry[] MapPTE(bool fIdentifyModules = true)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_PTE>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_PTEENTRY>();
        var m = Array.Empty<PteEntry>();
        if (!Vmmi.VMMDLL_Map_GetPte(_vmm, PID, fIdentifyModules, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_PTE>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_PTE_VERSION) goto fail;
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
    /// <param name="fIdentifyModules"></param>
    /// <returns></returns>
    public unsafe VadEntry[] MapVAD(bool fIdentifyModules = true)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_VAD>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_VADENTRY>();
        var m = Array.Empty<VadEntry>();
        if (!Vmmi.VMMDLL_Map_GetVad(_vmm, PID, fIdentifyModules, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_VAD>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_VAD_VERSION) goto fail;
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
    /// <param name="oPages"></param>
    /// <param name="cPages"></param>
    /// <returns></returns>
    public unsafe VadExEntry[] MapVADEx(uint oPages, uint cPages)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_VADEX>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_VADEXENTRY>();
        var m = Array.Empty<VadExEntry>();
        if (!Vmmi.VMMDLL_Map_GetVadEx(_vmm, PID, oPages, cPages, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_VADEX>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_VADEX_VERSION) goto fail;
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
    /// <param name="fExtendedInfo"></param>
    /// <returns></returns>
    public unsafe ModuleEntry[] MapModule(bool fExtendedInfo = false)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_MODULE>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_MODULEENTRY>();
        var m = Array.Empty<ModuleEntry>();
        var flags = fExtendedInfo ? (uint)0xff : 0;
        if (!Vmmi.VMMDLL_Map_GetModule(_vmm, PID, out var pMap, flags)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_MODULE>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_MODULE_VERSION) goto fail;
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
    /// <param name="module"></param>
    /// <returns></returns>
    public unsafe ModuleEntry MapModuleFromName(string module)
    {
        var e = new ModuleEntry();
        if (!Vmmi.VMMDLL_Map_GetModuleFromName(_vmm, PID, module, out var pMap, 0)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_MODULEENTRY>(pMap);
        e.fValid = true;
        e.vaBase = nM.vaBase;
        e.vaEntry = nM.vaEntry;
        e.cbImageSize = nM.cbImageSize;
        e.fWow64 = nM.fWow64;
        e.sText = module;
        e.sFullName = nM.uszFullName;
        e.tp = nM.tp;
        e.cbFileSizeRaw = nM.cbFileSizeRaw;
        e.cSection = nM.cSection;
        e.cEAT = nM.cEAT;
        e.cIAT = nM.cIAT;
        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return e;
    }

    /// <summary>
    /// Unloaded module information.
    /// </summary>
    /// <returns></returns>
    public unsafe UnloadedModuleEntry[] MapUnloadedModule()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_UNLOADEDMODULE>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_UNLOADEDMODULEENTRY>();
        var m = Array.Empty<UnloadedModuleEntry>();
        if (!Vmmi.VMMDLL_Map_GetUnloadedModule(_vmm, PID, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_UNLOADEDMODULE>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_UNLOADEDMODULE_VERSION) goto fail;
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
    /// <param name="module"></param>
    /// <returns></returns>
    public EATEntry[] MapModuleEAT(string module)
    {
        return MapModuleEAT(module, out _);
    }

    /// <summary>
    /// EAT (Export Address Table) information.
    /// </summary>
    /// <param name="module"></param>
    /// <param name="info"></param>
    /// <returns></returns>
    public unsafe EATEntry[] MapModuleEAT(string module, out EATInfo info)
    {
        info = new EATInfo();
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_EAT>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_EATENTRY>();
        var m = Array.Empty<EATEntry>();
        if (!Vmmi.VMMDLL_Map_GetEAT(_vmm, PID, module, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_EAT>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_EAT_VERSION) goto fail;
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
    /// <param name="module"></param>
    /// <returns></returns>
    public unsafe IATEntry[] MapModuleIAT(string module)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_IAT>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_IATENTRY>();
        var m = Array.Empty<IATEntry>();
        if (!Vmmi.VMMDLL_Map_GetIAT(_vmm, PID, module, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_IAT>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_IAT_VERSION) goto fail;
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
    /// <returns></returns>
    public unsafe HeapMap MapHeap()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_HEAP>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_HEAPENTRY>();
        var cbSEGENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_HEAPSEGMENTENTRY>();
        HeapMap Heap;
        Heap.heaps = Array.Empty<HeapEntry>();
        Heap.segments = Array.Empty<HeapSegmentEntry>();
        if (!Vmmi.VMMDLL_Map_GetHeap(_vmm, PID, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_HEAP>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_HEAP_VERSION) goto fail;
        Heap.heaps = new HeapEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var nH = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_HEAPENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            Heap.heaps[i].va = nH.va;
            Heap.heaps[i].f32 = nH.f32;
            Heap.heaps[i].tpHeap = nH.tp;
            Heap.heaps[i].iHeapNum = nH.dwHeapNum;
        }

        Heap.segments = new HeapSegmentEntry[nM.cSegments];
        for (var i = 0; i < nM.cMap; i++)
        {
            var nH = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_HEAPSEGMENTENTRY>((IntPtr)(nM.pSegments.ToInt64() + i * cbSEGENTRY));
            Heap.segments[i].va = nH.va;
            Heap.segments[i].cb = nH.cb;
            Heap.segments[i].tpHeapSegment = nH.tp;
            Heap.segments[i].iHeapNum = nH.iHeap;
        }

        fail:
        Vmmi.VMMDLL_MemFree((byte*)pMap.ToPointer());
        return Heap;
    }

    /// <summary>
    /// Heap allocated entries information.
    /// </summary>
    /// <param name="vaHeapOrHeapNum"></param>
    /// <returns></returns>
    public unsafe HeapAllocEntry[] MapHeapAlloc(ulong vaHeapOrHeapNum)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_HEAPALLOC>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_HEAPALLOCENTRY>();
        if (!Vmmi.VMMDLL_Map_GetHeapAlloc(_vmm, PID, vaHeapOrHeapNum, out var pHeapAllocMap)) return Array.Empty<HeapAllocEntry>();
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
    /// <returns></returns>
    public unsafe ThreadEntry[] MapThread()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_THREAD>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_THREADENTRY>();
        var m = Array.Empty<ThreadEntry>();
        if (!Vmmi.VMMDLL_Map_GetThread(_vmm, PID, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_THREAD>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_THREAD_VERSION) goto fail;
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
    /// <param name="tid">The thread id to retrieve the callstack for.</param>
    /// <param name="flags">Supported flags: 0, FLAG_NOCACHE, FLAG_FORCECACHE_READ</param>
    /// <returns></returns>
    public unsafe ThreadCallstackEntry[] MapThreadCallstack(uint tid, uint flags = 0)
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_THREAD_CALLSTACK>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_THREAD_CALLSTACKENTRY>();
        var m = Array.Empty<ThreadCallstackEntry>();
        if (!Vmmi.VMMDLL_Map_GetThread_Callstack(_vmm, PID, tid, flags, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_THREAD_CALLSTACK>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_THREAD_CALLSTACK_VERSION) goto fail;
        m = new ThreadCallstackEntry[nM.cMap];
        for (var i = 0; i < nM.cMap; i++)
        {
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_THREAD_CALLSTACKENTRY>((IntPtr)(pMap.ToInt64() + cbMAP + i * cbENTRY));
            ThreadCallstackEntry e;
            e.dwPID = PID;
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
    /// <returns></returns>
    public unsafe HandleEntry[] MapHandle()
    {
        var cbMAP = Marshal.SizeOf<Vmmi.VMMDLL_MAP_HANDLE>();
        var cbENTRY = Marshal.SizeOf<Vmmi.VMMDLL_MAP_HANDLEENTRY>();
        var m = Array.Empty<HandleEntry>();
        if (!Vmmi.VMMDLL_Map_GetHandle(_vmm, PID, out var pMap)) goto fail;
        var nM = Marshal.PtrToStructure<Vmmi.VMMDLL_MAP_HANDLE>(pMap);
        if (nM.dwVersion != Vmmi.VMMDLL_MAP_HANDLE_VERSION) goto fail;
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
    /// <returns></returns>
    public string GetPathUser()
    {
        return GetInformationString(VMMDLL_PROCESS_INFORMATION_OPT_STRING_PATH_USER_IMAGE);
    }

    /// <summary>
    /// Kernel mode path of the process image.
    /// </summary>
    /// <returns></returns>
    public string GetPathKernel()
    {
        return GetInformationString(VMMDLL_PROCESS_INFORMATION_OPT_STRING_PATH_KERNEL);
    }

    /// <summary>
    /// Process command line.
    /// </summary>
    /// <returns></returns>
    public string GetCmdline()
    {
        return GetInformationString(VMMDLL_PROCESS_INFORMATION_OPT_STRING_CMDLINE);
    }

    /// <summary>
    /// Get the string representation of an option value.
    /// </summary>
    /// <param name="fOptionString">VmmProcess.VMMDLL_PROCESS_INFORMATION_OPT_*</param>
    /// <returns></returns>
    public unsafe string GetInformationString(uint fOptionString)
    {
        var pb = Vmmi.VMMDLL_ProcessGetInformationString(_vmm, PID, fOptionString);
        if (pb == null) return "";
        var s = Marshal.PtrToStringAnsi((IntPtr)pb);
        Vmmi.VMMDLL_MemFree(pb);
        return s;
    }

    /// <summary>
    /// IMAGE_DATA_DIRECTORY information for the specified module.
    /// </summary>
    /// <param name="sModule"></param>
    /// <returns></returns>
    public unsafe IMAGE_DATA_DIRECTORY[] MapModuleDataDirectory(string sModule)
    {
        var PE_DATA_DIRECTORIES = new string[16] { "EXPORT", "IMPORT", "RESOURCE", "EXCEPTION", "SECURITY", "BASERELOC", "DEBUG", "ARCHITECTURE", "GLOBALPTR", "TLS", "LOAD_CONFIG", "BOUND_IMPORT", "IAT", "DELAY_IMPORT", "COM_DESCRIPTOR", "RESERVED" };
        bool result;
        var cbENTRY = (uint)Marshal.SizeOf<Vmmi.VMMDLL_IMAGE_DATA_DIRECTORY>();
        fixed (byte* pb = new byte[16 * cbENTRY])
        {
            result = Vmmi.VMMDLL_ProcessGetDirectories(_vmm, PID, sModule, pb);
            if (!result) return Array.Empty<IMAGE_DATA_DIRECTORY>();
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
    /// <param name="sModule"></param>
    /// <returns></returns>
    public unsafe IMAGE_SECTION_HEADER[] MapModuleSection(string sModule)
    {
        bool result;
        var cbENTRY = (uint)Marshal.SizeOf<Vmmi.VMMDLL_IMAGE_SECTION_HEADER>();
        result = Vmmi.VMMDLL_ProcessGetSections(_vmm, PID, sModule, null, 0, out var cData);
        if (!result || cData == 0) return Array.Empty<IMAGE_SECTION_HEADER>();
        fixed (byte* pb = new byte[cData * cbENTRY])
        {
            result = Vmmi.VMMDLL_ProcessGetSections(_vmm, PID, sModule, pb, cData, out cData);
            if (!result || cData == 0) return Array.Empty<IMAGE_SECTION_HEADER>();
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
    /// <param name="wszModuleName"></param>
    /// <param name="szFunctionName"></param>
    /// <returns></returns>
    public ulong GetProcAddress(string wszModuleName, string szFunctionName)
    {
        return Vmmi.VMMDLL_ProcessGetProcAddress(_vmm, PID, wszModuleName, szFunctionName);
    }

    /// <summary>
    /// Base address of a loaded module.
    /// </summary>
    /// <param name="wszModuleName"></param>
    /// <returns></returns>
    public ulong GetModuleBase(string wszModuleName)
    {
        return Vmmi.VMMDLL_ProcessGetModuleBase(_vmm, PID, wszModuleName);
    }

    /// <summary>
    /// Get process information.
    /// </summary>
    /// <returns>ProcessInformation on success. NULL on fail.</returns>
    public unsafe ProcessInfo? GetInfo()
    {
        var cbENTRY = (ulong)Marshal.SizeOf<Vmmi.VMMDLL_PROCESS_INFORMATION>();
        fixed (byte* pb = new byte[cbENTRY])
        {
            Marshal.WriteInt64(new IntPtr(pb + 0), unchecked((long)Vmmi.VMMDLL_PROCESS_INFORMATION_MAGIC));
            Marshal.WriteInt16(new IntPtr(pb + 8), unchecked((short)Vmmi.VMMDLL_PROCESS_INFORMATION_VERSION));
            if (!Vmmi.VMMDLL_ProcessGetInformation(_vmm, PID, pb, ref cbENTRY)) return null;
            var n = Marshal.PtrToStructure<Vmmi.VMMDLL_PROCESS_INFORMATION>((IntPtr)pb);
            if (n.wVersion != Vmmi.VMMDLL_PROCESS_INFORMATION_VERSION) return null;
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
            return e;
        }
    }

    /// <summary>
    /// Retrieve the PDB given a module base address.
    /// </summary>
    /// <param name="vaModuleBase"></param>
    /// <returns></returns>
    public VmmPdb CreatePdb(ulong vaModuleBase) =>
        new VmmPdb(_vmm, PID, vaModuleBase);

    /// <summary>
    /// Retrieve the PDB given a module name.
    /// </summary>
    /// <param name="sModule"></param>
    /// <returns></returns>
    public VmmPdb CreatePdb(string sModule)
    {
        var eModule = MapModuleFromName(sModule);
        if (!eModule.fValid) 
            throw new VmmException("Module not found.");
        return CreatePdb(eModule.vaBase);
    }

    /// <summary>
    /// Create a VmmSearch object for searching memory.
    /// </summary>
    /// <param name="addr_min"></param>
    /// <param name="addr_max"></param>
    /// <param name="cMaxResult"></param>
    /// <param name="readFlags"></param>
    /// <returns></returns>
    public VmmSearch CreateSearch(ulong addr_min = 0, ulong addr_max = ulong.MaxValue, uint cMaxResult = 0, uint readFlags = 0)
        => new VmmSearch(_vmm, this.PID, addr_min, addr_max, cMaxResult, readFlags);

    #endregion

    #region Types

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
}