using VmmSharpEx;
using VmmSharpEx.Options;

namespace VmmSharpEx_Tests;

internal unsafe class Program
{
    private static void Main()
    {
        string[] args = new[]
        {
            "-printf",
            "-v",
            "-device",
            "fpga",
            "-waitinitialize"
        };
        using var vmm = new Vmm(args);
        if (!vmm.PidGetFromName("Process.exe", out uint pid))
            throw new InvalidOperationException("Failed to get PID");
        ulong addr = vmm.FindSignature(pid, "40 a6 93 aa cd 01 00 00");
        Console.WriteLine(addr.ToString("X"));
    }

    private static void TestCb(
        IntPtr ctxUser,
        uint dwPID,
        uint cpMEMs,
        LeechCore.LcMemScatter** ppMEMs)
    {
        Console.WriteLine(ctxUser);
        Console.WriteLine(dwPID);
        Console.WriteLine(cpMEMs);
        for (int i = 0; i < cpMEMs; i++)
        {
            var mem = ppMEMs[i];
            Console.WriteLine(mem->qwA);
        }
    }
}