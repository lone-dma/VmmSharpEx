/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using VmmSharpEx;
using VmmSharpEx.Scatter;
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
        const int numThreads = 32;
        const int minReadSize = 0x1000; // 4KB
        const int maxReadSize = 128 * 1024 * 1024; // 128MB per op (adjustable)
        var pMap = _vmm.Map_GetPhysMem() ?? throw new InvalidOperationException("Physical memory map not available");
        ulong maxAddress = pMap.Max(x => x.pa + x.cb);
        var errors = new List<Exception>();
        var threads = new List<Thread>();
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var threadLocalRandom = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        // Shared scatter instance for naughty behavior
        var sharedScatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY);
        bool sharedScatterDisposed = false;

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

                        // Naughty: sometimes use shared scatter, sometimes new
                        bool useShared = rand.Next(0, 4) == 0; // 25% chance
                        VmmScatterManaged? scatter = null;
                        if (useShared && !sharedScatterDisposed)
                        {
                            scatter = sharedScatter;
                        }
                        else
                        {
                            scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY);
                        }
                        try
                        {
                            bool prepared = scatter.PrepareRead(addr, readSize);
                            if (!prepared)
                            {
                                _output.WriteLine($"[Thread {Thread.CurrentThread.ManagedThreadId}] PrepareRead failed: addr=0x{addr:X}, size=0x{readSize:X}");
                                continue;
                            }
                            scatter.Execute();
                            var buffer = new byte[0x1000];
                            bool ok = scatter.ReadSpan(addr, buffer);
                            _output.WriteLine($"[Thread {Thread.CurrentThread.ManagedThreadId}] Read addr=0x{addr:X}, size=0x{readSize:X}, ok={ok}, shared={useShared}");
                        }
                        catch (Exception ex)
                        {
                            lock (errors) { errors.Add(ex); }
                            _output.WriteLine($"[Thread {Thread.CurrentThread.ManagedThreadId}] Exception: {ex.Message}");
                        }
                        finally
                        {
                            // Only dispose if not shared
                            if (!useShared && scatter != null)
                                scatter.Dispose();
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

        // Naughty: Use-after-dispose scenario
        try
        {
            var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY);
            scatter.Dispose();
            try
            {
                // Try to use after dispose
                scatter.PrepareRead(0, 0x1000);
                _output.WriteLine("[UseAfterDispose] PrepareRead did not throw");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[UseAfterDispose] Managed exception: {ex.Message}");
            }
            try
            {
                scatter.Execute();
                _output.WriteLine("[UseAfterDispose] Execute did not throw");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[UseAfterDispose] Managed exception: {ex.Message}");
            }
            try
            {
                var buffer = new byte[0x1000];
                scatter.ReadSpan(0, buffer);
                _output.WriteLine("[UseAfterDispose] ReadSpan did not throw");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[UseAfterDispose] Managed exception: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[UseAfterDispose] Outer managed exception: {ex.Message}");
        }

        // Naughty: Double-dispose scenario
        try
        {
            var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY);
            scatter.Dispose();
            try
            {
                scatter.Dispose();
                _output.WriteLine("[DoubleDispose] Second dispose did not throw");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[DoubleDispose] Managed exception: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[DoubleDispose] Outer managed exception: {ex.Message}");
        }

        // Naughty: Concurrent PrepareRead/Execute/ReadSpan on the same instance
        try
        {
            var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY);
            var tasks = new List<Thread>();
            for (int i = 0; i < 8; i++)
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        var rand = new Random(Guid.NewGuid().GetHashCode());
                        ulong addr = (ulong)rand.Next(0, int.MaxValue) & ~0xFFFUL;
                        int size = 0x1000 * (1 + rand.Next(0, 4));
                        for (int j = 0; j < 10; j++)
                        {
                            int op = rand.Next(0, 3);
                            try
                            {
                                switch (op)
                                {
                                    case 0:
                                        scatter.PrepareRead(addr, size);
                                        break;
                                    case 1:
                                        scatter.Execute();
                                        break;
                                    case 2:
                                        var buffer = new byte[0x1000];
                                        scatter.ReadSpan(addr, buffer);
                                        break;
                                }
                            }
                            catch (Exception ex)
                            {
                                _output.WriteLine($"[ConcurrentOps] Managed exception: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"[ConcurrentOps] Outer managed exception: {ex.Message}");
                    }
                });
                tasks.Add(t);
            }
            tasks.ForEach(t => t.Start());
            tasks.ForEach(t => t.Join());
            scatter.Dispose();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[ConcurrentOps] Outer managed exception: {ex.Message}");
        }

        // Naughty: Invalid address/size usage
        try
        {
            var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY);
            try
            {
                scatter.PrepareRead(0xFFFFFFFFFFFFFFFF, 0x1000); // Invalid address
                _output.WriteLine("[InvalidAddr] PrepareRead did not throw");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[InvalidAddr] Managed exception: {ex.Message}");
            }
            try
            {
                scatter.PrepareRead(0, int.MaxValue); // Oversized read
                _output.WriteLine("[InvalidSize] PrepareRead did not throw");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[InvalidSize] Managed exception: {ex.Message}");
            }
            try
            {
                scatter.PrepareRead(0, -1); // Negative size (should be invalid)
                _output.WriteLine("[NegativeSize] PrepareRead did not throw");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[NegativeSize] Managed exception: {ex.Message}");
            }
            scatter.Dispose();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[InvalidAddr/Size] Outer managed exception: {ex.Message}");
        }

        if (errors.Count > 0)
        {
            throw new AggregateException(errors);
        }
    }
}
