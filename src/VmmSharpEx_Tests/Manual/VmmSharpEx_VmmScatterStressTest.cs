/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using VmmSharpEx;
using VmmSharpEx_Tests.Manual.Internal;
using Xunit.Abstractions;

namespace VmmSharpEx_Tests.Manual;

[Collection(nameof(ManualCollection))]
public class VmmSharpEx_VmmScatterStressTest
{
    private readonly Vmm _vmm;
    private readonly ITestOutputHelper _output;

    public VmmSharpEx_VmmScatterStressTest(ManualVmmFixture fixture, ITestOutputHelper output)
    {
        _vmm = fixture.Vmm;
        _output = output;
    }

    //[Fact()]
    public void VmmScatter_StressTest_PhysicalMemory()
    {
        const int numThreads = 16;
        const int minReadSize = 0x1000; // 4KB
        const int maxReadSize = 128 * 1024 * 1024; // 128MB per op (adjustable)
        var maxAddress = (1UL << 40) - 0x1000; // 1TB - 4KB (adjust as needed)
        var errors = new List<Exception>();
        var threads = new List<Thread>();
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var threadLocalRandom = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        for (int t = 0; t < numThreads; t++)
        {
            var thread = new Thread(() =>
            {
                var rand = threadLocalRandom.Value!;
                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        // Portable random ulong address, page-aligned
                        ulong high = (uint)rand.Next(0, int.MaxValue);
                        ulong low = (uint)rand.Next(0, int.MaxValue);
                        ulong addr = ((high << 32) | low) & ~0xFFFUL;
                        addr = addr % maxAddress;
                        int readSize = rand.Next(minReadSize, maxReadSize + 1) & ~0xFFF; // page-aligned size

                        using var scatter = _vmm.CreateScatter(Vmm.PID_PHYSICALMEMORY);
                        bool prepared = scatter.PrepareRead(addr, readSize);
                        if (!prepared)
                        {
                            _output.WriteLine($"[Thread {Thread.CurrentThread.ManagedThreadId}] PrepareRead failed: addr=0x{addr:X}, size=0x{readSize:X}");
                            continue;
                        }
                        try
                        {
                            scatter.Execute();
                            var buffer = new byte[0x1000]; // Read just the first page for verification
                            bool ok = scatter.ReadSpan(addr, buffer);
                            _output.WriteLine($"[Thread {Thread.CurrentThread.ManagedThreadId}] Read addr=0x{addr:X}, size=0x{readSize:X}, ok={ok}");
                        }
                        catch (Exception ex)
                        {
                            lock (errors) { errors.Add(ex); }
                            _output.WriteLine($"[Thread {Thread.CurrentThread.ManagedThreadId}] Exception: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (errors) { errors.Add(ex); }
                }
            });
            threads.Add(thread);
        }

        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        if (errors.Count > 0)
        {
            throw new AggregateException(errors);
        }
    }
}
