using VmmSharpEx;

namespace VmmSharpEx_Tests;

internal class Program
{
    private static Vmm _vmm;

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
        _vmm = new Vmm(args);
        if (!_vmm.PidGetFromName("explorer.exe", out var pid))
        {
            throw new VmmException("Failed to get PID for explorer.exe");
        }

        using var search = _vmm.CreateSearch(pid);
        search.AddEntry(new byte[] { 0x00, 0x00, 0x01, 0x00, 0x01 });
        var result = search.Result;
        Console.WriteLine($"Found {result.Results.Count} results.");
    }
}