/*  
*  C# API wrapper 'vmmsharp' for MemProcFS 'vmm.dll' and LeechCore 'leechcore.dll' APIs.
*  
*  Please see the example project in vmmsharp_example for additional information.
*  
*  Please consult the C/C++ header files vmmdll.h and leechcore.h for information about parameters and API usage.
*  
*  (c) Ulf Frisk, 2020-2025
*  Author: Ulf Frisk, pcileech@frizk.net
*  
*/

/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using System.Runtime.InteropServices;
using VmmSharpEx.Options;

namespace VmmSharpEx.Internal;

internal static partial class Vmmi
{
    #region Types/Constants

    public const string VMMDLL_VfsListX = "VMMDLL_VfsListU";
    public const string VMMDLL_VfsReadX = "VMMDLL_VfsReadU";
    public const string VMMDLL_VfsWriteX = "VMMDLL_VfsWriteU";
    public const string VMMDLL_ProcessGetProcAddressX = "VMMDLL_ProcessGetProcAddressU";
    public const string VMMDLL_ProcessGetModuleBaseX = "VMMDLL_ProcessGetModuleBaseU";
    public const string VMMDLL_ProcessGetDirectoriesX = "VMMDLL_ProcessGetDirectoriesU";
    public const string VMMDLL_ProcessGetSectionsX = "VMMDLL_ProcessGetSectionsU";
    public const string VMMDLL_Map_GetPteX = "VMMDLL_Map_GetPteU";
    public const string VMMDLL_Map_GetVadX = "VMMDLL_Map_GetVadU";
    public const string VMMDLL_Map_GetModuleX = "VMMDLL_Map_GetModuleU";
    public const string VMMDLL_Map_GetModuleFromNameX = "VMMDLL_Map_GetModuleFromNameU";
    public const string VMMDLL_Map_GetUnloadedModuleX = "VMMDLL_Map_GetUnloadedModuleU";
    public const string VMMDLL_Map_GetEATX = "VMMDLL_Map_GetEATU";
    public const string VMMDLL_Map_GetIATX = "VMMDLL_Map_GetIATU";
    public const string VMMDLL_Map_GetHandleX = "VMMDLL_Map_GetHandleU";
    public const string VMMDLL_Map_GetNetX = "VMMDLL_Map_GetNetU";
    public const string VMMDLL_Map_GetKDeviceX = "VMMDLL_Map_GetKDeviceU";
    public const string VMMDLL_Map_GetKDriverX = "VMMDLL_Map_GetKDriverU";
    public const string VMMDLL_Map_GetKObjectX = "VMMDLL_Map_GetKObjectU";
    public const string VMMDLL_Map_GetThread_CallstackX = "VMMDLL_Map_GetThread_CallstackU";
    public const string VMMDLL_Map_GetUsersX = "VMMDLL_Map_GetUsersU";
    public const string VMMDLL_Map_GetVMX = "VMMDLL_Map_GetVMU";
    public const string VMMDLL_Map_GetServicesX = "VMMDLL_Map_GetServicesU";
    public const string VMMDLL_WinReg_EnumKeyExX = "VMMDLL_WinReg_EnumKeyExU";
    public const string VMMDLL_WinReg_EnumValueX = "VMMDLL_WinReg_EnumValueU";
    public const string VMMDLL_WinReg_QueryValueExX = "VMMDLL_WinReg_QueryValueExU";

    public const ulong MAX_PATH = 260;
    public const uint VMMDLL_MAP_PTE_VERSION = 2;
    public const uint VMMDLL_MAP_VAD_VERSION = 6;
    public const uint VMMDLL_MAP_VADEX_VERSION = 4;
    public const uint VMMDLL_MAP_MODULE_VERSION = 6;
    public const uint VMMDLL_MAP_UNLOADEDMODULE_VERSION = 2;
    public const uint VMMDLL_MAP_EAT_VERSION = 3;
    public const uint VMMDLL_MAP_IAT_VERSION = 2;
    public const uint VMMDLL_MAP_HEAP_VERSION = 4;
    public const uint VMMDLL_MAP_HEAPALLOC_VERSION = 1;
    public const uint VMMDLL_MAP_THREAD_VERSION = 4;
    public const uint VMMDLL_MAP_THREAD_CALLSTACK_VERSION = 1;
    public const uint VMMDLL_MAP_HANDLE_VERSION = 3;
    public const uint VMMDLL_MAP_NET_VERSION = 3;
    public const uint VMMDLL_MAP_PHYSMEM_VERSION = 2;
    public const uint VMMDLL_MAP_KDEVICE_VERSION = 1;
    public const uint VMMDLL_MAP_KDRIVER_VERSION = 1;
    public const uint VMMDLL_MAP_KOBJECT_VERSION = 1;
    public const uint VMMDLL_MAP_POOL_VERSION = 2;
    public const uint VMMDLL_MAP_USER_VERSION = 2;
    public const uint VMMDLL_MAP_VM_VERSION = 2;
    public const uint VMMDLL_MAP_PFN_VERSION = 1;
    public const uint VMMDLL_MAP_SERVICE_VERSION = 3;
    public const uint VMMDLL_MEM_SEARCH_VERSION = 0xfe3e0003;
    public const uint VMMDLL_REGISTRY_HIVE_INFORMATION_VERSION = 4;

    public const uint VMMDLL_VFS_FILELIST_EXINFO_VERSION = 1;
    public const uint VMMDLL_VFS_FILELIST_VERSION = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_VFS_FILELIST
    {
        public uint dwVersion;
        public uint _Reserved;
        public IntPtr pfnAddFile;
        public IntPtr pfnAddDirectory;
        public ulong h;
    }

    public const ulong VMMDLL_PROCESS_INFORMATION_MAGIC = 0xc0ffee663df9301e;
    public const ushort VMMDLL_PROCESS_INFORMATION_VERSION = 7;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct VMMDLL_PROCESS_INFORMATION
    {
        public ulong magic;
        public ushort wVersion;
        public ushort wSize;
        public uint tpMemoryModel;
        public uint tpSystem;
        public bool fUserOnly;
        public uint dwPID;
        public uint dwPPID;
        public uint dwState;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string szName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szNameLong;

        public ulong paDTB;
        public ulong paDTB_UserOpt;
        public ulong vaEPROCESS;
        public ulong vaPEB;
        public ulong _Reserved1;
        public bool fWow64;
        public uint vaPEB32;
        public uint dwSessionId;
        public ulong qwLUID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szSID;

        public uint IntegrityLevel;
    }

    public struct VMMDLL_MAP_MODULE
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] _Reserved1;

        public ulong pbMultiText;
        public uint cbMultiText;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_PTEENTRY
    {
        public ulong vaBase;
        public ulong cPages;
        public ulong fPage;
        public bool fWoW64;
        public uint _FutureUse1;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszText;
        public uint _Reserved1;
        public uint cSoftware;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_PTE
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] _Reserved1;

        public ulong pbMultiText;
        public uint cbMultiText;
        public uint cMap;
    }


    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct VMMDLL_IMAGE_SECTION_HEADER
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
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

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_IMAGE_DATA_DIRECTORY
    {
        public uint VirtualAddress;
        public uint Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_VADENTRY
    {
        public ulong vaStart;
        public ulong vaEnd;
        public ulong vaVad;
        public uint dw0;
        public uint dw1;
        public uint u2;
        public uint cbPrototypePte;
        public ulong vaPrototypePte;
        public ulong vaSubsection;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszText;
        public uint _FutureUse1;
        public uint _Reserved1;
        public ulong vaFileObject;
        public uint cVadExPages;
        public uint cVadExPagesBase;
        public ulong _Reserved2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_VAD
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] _Reserved1;

        public uint cPage;
        public ulong pbMultiText;
        public uint cbMultiText;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_VADEXENTRY
    {
        public uint tp;
        public byte iPML;
        public byte pteFlags;
        public ushort _Reserved2;
        public ulong va;
        public ulong pa;
        public ulong pte;
        public uint _Reserved1;
        public uint proto_tp;
        public ulong proto_pa;
        public ulong proto_pte;
        public ulong vaVadBase;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_VADEX
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] _Reserved1;

        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_UNLOADEDMODULEENTRY
    {
        public ulong vaBase;
        public uint cbImageSize;
        public bool fWow64;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszText;
        public uint _FutureUse1;
        public uint dwCheckSum;
        public uint dwTimeDateStamp;
        public uint _Reserved1;
        public ulong ftUnload;
    }

    public struct VMMDLL_MAP_UNLOADEDMODULE
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] _Reserved1;

        public ulong pbMultiText;
        public uint cbMultiText;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_MODULEENTRY_DEBUGINFO
    {
        public uint dwAge;
        public uint _Reserved;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Guid;

        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszGuid;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszPdbFilename;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_MODULEENTRY_VERSIONINFO
    {
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszCompanyName;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszFileDescription;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszFileVersion;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszInternalName;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszLegalCopyright;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszFileOriginalFilename;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszProductName;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszProductVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_MODULEENTRY
    {
        public ulong vaBase;
        public ulong vaEntry;
        public uint cbImageSize;
        public bool fWow64;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszText;
        public uint _Reserved3;
        public uint _Reserved4;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszFullName;
        public uint tp;
        public uint cbFileSizeRaw;
        public uint cSection;
        public uint cEAT;
        public uint cIAT;
        public uint _Reserved2;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public ulong[] _Reserved1;

        public IntPtr pExDebugInfo;
        public IntPtr pExVersionInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_EATENTRY
    {
        public ulong vaFunction;
        public uint dwOrdinal;
        public uint oFunctionsArray;
        public uint oNamesArray;
        public uint _FutureUse1;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszFunction;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszForwardedFunction;
    }

    public struct VMMDLL_MAP_EAT
    {
        public uint dwVersion;
        public uint dwOrdinalBase;
        public uint cNumberOfNames;
        public uint cNumberOfFunctions;
        public uint cNumberOfForwardedFunctions;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] _Reserved1;

        public ulong vaModuleBase;
        public ulong vaAddressOfFunctions;
        public ulong vaAddressOfNames;
        public ulong pbMultiText;
        public uint cbMultiText;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_IATENTRY
    {
        public ulong vaFunction;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszFunction;
        public uint _FutureUse1;
        public uint _FutureUse2;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszModule;
        public bool f32;
        public ushort wHint;
        public ushort _Reserved1;
        public uint rvaFirstThunk;
        public uint rvaOriginalFirstThunk;
        public uint rvaNameModule;
        public uint rvaNameFunction;
    }

    public struct VMMDLL_MAP_IAT
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] _Reserved1;

        public ulong vaModuleBase;
        public ulong pbMultiText;
        public uint cbMultiText;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_HEAPENTRY
    {
        public ulong va;
        public uint tp;
        public bool f32;
        public uint iHeap;
        public uint dwHeapNum;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_HEAPSEGMENTENTRY
    {
        public ulong va;
        public uint cb;
        public ushort tp;
        public ushort iHeap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_HEAP
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public uint[] _Reserved1;

        public IntPtr pSegments;
        public uint cSegments;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_HEAPALLOCENTRY
    {
        public ulong va;
        public uint cb;
        public uint tp;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_HEAPALLOC
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public uint[] _Reserved1;

        public IntPtr _Reserved20;
        public IntPtr _Reserved21;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_THREADENTRY
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
        public ulong vaStackBaseUser; // value from _NT_TIB / _TEB
        public ulong vaStackLimitUser; // value from _NT_TIB / _TEB
        public ulong vaStackBaseKernel;
        public ulong vaStackLimitKernel;
        public ulong vaTrapFrame;
        public ulong vaRIP; // RIP register (if user mode)
        public ulong vaRSP; // RSP register (if user mode)
        public ulong qwAffinity;
        public uint dwUserTime;
        public uint dwKernelTime;
        public byte bSuspendCount;
        public byte bWaitReason;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] _FutureUse1;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
        public uint[] _FutureUse2;

        public ulong vaImpersonationToken;
        public ulong vaWin32StartAddress;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_THREAD
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public uint[] _Reserved1;

        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_THREAD_CALLSTACKENTRY
    {
        public uint i;
        public bool fRegPresent;
        public ulong vaRetAddr;
        public ulong vaRSP;
        public ulong vaBaseSP;
        public uint _FutureUse1;
        public uint cbDisplacement;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszModule;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszFunction;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_THREAD_CALLSTACK
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public uint[] _Reserved1;

        public uint dwPID;
        public uint dwTID;
        public uint cbText;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszText;
        public IntPtr pbMultiText;
        public uint cbMultiText;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_HANDLEENTRY
    {
        public ulong vaObject;
        public uint dwHandle;
        public uint dwGrantedAccess_iType;
        public ulong qwHandleCount;
        public ulong qwPointerCount;
        public ulong vaObjectCreateInfo;
        public ulong vaSecurityDescriptor;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszText;
        public uint _FutureUse2;
        public uint dwPID;
        public uint dwPoolTag;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public uint[] _FutureUse;

        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszType;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_HANDLE
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] _Reserved1;

        public ulong pbMultiText;
        public uint cbMultiText;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_NETENTRY
    {
        public uint dwPID;
        public uint dwState;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public ushort[] _FutureUse3;

        public ushort AF;

        // src
        public bool src_fValid;
        public ushort src__Reserved1;
        public ushort src_port;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] src_pbAddr;

        [MarshalAs(UnmanagedType.LPUTF8Str)] public string src_uszText;

        // dst
        public bool dst_fValid;
        public ushort dst__Reserved1;
        public ushort dst_port;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] dst_pbAddr;

        [MarshalAs(UnmanagedType.LPUTF8Str)] public string dst_uszText;

        //
        public ulong vaObj;
        public ulong ftTime;
        public uint dwPoolTag;
        public uint _FutureUse4;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszText;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] _FutureUse2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_NET
    {
        public uint dwVersion;
        public uint _Reserved1;
        public ulong pbMultiText;
        public uint cbMultiText;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_PHYSMEMENTRY
    {
        public ulong pa;
        public ulong cb;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_PHYSMEM
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] _Reserved1;

        public uint cMap;
        public uint _Reserved2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_KDEVICEENTRY
    {
        public ulong va;
        public uint iDepth;
        public uint dwDeviceType;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszDeviceType;
        public ulong vaDriverObject;
        public ulong vaAttachedDevice;
        public ulong vaFileSystemDevice;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszVolumeInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_KDEVICE
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] _Reserved1;

        public ulong pbMultiText;
        public uint cbMultiText;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_KDRIVERENTRY
    {
        public ulong va;
        public ulong vaDriverStart;
        public ulong cbDriverSize;
        public ulong vaDeviceObject;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszName;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszPath;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszServiceKeyName;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
        public ulong[] MajorFunction;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_KDRIVER
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] _Reserved1;

        public ulong pbMultiText;
        public uint cbMultiText;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_KOBJECTENTRY
    {
        public ulong va;
        public ulong vaParent;
        public uint _Filler;
        public uint cvaChild;
        public IntPtr pvaChild;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszName;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszType;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_KOBJECT
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] _Reserved1;

        public ulong pbMultiText;
        public uint cbMultiText;
        public uint cMap;
    }

    public const uint VMMDLL_POOLMAP_FLAG_ALL = 0;
    public const uint VMMDLL_POOLMAP_FLAG_BIG = 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_POOLENTRY
    {
        public ulong va;
        public uint dwTag;
        public byte _ReservedZero;
        public byte fAlloc;
        public byte tpPool;
        public byte tpSS;
        public uint cb;
        public uint _Filler;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_POOL
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public uint[] _Reserved1;

        public uint cbTotal;
        public IntPtr _piTag2Map;
        public IntPtr _pTag;
        public uint cTag;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct VMMDLL_MAP_USERENTRY
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public uint[] _FutureUse1;

        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszText;
        public ulong vaRegHive;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszSID;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public uint[] _FutureUse2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_USER
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] _Reserved1;

        public ulong pbMultiText;
        public uint cbMultiText;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_VMENTRY
    {
        public ulong hVM;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszName;
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

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_VM
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] _Reserved1;

        public ulong pbMultiText;
        public uint cbMultiText;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_SERVICEENTRY
    {
        public ulong vaObj;
        public uint dwOrdinal;

        public uint dwStartType;

        // SERVICE_STATUS START
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;

        public uint dwWaitHint;

        // SERVICE_STATUS END
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszServiceName;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszDisplayName;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszPath;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszUserTp;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszUserAcct;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string uszImagePath;
        public uint dwPID;
        public uint _FutureUse1;
        public ulong _FutureUse2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_SERVICE
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] _Reserved1;

        public ulong pbMultiText;
        public uint cbMultiText;
        public uint cMap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_PFNENTRY
    {
        public uint dwPfn;
        public uint tpExtended;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] dwPfnPte;

        public ulong va;
        public ulong vaPte;
        public ulong OriginalPte;
        public uint _u3;
        public ulong _u4;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public uint[] _FutureUse;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MAP_PFN
    {
        public uint dwVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] _Reserved1;

        public uint cMap;
        public uint _Reserved2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_REGISTRY_HIVE_INFORMATION
    {
        public ulong magic;
        public ushort wVersion;
        public ushort wSize;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x34)]
        public byte[] _FutureReserved1;

        public ulong vaCMHIVE;
        public ulong vaHBASE_BLOCK;
        public uint cbLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] uszName;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 33)]
        public byte[] uszNameShort;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 260)]
        public byte[] uszHiveRootPath;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x10)]
        public ulong[] _FutureReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VMMDLL_MEM_SEARCH_CONTEXT_SEARCHENTRY
    {
        public uint cbAlign;
        public uint cb;
        public unsafe fixed byte pb[32];

        public unsafe fixed byte pbSkipMask[32];
        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] pb;
        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] pbSkipMask;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool SearchResultCallback(VMMDLL_MEM_SEARCH_CONTEXT ctx, ulong va, uint iSearch);

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct VMMDLL_MEM_SEARCH_CONTEXT
    {
        public uint dwVersion;
        private readonly uint _Filler01;
        private readonly uint _Filler02;
        public int fAbortRequested;
        public uint cMaxResult;
        public uint cSearch;
        public IntPtr search;
        public ulong vaMin;
        public ulong vaMax;
        public readonly ulong vaCurrent;
        private readonly uint _Filler2;
        private readonly uint cResult;
        public readonly ulong cbReadTotal;
        private readonly IntPtr pvUserPtrOpt;
        public IntPtr pfnResultOptCB;
        public ulong ReadFlags;
        private readonly bool fForcePTE;
        private readonly bool fForceVAD;
        private readonly IntPtr pfnFilterOptCB;
    }

    #endregion

    #region Imports

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_InitializeEx")]
    public static partial IntPtr VMMDLL_InitializeEx(
        int argc,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] [In]
        string[] argv,
        out IntPtr ppLcErrorInfo);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_CloseAll")]
    public static partial void VMMDLL_CloseAll();

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Close")]
    public static partial void VMMDLL_Close(
        IntPtr hVMM);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_ConfigGet")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VMMDLL_ConfigGet(
        IntPtr hVMM,
        VmmOption fOption,
        out ulong pqwValue);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_ConfigSet")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VMMDLL_ConfigSet(
        IntPtr hVMM,
        VmmOption fOption,
        ulong qwValue);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_MemFree")]
    public static unsafe partial void VMMDLL_MemFree(
        byte* pvMem);

    // VFS (VIRTUAL FILE SYSTEM) FUNCTIONALITY BELOW:

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_VfsListU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_VfsList(
        IntPtr hVMM,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string wcsPath,
        ref VMMDLL_VFS_FILELIST pFileList);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_VfsReadU")]
    public static unsafe partial uint VMMDLL_VfsRead(
        IntPtr hVMM,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string wcsFileName,
        byte* pb,
        uint cb,
        out uint pcbRead,
        ulong cbOffset);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_VfsWriteU")]
    public static unsafe partial uint VMMDLL_VfsWrite(
        IntPtr hVMM,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string wcsFileName,
        byte* pb,
        uint cb,
        out uint pcbRead,
        ulong cbOffset);

    // PLUGIN FUNCTIONALITY BELOW:

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_InitializePlugins")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VMMDLL_InitializePlugins(IntPtr hVMM);

    // MEMORY READ/WRITE FUNCTIONALITY BELOW:

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_MemReadScatter")]
    public static unsafe partial uint VMMDLL_MemReadScatter(
        IntPtr hVMM,
        uint dwPID,
        IntPtr ppMEMs,
        uint cpMEMs,
        VmmFlags flags);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_MemReadEx")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_MemReadEx(
        IntPtr hVMM,
        uint dwPID,
        ulong qwA,
        byte* pb,
        uint cb,
        out uint pcbReadOpt,
        VmmFlags flags);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_MemPrefetchPages")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_MemPrefetchPages(
        IntPtr hVMM,
        uint dwPID,
        byte* pPrefetchAddresses,
        uint cPrefetchAddresses);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_MemWrite")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_MemWrite(
        IntPtr hVMM,
        uint dwPID,
        ulong qwA,
        byte* pb,
        uint cb);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_MemVirt2Phys")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VMMDLL_MemVirt2Phys(
        IntPtr hVMM,
        uint dwPID,
        ulong qwVA,
        out ulong pqwPA
    );

    // MEMORY NEW SCATTER READ/WRITE FUNCTIONALITY BELOW:

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Scatter_Initialize")]
    public static unsafe partial IntPtr VMMDLL_Scatter_Initialize(
        IntPtr hVMM,
        uint dwPID,
        VmmFlags flags);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Scatter_Prepare")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Scatter_Prepare(
        IntPtr hS,
        ulong va,
        uint cb);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Scatter_PrepareWrite")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Scatter_PrepareWrite(
        IntPtr hS,
        ulong va,
        byte* pb,
        uint cb);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Scatter_ExecuteRead")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Scatter_ExecuteRead(
        IntPtr hS);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Scatter_Execute")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Scatter_Execute(
        IntPtr hS);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Scatter_Read")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Scatter_Read(
        IntPtr hS,
        ulong va,
        uint cb,
        byte* pb,
        out uint pcbRead);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Scatter_Clear")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SVMMDLL_Scatter_Clear(IntPtr hS, uint dwPID, VmmFlags flags);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Scatter_Clear")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Scatter_Clear(
        IntPtr hS,
        uint dwPID,
        VmmFlags flags);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Scatter_CloseHandle")]
    public static unsafe partial void VMMDLL_Scatter_CloseHandle(
        IntPtr hS);

    // PROCESS FUNCTIONALITY BELOW:

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_PidList")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_PidList(IntPtr hVMM, byte* pPIDs, ref ulong pcPIDs);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_PidGetFromName")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VMMDLL_PidGetFromName(IntPtr hVMM, [MarshalAs(UnmanagedType.LPStr)] string szProcName, out uint pdwPID);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_ProcessGetProcAddressU")]
    public static partial ulong VMMDLL_ProcessGetProcAddress(IntPtr hVMM, uint pid, [MarshalAs(UnmanagedType.LPUTF8Str)] string uszModuleName, [MarshalAs(UnmanagedType.LPStr)] string szFunctionName);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_ProcessGetModuleBaseU")]
    public static partial ulong VMMDLL_ProcessGetModuleBase(IntPtr hVMM, uint pid, [MarshalAs(UnmanagedType.LPUTF8Str)] string uszModuleName);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_ProcessGetInformation")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_ProcessGetInformation(
        IntPtr hVMM,
        uint dwPID,
        byte* pProcessInformation,
        ref ulong pcbProcessInformation);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_ProcessGetInformationAll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VMMDLL_ProcessGetInformationAll(
    IntPtr hVMM,
    out IntPtr ppProcessInformationAll,
    out uint pcProcessInformation
    );

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_ProcessGetInformationString")]
    public static unsafe partial byte* VMMDLL_ProcessGetInformationString(
        IntPtr hVMM,
        uint dwPID,
        uint fOptionString);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_ProcessGetDirectoriesU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_ProcessGetDirectories(
        IntPtr hVMM,
        uint dwPID,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string uszModule,
        byte* pData);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_ProcessGetSectionsU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_ProcessGetSections(
        IntPtr hVMM,
        uint dwPID,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string uszModule,
        byte* pData,
        uint cData,
        out uint pcData);

    // WINDOWS SPECIFIC DEBUGGING / SYMBOL FUNCTIONALITY BELOW:

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_PdbLoad")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_PdbLoad(
        IntPtr hVMM,
        uint dwPID,
        ulong vaModuleBase,
        byte* pModuleMapEntry);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_PdbSymbolName")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_PdbSymbolName(
        IntPtr hVMM,
        [MarshalAs(UnmanagedType.LPStr)] string szModule,
        ulong cbSymbolAddressOrOffset,
        byte* szSymbolName,
        out uint pdwSymbolDisplacement);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_PdbSymbolAddress")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VMMDLL_PdbSymbolAddress(
        IntPtr hVMM,
        [MarshalAs(UnmanagedType.LPStr)] string szModule,
        [MarshalAs(UnmanagedType.LPStr)] string szSymbolName,
        out ulong pvaSymbolAddress);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_PdbTypeSize")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VMMDLL_PdbTypeSize(
        IntPtr hVMM,
        [MarshalAs(UnmanagedType.LPStr)] string szModule,
        [MarshalAs(UnmanagedType.LPStr)] string szTypeName,
        out uint pcbTypeSize);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_PdbTypeChildOffset")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VMMDLL_PdbTypeChildOffset(
        IntPtr hVMM,
        [MarshalAs(UnmanagedType.LPStr)] string szModule,
        [MarshalAs(UnmanagedType.LPStr)] string szTypeName,
        [MarshalAs(UnmanagedType.LPStr)] string wszTypeChildName,
        out uint pcbTypeChildOffset);

    // VMMDLL_Map_GetPte
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetPteU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetPte(
        IntPtr hVMM,
        uint dwPid,
        [MarshalAs(UnmanagedType.Bool)] bool fIdentifyModules,
        out IntPtr ppPteMap);

    // VMMDLL_Map_GetVad
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetVadU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetVad(
        IntPtr hVMM,
        uint dwPid,
        [MarshalAs(UnmanagedType.Bool)] bool fIdentifyModules,
        out IntPtr ppVadMap);

    // VMMDLL_Map_GetVadEx
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetVadEx")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetVadEx(
        IntPtr hVMM,
        uint dwPid,
        uint oPage,
        uint cPage,
        out IntPtr ppVadExMap);

    // VMMDLL_Map_GetModule
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetModuleU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetModule(
        IntPtr hVMM,
        uint dwPid,
        out IntPtr ppModuleMap,
        uint flags);

    // VMMDLL_Map_GetModuleFromName
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetModuleFromNameU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetModuleFromName(
        IntPtr hVMM,
        uint dwPID,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string uszModuleName,
        out IntPtr ppModuleMapEntry,
        uint flags);

    // VMMDLL_Map_GetUnloadedModule
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetUnloadedModuleU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetUnloadedModule(
        IntPtr hVMM,
        uint dwPid,
        out IntPtr ppModuleMap);

    // VMMDLL_Map_GetEAT
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetEATU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetEAT(
        IntPtr hVMM,
        uint dwPid,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string uszModuleName,
        out IntPtr ppEatMap);

    // VMMDLL_Map_GetIAT
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetIATU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetIAT(
        IntPtr hVMM,
        uint dwPid,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string uszModuleName,
        out IntPtr ppIatMap);

    // VMMDLL_Map_GetHeap
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetHeap")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetHeap(
        IntPtr hVMM,
        uint dwPid,
        out IntPtr ppHeapMap);

    // VMMDLL_Map_GetHeapAlloc
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetHeapAlloc")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetHeapAlloc(
        IntPtr hVMM,
        uint dwPid,
        ulong qwHeapNumOrAddress,
        out IntPtr ppHeapAllocMap);

    // VMMDLL_Map_GetThread
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetThread")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetThread(
        IntPtr hVMM,
        uint dwPid,
        out IntPtr ppThreadMap);

    // VMMDLL_Map_GetThread_Callstack
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetThread_CallstackU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetThread_Callstack(
        IntPtr hVMM,
        uint dwPID,
        uint dwTID,
        VmmFlags flags,
        out IntPtr ppThreadCallstack);

    // VMMDLL_Map_GetHandle
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetHandleU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetHandle(
        IntPtr hVMM,
        uint dwPid,
        out IntPtr ppHandleMap);

    // VMMDLL_Map_GetNet
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetNetU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetNet(
        IntPtr hVMM,
        out IntPtr ppNetMap);

    // VMMDLL_Map_GetPhysMem
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetPhysMem")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetPhysMem(
        IntPtr hVMM,
        out IntPtr ppPhysMemMap);

    // VMMDLL_Map_GetKDevice
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetKDeviceU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetKDevice(
        IntPtr hVMM,
        out IntPtr ppKDeviceMap);

    // VMMDLL_Map_GetKDriver
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetKDriverU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetKDriver(
        IntPtr hVMM,
        out IntPtr ppKDriverMap);

    // VMMDLL_Map_GetKObject
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetKObjectU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetKObject(
        IntPtr hVMM,
        out IntPtr ppKObjectMap);

    // VMMDLL_Map_GetPool
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetPool")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetPool(
        IntPtr hVMM,
        out IntPtr ppPoolMap,
        VmmPoolMapFlags flags);

    // VMMDLL_Map_GetUsers
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetUsersU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetUsers(
        IntPtr hVMM,
        out IntPtr ppUserMap);

    // VMMDLL_Map_GetVM
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetVMU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetVM(
        IntPtr hVMM,
        out IntPtr ppUserMap);

    // VMMDLL_Map_GetServices
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetServicesU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetServices(
        IntPtr hVMM,
        out IntPtr ppServiceMap);

    // VMMDLL_Map_GetPfn
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Map_GetPfn")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Map_GetPfn(
        IntPtr hVMM,
        byte* pPfns,
        uint cPfns,
        byte* pPfnMap,
        ref uint pcbPfnMap);

    // REGISTRY FUNCTIONALITY BELOW:
    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_WinReg_HiveList")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_WinReg_HiveList(
        IntPtr hVMM,
        byte* pHives,
        uint cHives,
        out uint pcHives);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_WinReg_HiveReadEx")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_WinReg_HiveReadEx(
        IntPtr hVMM,
        ulong vaCMHive,
        uint ra,
        byte* pb,
        uint cb,
        out uint pcbReadOpt,
        VmmFlags flags);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_WinReg_HiveWrite")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_WinReg_HiveWrite(
        IntPtr hVMM,
        ulong vaCMHive,
        uint ra,
        byte* pb,
        uint cb);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_WinReg_EnumKeyExU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_WinReg_EnumKeyEx(
        IntPtr hVMM,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string uszFullPathKey,
        uint dwIndex,
        byte* lpName,
        ref uint lpcchName,
        out ulong lpftLastWriteTime);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_WinReg_EnumValueU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_WinReg_EnumValue(
        IntPtr hVMM,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string uszFullPathKey,
        uint dwIndex,
        byte* lpValueName,
        ref uint lpcchValueName,
        out uint lpType,
        byte* lpData,
        ref uint lpcbData);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_WinReg_QueryValueExU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_WinReg_QueryValueEx(
        IntPtr hVMM,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string uszFullPathKeyValue,
        out uint lpType,
        byte* lpData,
        ref uint lpcbData);

    // MEMORY SEARCH FUNCTIONALITY BELOW:

#pragma warning disable SYSLIB1054

    [DllImport("vmm.dll", EntryPoint = "VMMDLL_MemSearch")]
    public static extern unsafe bool VMMDLL_MemSearch(
        IntPtr hVMM,
        uint dwPID,
        void* ctx,
        IntPtr ppva,
        IntPtr pcva);

#pragma warning restore SYSLIB1054

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_UtilFillHexAscii")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_UtilFillHexAscii(
        byte* pb,
        uint cb,
        uint cbInitialOffset,
        byte* sz,
        ref uint pcsz);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_Log")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_Log(
        IntPtr hVMM,
        uint MID,
        Vmm.LogLevel dwLogLevel,
        [MarshalAs(UnmanagedType.LPStr)] string uszFormat,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string uszTextToLog);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_LogCallback")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VMMDLL_LogCallback(
        IntPtr hVMM,
        Vmm.VMMDLL_LOG_CALLBACK_PFN pfnCB);

    [LibraryImport("vmm.dll", EntryPoint = "VMMDLL_MemCallback")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool VMMDLL_MemCallback(
        IntPtr hVMM,
        VmmMemCallbackType tp,
        IntPtr ctxUser,
        IntPtr pfnCB);

    #endregion
}