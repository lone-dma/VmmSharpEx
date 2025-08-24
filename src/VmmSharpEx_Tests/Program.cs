using System.Text;
using VmmSharpEx;
using VmmSharpEx.Scatter;

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
        if (!vmm.PidGetFromName("explorer.exe", out uint pid))
            throw new InvalidOperationException("Failed to get PID");
        ulong baseAddress = vmm.ProcessGetModuleBase(pid, "explorer.exe");
        {
            using var map = new ScatterReadMap(vmm, pid); // MUST DISPOSE
            var rd1 = map.AddRound();
            var rd2 = map.AddRound(useCache: false);
            for (int ix = 0; ix < 3; ix++)
            {
                int i = ix; // Capture
                rd1[i].AddValueEntry<uint>(0, baseAddress + (uint)i * 4);
                rd1[i].AddStringEntry(1, baseAddress, 16, Encoding.ASCII);
                rd1[i].Completed += (sender, cb1) =>
                {
                    if (cb1.TryGetValue<uint>(0, out var value))
                    {
                        Console.WriteLine($"DWORD: {value}");
                        rd2[i].AddArrayEntry<byte>(0, baseAddress + (uint)i * 12, 12);
                        rd2[i].Completed += (sender, cb2) =>
                        {
                            if (cb2.TryGetArray<byte>(0, out var bytes))
                            {
                                Console.WriteLine($"Bytes: {BitConverter.ToString(bytes.ToArray())}");
                            }
                        };
                    }
                    if (cb1.TryGetString(1, out var str))
                    {
                        Console.WriteLine($"String: {str}");
                    }
                };
            }
            map.Execute();
        }
        Thread.Sleep(5000);
    }
}