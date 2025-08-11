using System.Runtime.InteropServices;

namespace VmmSharpEx.Internal
{
    internal static partial class Lci
    {
        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct LC_CONFIG_ERRORINFO
        {
            internal uint dwVersion;
            internal uint cbStruct;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] internal uint[] _FutureUse;
            internal bool fUserInputRequest;
            internal uint cwszUserText;
            // szUserText
        }

        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal unsafe struct LC_MEM_SCATTER
        {
            internal uint version;
            internal bool f;
            internal ulong qwA;
            internal IntPtr pb;
            internal uint cb;
            internal uint iStack;
            public fixed ulong vStack[12];
        }

#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time

        [DllImport("leechcore.dll", EntryPoint = "LcCreate")]
        public static extern IntPtr LcCreate(ref LeechCore.LCConfig pLcCreateConfig);

        [DllImport("leechcore.dll", EntryPoint = "LcCreateEx")]
        public static extern IntPtr LcCreateEx(ref LeechCore.LCConfig pLcCreateConfig, out IntPtr ppLcCreateErrorInfo);

#pragma warning restore SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time


        [LibraryImport("leechcore.dll", EntryPoint = "LcClose")]
        internal static partial void LcClose(IntPtr hLC);

        [LibraryImport("leechcore.dll", EntryPoint = "LcMemFree")]
        internal static unsafe partial void LcMemFree(IntPtr pv);

        [LibraryImport("leechcore.dll", EntryPoint = "LcAllocScatter1")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static unsafe partial bool LcAllocScatter1(uint cMEMs, out IntPtr pppMEMs);

        [LibraryImport("leechcore.dll", EntryPoint = "LcRead")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static unsafe partial bool LcRead(IntPtr hLC, ulong pa, uint cb, byte* pb);

        [LibraryImport("leechcore.dll", EntryPoint = "LcReadScatter")]
        internal static unsafe partial void LcReadScatter(IntPtr hLC, uint cMEMs, IntPtr ppMEMs);

        [LibraryImport("leechcore.dll", EntryPoint = "LcWrite")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static unsafe partial bool LcWrite(IntPtr hLC, ulong pa, uint cb, byte* pb);

        [LibraryImport("leechcore.dll", EntryPoint = "LcWriteScatter")]
        internal static unsafe partial void LcWriteScatter(IntPtr hLC, uint cMEMs, IntPtr ppMEMs);

        [LibraryImport("leechcore.dll", EntryPoint = "LcGetOption")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetOption(IntPtr hLC, ulong fOption, out ulong pqwValue);

        [LibraryImport("leechcore.dll", EntryPoint = "LcSetOption")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetOption(IntPtr hLC, ulong fOption, ulong qwValue);

        [LibraryImport("leechcore.dll", EntryPoint = "LcCommand")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static unsafe partial bool LcCommand(IntPtr hLC, ulong fOption, uint cbDataIn, byte* pbDataIn, out IntPtr ppbDataOut, out uint pcbDataOut);
    }
}
