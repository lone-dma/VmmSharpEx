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
        if (!vmm.PidGetFromName("explorer.exe", out var pid))
        {
            throw new VmmException("Failed to get PID for explorer.exe");
        }

        using var cb = vmm.CreateMemCallback(VmmMemCallbackType.READ_VIRTUAL_PRE, TestCb, 0);

        ulong vaBase = vmm.ProcessGetModuleBase(pid, "explorer.exe");
        for (int i = 0; i < 10; i++)
        {
            vmm.MemRead(pid, vaBase + (uint)(i * 0x1000), 8);
            Thread.Sleep(1000);
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