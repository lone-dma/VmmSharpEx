﻿/*  
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

internal static partial class Lci
{
    [LibraryImport("leechcore.dll", EntryPoint = "LcClose")]
    public static partial void LcClose(IntPtr hLC);

    [LibraryImport("leechcore.dll", EntryPoint = "LcMemFree")]
    public static unsafe partial void LcMemFree(IntPtr pv);

    [LibraryImport("leechcore.dll", EntryPoint = "LcAllocScatter1")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool LcAllocScatter1(uint cMEMs, out IntPtr pppMEMs);

    [LibraryImport("leechcore.dll", EntryPoint = "LcRead")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool LcRead(IntPtr hLC, ulong pa, uint cb, byte* pb);

    [LibraryImport("leechcore.dll", EntryPoint = "LcReadScatter")]
    public static unsafe partial void LcReadScatter(IntPtr hLC, uint cMEMs, IntPtr ppMEMs);

    [LibraryImport("leechcore.dll", EntryPoint = "LcWrite")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool LcWrite(IntPtr hLC, ulong pa, uint cb, byte* pb);

    [LibraryImport("leechcore.dll", EntryPoint = "LcGetOption")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetOption(IntPtr hLC, LcOption fOption, out ulong pqwValue);

    [LibraryImport("leechcore.dll", EntryPoint = "LcSetOption")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetOption(IntPtr hLC, LcOption fOption, ulong qwValue);

    [LibraryImport("leechcore.dll", EntryPoint = "LcCommand")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool LcCommand(IntPtr hLC, LcCmd fOption, uint cbDataIn, byte* pbDataIn, out IntPtr ppbDataOut, out uint pcbDataOut);

    [StructLayout(LayoutKind.Sequential)]
    public struct LC_CONFIG_ERRORINFO
    {
        public uint dwVersion;
        public uint cbStruct;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public uint[] _FutureUse;

        public bool fUserInputRequest;

        public uint cwszUserText;
        // szUserText
    }

#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time

    [DllImport("leechcore.dll", EntryPoint = "LcCreate")]
    public static extern IntPtr LcCreate(ref LeechCore.LCConfig pLcCreateConfig);

    [DllImport("leechcore.dll", EntryPoint = "LcCreateEx")]
    public static extern IntPtr LcCreateEx(ref LeechCore.LCConfig pLcCreateConfig, out IntPtr ppLcCreateErrorInfo);

#pragma warning restore SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
}