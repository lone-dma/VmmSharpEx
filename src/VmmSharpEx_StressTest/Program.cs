using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using VmmSharpEx;
using VmmSharpEx.Options;
using VmmSharpEx.Scatter;

namespace VmmSharpEx_StressTest;

internal static class Program
{
    #region Initialization

    private static Vmm _vmm = null!;
    private const int THREAD_COUNT = 16;
    private const int MIN_READ_SIZE = 0x8;
    private const int MAX_READ_SIZE = 0x1000;
    private static long _totalOperations = 0;
    private static volatile byte _data;

    private static PMemPageEntry[] _paPages = null!;

    static void Main()
    {
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
        Console.WriteLine("=== VmmSharpEx AOT Stress Test (Infinite Loop) ===");
        Console.WriteLine($"Threads: {THREAD_COUNT}");
        Console.WriteLine("Press Ctrl+C or close window to stop.");
        Console.WriteLine();

        // Start test
        InitDMA();
        for (int i = 0; i < THREAD_COUNT; i++)
        {
            new Thread(TestWorker)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest
            }.Start();
        }

        // Status reporter
        var totalSw = Stopwatch.StartNew();
        var reportSw = Stopwatch.StartNew();
        while (true)
        {
            // Only print status every ~10 seconds
            if (reportSw.Elapsed.TotalSeconds >= 10)
            {
                reportSw.Restart();

                Console.WriteLine($"[{totalSw.Elapsed:hh\\:mm\\:ss}] Ops: {_totalOperations:N0}");
            }
            Thread.Sleep(100);
        }
    }

    private static void TestWorker()
    {
        while (true)
        {
            try
            {
                bool isCached = Random.Shared.Next(2) == 0;
                bool useMap = Random.Shared.Next(2) == 0;
                int opsCount = Random.Shared.Next(1, 50);
                if (useMap)
                {
                    using var map = new VmmScatterMap(_vmm, Vmm.PID_PHYSICALMEMORY);
                    var rd1 = map.AddRound(isCached ? VmmFlags.NONE : VmmFlags.NOCACHE);
                    var rd2 = map.AddRound(isCached ? VmmFlags.NONE : VmmFlags.NOCACHE);
                    for (int i = 0; i < opsCount; i++)
                    {
                        var read1 = GetRandomRead();
                        rd1.PrepareRead(read1.Address, read1.Size);
                        rd1.Completed += (_, s) =>
                        {
                            if (s.ReadPooled<byte>(read1.Address, (int)read1.Size) is IMemoryOwner<byte> arr1)
                            {
                                byte x = arr1.Memory.Span[0];
                                Interlocked.Exchange(ref _data, x);
                                var read2 = GetRandomRead();
                                rd2.PrepareRead(read2.Address, read2.Size);
                                rd2.Completed += (_, s2) =>
                                {
                                    if (s.ReadPooled<byte>(read2.Address, (int)read2.Size) is IMemoryOwner<byte> arr2)
                                    {
                                        byte y = arr2.Memory.Span[0];
                                        Interlocked.Exchange(ref _data, y);
                                    }
                                };
                            }
                        };
                    }
                    map.Execute();
                }
                else
                {
                    using var scatter = new VmmScatter(_vmm, Vmm.PID_PHYSICALMEMORY, isCached ? VmmFlags.NONE : VmmFlags.NOCACHE);
                    for (int i = 0; i < opsCount; i++)
                    {
                        var read = GetRandomRead();
                        scatter.PrepareRead(read.Address, read.Size);
                        scatter.Completed += (_, s) =>
                        {
                            if (s.ReadPooled<byte>(read.Address, (int)read.Size) is IMemoryOwner<byte> arr1)
                            {
                                byte x = arr1.Memory.Span[0];
                                Interlocked.Exchange(ref _data, x);
                            }
                        };
                    }
                    scatter.Execute();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"** {ex.Message}");
            }
            finally
            {
                Interlocked.Increment(ref _totalOperations);
            }
        }
    }

    private static void InitDMA()
    {
        string[] args =
        [
            "-device",
            "FPGA",
            "-printf",
            "-v",
            "-waitinitialize"
        ];

        _vmm = new Vmm(args)
        {
            EnableMemoryWriting = false
        };
        _ = _vmm.GetMemoryMap(applyMap: true);
        var map = _vmm.Map_GetPhysMem() ?? throw new InvalidOperationException("Failed to retrieve Physical Memory Map!");
        // Set the physical memory pages.
        var paList = new List<PMemPageEntry>();
        foreach (var pMapEntry in map)
        {
            for (ulong p = pMapEntry.pa, cbToEnd = pMapEntry.cb;
                cbToEnd > 0x1000;
                p += 0x1000, cbToEnd -= 0x1000)
            {
                paList.Add(new()
                {
                    PageBase = p,
                    RemainingBytesInSection = cbToEnd
                });
            }
        }
        var pages = paList.ToArray();
        Random.Shared.Shuffle(pages);
        _paPages = pages;
    }

    private readonly struct PMemPageEntry
    {
        public readonly ulong PageBase { get; init; }
        public readonly ulong RemainingBytesInSection { get; init; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (ulong Address, uint Size) GetRandomRead()
    {
        var page = _paPages[Random.Shared.Next(_paPages.Length)];
        int maxSize = (int)Math.Min((ulong)MAX_READ_SIZE, page.RemainingBytesInSection);
        uint size = (uint)Random.Shared.Next(MIN_READ_SIZE, maxSize + 1);
        ulong maxOffset = Math.Min((ulong)(0x1000 - size), page.RemainingBytesInSection - (ulong)size);
        int offset = Random.Shared.Next(0, (int)maxOffset + 1);
        return (page.PageBase + (ulong)offset, size);
    }

    #endregion
}