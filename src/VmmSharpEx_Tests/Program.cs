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
        var info = vmm.ProcessGetInformationAll();
        foreach (var proc in info)
        {
            Console.WriteLine(proc.sNameLong);
        }
        var pids = vmm.PidGetAllFromName("brave.exe");
        foreach (var pid in pids)
        {
            Console.WriteLine(pid);
        }
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