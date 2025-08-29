using System.Diagnostics;
using System.Text;
using VmmSharpEx;

namespace VmmSharpEx_Tests;

class Program
{
    /// <summary>
    /// VMM Instance for this App Domain.
    /// </summary>
    internal static Vmm Vmm { get; }
    static Program()
    {
        // Enable Unicode output in console
        Console.OutputEncoding = Encoding.Unicode;
        Console.InputEncoding = Encoding.Unicode;
        // Optimize this process for performance
        Thread.CurrentThread.Priority = ThreadPriority.Highest;
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
        // Initialize VMM
        string[] args = new[]
{
            "-printf",
            "-v",
            "-device",
            "fpga",
            "-waitinitialize"
        };
        Vmm = new Vmm(args);
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
    }

    private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
    {
        Vmm.Dispose();
    }

    static void Main()
    {
        ScatterMap_Example.Run(Vmm);
        RunBenchmark(
            method: ScatterBenchmark.Run, 
            name: nameof(VmmSharpEx_Tests.ScatterBenchmark), 
            count: 10);
    }

    private static void RunBenchmark(Action method, string name, int count)
    {
        var list = new List<TimeSpan>(count);
        for (int i = 0; i <= count; i++)
        {
            var sw = Stopwatch.StartNew();
            method();
            var elapsed = sw.Elapsed;
            if (i > 0) // Already Jitted
            {
                list.Add(elapsed);
                Console.WriteLine($"{name} #{i} Runtime:\n" +
                    $"• {elapsed.TotalMilliseconds} ms\n" +
                    $"• {elapsed.TotalMicroseconds} µs\n" +
                    $"• {elapsed.Ticks} ticks");
            }
        }
        Console.WriteLine($"=== {name} Completed ===\n" +
            $"• {list.Select(x => x.TotalMilliseconds).Average()} ms avg\n" +
            $"• {list.Select(x => x.TotalMicroseconds).Average()} µs avg\n" +
            $"• {list.Select(x => x.Ticks).Average()} ticks avg");
    }
}