using System.Runtime.CompilerServices;
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
        ScatterMap_Example.Run(vmm);
    }
}