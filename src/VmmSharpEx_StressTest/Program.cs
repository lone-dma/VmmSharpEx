using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using VmmSharpEx;
using VmmSharpEx.Options;
using VmmSharpEx.Scatter;

namespace VmmSharpEx_StressTest;

internal static class Program
{
    private static Vmm _vmm = null!;
    private static ulong Heap;
    private static int HeapLen;
    private const int THREAD_COUNT = 8;
    private const int ITERATIONS_PER_TEST = 100;

    private static readonly ConcurrentBag<Exception> _exceptions = [];
    private static volatile int _totalOperations;
    private static volatile int _successfulOperations;
    private static volatile int _failedOperations;



    static void Main()
    {
        Console.WriteLine("=== VmmSharpEx AOT Stress Test (Infinite Loop) ===");
        Console.WriteLine($"Threads: {THREAD_COUNT}, Iterations per test: {ITERATIONS_PER_TEST}");
        Console.WriteLine("Press Ctrl+C or close window to stop.");
        Console.WriteLine();

        InitDMA();
        using (_vmm)
        {
            int iteration = 0;
            var totalSw = Stopwatch.StartNew();
            var reportSw = Stopwatch.StartNew();

            while (true)
            {
                iteration++;

                // Run all stress tests silently
                RunStressTest(StressTestBasicMemoryReadWrite);
                RunStressTest(StressTestScatterManagedConcurrent);
                RunStressTest(StressTestScatterCreateDisposeRapid);
                RunStressTest(StressTestMemSearchStartStopAbort);
                RunStressTest(StressTestLeechCoreDirect);
                RunStressTest(StressTestMixedConcurrent);
                RunStressTest(StressTestScatterWithResetCycles);
                RunStressTest(StressTestConcurrentValueTypeReads);
                RunStressTest(StressTestSpanAndArrayOperations);
                RunStressTest(StressTestPooledMemoryOperations);

                // Additional edge case and randomized tests
                RunStressTest(StressTestRandomizedReadSizes);
                RunStressTest(StressTestPageBoundaryEdgeCases);
                RunStressTest(StressTestScatterOverlappingAddresses);
                RunStressTest(StressTestInvalidAddressGracefulFailure);
                RunStressTest(StressTestRandomizedScatterPatterns);
                RunStressTest(StressTestStringEncodings);
                RunStressTest(StressTestScatterFlagCombinations);
                RunStressTest(StressTestMemoryPrefetch);
                RunStressTest(StressTestWriteVerification);
                RunStressTest(StressTestLargeBlockOperations);
                RunStressTest(StressTestRapidSearchCancellation);
                RunStressTest(StressTestScatterStringReads);
                RunStressTest(StressTestChaosMonkey);

                // Only print status every ~10 seconds
                if (reportSw.Elapsed.TotalSeconds >= 10)
                {
                    reportSw.Restart();

                    Console.WriteLine($"[{totalSw.Elapsed:hh\\:mm\\:ss}] Iterations: {iteration:N0} | Ops: {_totalOperations:N0} | Success: {_successfulOperations:N0} | Failed: {_failedOperations:N0} | Exceptions: {_exceptions.Count:N0}");

                    if (_exceptions.Count > 0)
                    {
                        var lastEx = _exceptions.LastOrDefault();
                        if (lastEx is not null)
                            Console.WriteLine($"  Last Exception: {lastEx.GetType().Name}: {lastEx.Message}");
                    }
                }
            }
        }
    }

    private static void RunStressTest(Action testAction)
    {
        try
        {
            testAction();
        }
        catch (Exception ex)
        {
            _exceptions.Add(ex);
        }
    }

    #region Stress Tests

    private static void StressTestBasicMemoryReadWrite()
    {
        Parallel.For(0, THREAD_COUNT, _ =>
        {
            for (int i = 0; i < ITERATIONS_PER_TEST; i++)
            {
                try
                {
                    Interlocked.Increment(ref _totalOperations);

                    // Read value types
                    if (_vmm.MemReadValue<ulong>(Vmm.PID_PHYSICALMEMORY, Heap, out var val))
                    {
                        Interlocked.Increment(ref _successfulOperations);
                    }
                    else
                    {
                        Interlocked.Increment(ref _failedOperations);
                    }

                    Interlocked.Increment(ref _totalOperations);

                    // Read bytes
                    var bytes = _vmm.MemRead(Vmm.PID_PHYSICALMEMORY, Heap, 64, out var cbRead);
                    if (bytes is not null && cbRead > 0)
                    {
                        Interlocked.Increment(ref _successfulOperations);
                    }
                    else
                    {
                        Interlocked.Increment(ref _failedOperations);
                    }

                    Interlocked.Increment(ref _totalOperations);

                    // Write and read back
                    var testValue = (ulong)(Environment.TickCount64 + i);
                    var writeAddr = Heap + 0x100 + (ulong)(i % 0x100);
                    if (_vmm.MemWriteValue(Vmm.PID_PHYSICALMEMORY, writeAddr, testValue))
                    {
                        if (_vmm.MemReadValue<ulong>(Vmm.PID_PHYSICALMEMORY, writeAddr, out var readBack) && readBack == testValue)
                        {
                            Interlocked.Increment(ref _successfulOperations);
                        }
                        else
                        {
                            Interlocked.Increment(ref _failedOperations);
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref _failedOperations);
                    }
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestScatterManagedConcurrent()
    {
        Parallel.For(0, THREAD_COUNT, _ =>
        {
            for (int i = 0; i < ITERATIONS_PER_TEST; i++)
            {
                try
                {
                    using var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY);

                    // Prepare multiple reads
                    for (int j = 0; j < 16; j++)
                    {
                        var addr = Heap + (ulong)(j * 8);
                        scatter.PrepareReadValue<ulong>(addr);
                    }

                    // Prepare array reads
                    scatter.PrepareReadArray<byte>(Heap, 256);
                    scatter.PrepareRead(Heap + 0x1000, 512);

                    Interlocked.Increment(ref _totalOperations);

                    // Execute
                    scatter.Execute();

                    // Read results
                    int successCount = 0;
                    for (int j = 0; j < 16; j++)
                    {
                        var addr = Heap + (ulong)(j * 8);
                        if (scatter.ReadValue<ulong>(addr, out var scatterVal))
                        {
                            successCount++;
                        }
                    }

                    var arrResult = scatter.ReadArray<byte>(Heap, 256);
                    if (arrResult is not null)
                        successCount++;

                    var rawResult = scatter.Read(Heap + 0x1000, 512);
                    if (rawResult is not null)
                        successCount++;

                    if (successCount > 0)
                    {
                        Interlocked.Increment(ref _successfulOperations);
                    }
                    else
                    {
                        Interlocked.Increment(ref _failedOperations);
                    }
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestScatterCreateDisposeRapid()
    {
        Parallel.For(0, THREAD_COUNT, _ =>
        {
            for (int i = 0; i < ITERATIONS_PER_TEST * 5; i++)
            {
                try
                {
                    Interlocked.Increment(ref _totalOperations);

                    // Rapid create/prepare/execute/dispose cycle
                    using (var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY, VmmFlags.NOCACHE))
                    {
                        scatter.PrepareReadValue<ulong>(Heap);
                        scatter.PrepareReadValue<int>(Heap + 8);
                        scatter.PrepareReadValue<short>(Heap + 16);
                        scatter.PrepareReadValue<byte>(Heap + 24);
                        scatter.Execute();

                        if (scatter.ReadValue<ulong>(Heap, out var rapidVal))
                        {
                            Interlocked.Increment(ref _successfulOperations);
                        }
                        else
                        {
                            Interlocked.Increment(ref _failedOperations);
                        }
                    }
                    // Dispose called here
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestMemSearchStartStopAbort()
    {
        var searchBytes = new byte[] { 0x48, 0x8B, 0xC4 }; // Common x64 prologue pattern
        var searchItem = new VmmSearch.SearchItem(searchBytes, align: 1);

        Parallel.For(0, THREAD_COUNT, threadIdx =>
        {
            for (int i = 0; i < ITERATIONS_PER_TEST / 10; i++)
            {
                try
                {
                    Interlocked.Increment(ref _totalOperations);

                    using var cts = new CancellationTokenSource();

                    // Start search and abort quickly
                    var searchTask = _vmm.MemSearchAsync(
                        Vmm.PID_PHYSICALMEMORY,
                        [searchItem],
                        addr_min: Heap,
                        addr_max: Heap + 0x10000,
                        cMaxResult: 5,
                        ct: cts.Token);

                    // Random chance to cancel
                    if ((i + threadIdx) % 3 == 0)
                    {
                        Thread.Sleep(1); // Small delay
                        cts.Cancel();
                    }

                    try
                    {
                        var result = searchTask.GetAwaiter().GetResult();
                        Interlocked.Increment(ref _successfulOperations);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancelled
                        Interlocked.Increment(ref _successfulOperations);
                    }
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestLeechCoreDirect()
    {
        Parallel.For(0, THREAD_COUNT, _ =>
        {
            for (int i = 0; i < ITERATIONS_PER_TEST; i++)
            {
                try
                {
                    Interlocked.Increment(ref _totalOperations);

                    // Direct LeechCore reads
                    var lc = _vmm.LeechCore;
                    var data = lc.Read(Heap, 64);
                    if (data is not null)
                    {
                        Interlocked.Increment(ref _successfulOperations);
                    }
                    else
                    {
                        Interlocked.Increment(ref _failedOperations);
                    }

                    Interlocked.Increment(ref _totalOperations);

                    // ReadValue through LeechCore
                    if (lc.ReadValue<ulong>(Heap, out var val))
                    {
                        Interlocked.Increment(ref _successfulOperations);
                    }
                    else
                    {
                        Interlocked.Increment(ref _failedOperations);
                    }

                    Interlocked.Increment(ref _totalOperations);

                    // ReadArray through LeechCore
                    var arr = lc.ReadArray<int>(Heap, 16);
                    if (arr is not null && arr.Length == 16)
                    {
                        Interlocked.Increment(ref _successfulOperations);
                    }
                    else
                    {
                        Interlocked.Increment(ref _failedOperations);
                    }

                    Interlocked.Increment(ref _totalOperations);

                    // ReadSpan through LeechCore
                    Span<byte> buffer = stackalloc byte[128];
                    if (lc.ReadSpan(Heap, buffer))
                    {
                        Interlocked.Increment(ref _successfulOperations);
                    }
                    else
                    {
                        Interlocked.Increment(ref _failedOperations);
                    }
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestMixedConcurrent()
    {
        // This test does everything at once to stress the system
        var tasks = new Task[THREAD_COUNT];

        for (int t = 0; t < THREAD_COUNT; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                var random = new Random(threadId * 12345);

                for (int i = 0; i < ITERATIONS_PER_TEST; i++)
                {
                    try
                    {
                        int operation = random.Next(6);

                        switch (operation)
                        {
                            case 0: // Basic read
                                Interlocked.Increment(ref _totalOperations);
                                if (_vmm.MemReadValue<ulong>(Vmm.PID_PHYSICALMEMORY, Heap, out _))
                                    Interlocked.Increment(ref _successfulOperations);
                                else
                                    Interlocked.Increment(ref _failedOperations);
                                break;

                            case 1: // Basic write
                                Interlocked.Increment(ref _totalOperations);
                                if (_vmm.MemWriteValue(Vmm.PID_PHYSICALMEMORY, Heap + 0x200, (ulong)i))
                                    Interlocked.Increment(ref _successfulOperations);
                                else
                                    Interlocked.Increment(ref _failedOperations);
                                break;

                            case 2: // Scatter read
                                Interlocked.Increment(ref _totalOperations);
                                using (var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY))
                                {
                                    scatter.PrepareReadValue<ulong>(Heap);
                                    scatter.Execute();
                                    if (scatter.ReadValue<ulong>(Heap, out _))
                                        Interlocked.Increment(ref _successfulOperations);
                                    else
                                        Interlocked.Increment(ref _failedOperations);
                                }
                                break;

                            case 3: // LeechCore read
                                Interlocked.Increment(ref _totalOperations);
                                if (_vmm.LeechCore.ReadValue<ulong>(Heap, out _))
                                    Interlocked.Increment(ref _successfulOperations);
                                else
                                    Interlocked.Increment(ref _failedOperations);
                                break;

                            case 4: // String read (Unicode)
                                Interlocked.Increment(ref _totalOperations);
                                var str = _vmm.MemReadString(Vmm.PID_PHYSICALMEMORY, Heap, 64, Encoding.Unicode);
                                if (str is not null)
                                    Interlocked.Increment(ref _successfulOperations);
                                else
                                    Interlocked.Increment(ref _failedOperations);
                                break;

                            case 5: // Pooled read
                                Interlocked.Increment(ref _totalOperations);
                                using (var pooled = _vmm.MemReadPooled<byte>(Vmm.PID_PHYSICALMEMORY, Heap, 256))
                                {
                                    if (pooled is not null)
                                        Interlocked.Increment(ref _successfulOperations);
                                    else
                                        Interlocked.Increment(ref _failedOperations);
                                }
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _exceptions.Add(ex);
                        Interlocked.Increment(ref _failedOperations);
                    }
                }
            });
        }

        Task.WaitAll(tasks);
    }

    private static void StressTestScatterWithResetCycles()
    {
        Parallel.For(0, THREAD_COUNT, _ =>
        {
            try
            {
                using var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY);

                for (int cycle = 0; cycle < ITERATIONS_PER_TEST; cycle++)
                {
                    Interlocked.Increment(ref _totalOperations);

                    // Reset and reprepare
                    scatter.Reset();

                    // Prepare new set of reads
                    for (int j = 0; j < 8; j++)
                    {
                        scatter.PrepareReadValue<ulong>(Heap + (ulong)(j * 16));
                    }

                    scatter.Execute();

                    // Read all
                    int success = 0;
                    for (int j = 0; j < 8; j++)
                    {
                        if (scatter.ReadValue<ulong>(Heap + (ulong)(j * 16), out var cycleVal))
                            success++;
                    }

                    if (success > 0)
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);
                }
            }
            catch (Exception ex)
            {
                _exceptions.Add(ex);
                Interlocked.Increment(ref _failedOperations);
            }
        });
    }

    private static void StressTestConcurrentValueTypeReads()
    {
        Parallel.For(0, THREAD_COUNT, _ =>
        {
            for (int i = 0; i < ITERATIONS_PER_TEST; i++)
            {
                try
                {
                    // Test various unmanaged types for AOT compatibility
                    TestValueTypeRead<byte>();
                    TestValueTypeRead<sbyte>();
                    TestValueTypeRead<short>();
                    TestValueTypeRead<ushort>();
                    TestValueTypeRead<int>();
                    TestValueTypeRead<uint>();
                    TestValueTypeRead<long>();
                    TestValueTypeRead<ulong>();
                    TestValueTypeRead<float>();
                    TestValueTypeRead<double>();
                    TestValueTypeRead<nint>();
                    TestValueTypeRead<nuint>();
                    TestValueTypeRead<Guid>();
                    TestValueTypeRead<TestStruct>();
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TestValueTypeRead<T>() where T : unmanaged
    {
        Interlocked.Increment(ref _totalOperations);
        if (_vmm.MemReadValue<T>(Vmm.PID_PHYSICALMEMORY, Heap, out _))
            Interlocked.Increment(ref _successfulOperations);
        else
            Interlocked.Increment(ref _failedOperations);
    }

    private static void StressTestSpanAndArrayOperations()
    {
        Parallel.For(0, THREAD_COUNT, _ =>
        {
            for (int i = 0; i < ITERATIONS_PER_TEST; i++)
            {
                try
                {
                    // MemReadSpan with stack-allocated buffer
                    Interlocked.Increment(ref _totalOperations);
                    Span<byte> stackBuffer = stackalloc byte[256];
                    if (_vmm.MemReadSpan(Vmm.PID_PHYSICALMEMORY, Heap, stackBuffer))
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);

                    // MemReadArray
                    Interlocked.Increment(ref _totalOperations);
                    var arr = _vmm.MemReadArray<int>(Vmm.PID_PHYSICALMEMORY, Heap, 32);
                    if (arr is not null && arr.Length == 32)
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);

                    // MemWriteSpan
                    Interlocked.Increment(ref _totalOperations);
                    Span<byte> writeData = stackalloc byte[16];
                    for (int j = 0; j < 16; j++) writeData[j] = (byte)(i + j);
                    if (_vmm.MemWriteSpan(Vmm.PID_PHYSICALMEMORY, Heap + 0x300, writeData))
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);

                    // MemWriteArray
                    Interlocked.Increment(ref _totalOperations);
                    var writeArr = new int[] { i, i + 1, i + 2, i + 3 };
                    if (_vmm.MemWriteArray(Vmm.PID_PHYSICALMEMORY, Heap + 0x400, writeArr))
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);

                    // Scatter ReadSpan
                    Interlocked.Increment(ref _totalOperations);
                    using (var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY))
                    {
                        scatter.PrepareRead(Heap, 128);
                        scatter.Execute();
                        Span<byte> resultSpan = stackalloc byte[128];
                        if (scatter.ReadSpan(Heap, resultSpan))
                            Interlocked.Increment(ref _successfulOperations);
                        else
                            Interlocked.Increment(ref _failedOperations);
                    }
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestPooledMemoryOperations()
    {
        Parallel.For(0, THREAD_COUNT, _ =>
        {
            for (int i = 0; i < ITERATIONS_PER_TEST; i++)
            {
                try
                {
                    // MemReadPooled<byte>
                    Interlocked.Increment(ref _totalOperations);
                    using (var pooled = _vmm.MemReadPooled<byte>(Vmm.PID_PHYSICALMEMORY, Heap, 512))
                    {
                        if (pooled is not null && pooled.Memory.Length == 512)
                            Interlocked.Increment(ref _successfulOperations);
                        else
                            Interlocked.Increment(ref _failedOperations);
                    }

                    // MemReadPooled<int>
                    Interlocked.Increment(ref _totalOperations);
                    using (var pooled = _vmm.MemReadPooled<int>(Vmm.PID_PHYSICALMEMORY, Heap, 64))
                    {
                        if (pooled is not null && pooled.Memory.Length == 64)
                            Interlocked.Increment(ref _successfulOperations);
                        else
                            Interlocked.Increment(ref _failedOperations);
                    }

                    // MemReadPooled<ulong>
                    Interlocked.Increment(ref _totalOperations);
                    using (var pooled = _vmm.MemReadPooled<ulong>(Vmm.PID_PHYSICALMEMORY, Heap, 32))
                    {
                        if (pooled is not null && pooled.Memory.Length == 32)
                            Interlocked.Increment(ref _successfulOperations);
                        else
                            Interlocked.Increment(ref _failedOperations);
                    }

                    // LeechCore ReadPooled
                    Interlocked.Increment(ref _totalOperations);
                    using (var pooled = _vmm.LeechCore.ReadPooled<byte>(Heap, 256))
                    {
                        if (pooled is not null && pooled.Memory.Length == 256)
                            Interlocked.Increment(ref _successfulOperations);
                        else
                            Interlocked.Increment(ref _failedOperations);
                    }

                    // Scatter ReadPooled
                    Interlocked.Increment(ref _totalOperations);
                    using (var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY))
                    {
                        scatter.PrepareReadArray<long>(Heap, 16);
                        scatter.Execute();
                        using (var pooled = scatter.ReadPooled<long>(Heap, 16))
                        {
                            if (pooled is not null && pooled.Memory.Length == 16)
                                Interlocked.Increment(ref _successfulOperations);
                            else
                                Interlocked.Increment(ref _failedOperations);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    #endregion

    #region Edge Case and Randomized Tests

    private static void StressTestRandomizedReadSizes()
    {
        Parallel.For(0, THREAD_COUNT, threadIdx =>
        {
            var random = new Random(Environment.TickCount + threadIdx);

            for (int i = 0; i < ITERATIONS_PER_TEST; i++)
            {
                try
                {
                    // Random read sizes from 1 byte to 4KB
                    int[] sizes = [1, 2, 3, 4, 7, 8, 15, 16, 31, 32, 63, 64, 127, 128, 255, 256, 511, 512, 1023, 1024, 2048, 4096];
                    int size = sizes[random.Next(sizes.Length)];
                    ulong offset = (ulong)random.Next(0, Math.Max(1, HeapLen - size));

                    Interlocked.Increment(ref _totalOperations);
                    var data = _vmm.MemRead(Vmm.PID_PHYSICALMEMORY, Heap + offset, (uint)size, out var cbRead);
                    if (data is not null && cbRead == size)
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);

                    // Random span read
                    Interlocked.Increment(ref _totalOperations);
                    var buffer = new byte[size];
                    if (_vmm.MemReadSpan(Vmm.PID_PHYSICALMEMORY, Heap + offset, buffer.AsSpan()))
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestPageBoundaryEdgeCases()
    {
        Parallel.For(0, THREAD_COUNT, _ =>
        {
            for (int i = 0; i < ITERATIONS_PER_TEST; i++)
            {
                try
                {
                    // Page-aligned base
                    ulong pageBase = Heap & ~0xFFFUL;

                    // Read crossing page boundary (last byte of page + first bytes of next)
                    Interlocked.Increment(ref _totalOperations);
                    ulong crossBoundaryAddr = pageBase + 0xFFC; // 4 bytes before page end
                    var crossData = _vmm.MemRead(Vmm.PID_PHYSICALMEMORY, crossBoundaryAddr, 16, out var cb1); // crosses into next page
                    if (crossData is not null)
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);

                    // Read exactly at page boundary
                    Interlocked.Increment(ref _totalOperations);
                    if (_vmm.MemReadValue<ulong>(Vmm.PID_PHYSICALMEMORY, pageBase + 0x1000, out var pageBoundaryVal))
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);

                    // Single byte reads at various offsets within page
                    int[] offsets = [0, 1, 0xFF, 0x100, 0x7FF, 0x800, 0xFFE, 0xFFF];
                    foreach (var off in offsets)
                    {
                        Interlocked.Increment(ref _totalOperations);
                        if (_vmm.MemReadValue<byte>(Vmm.PID_PHYSICALMEMORY, pageBase + (ulong)off, out var offsetVal))
                            Interlocked.Increment(ref _successfulOperations);
                        else
                            Interlocked.Increment(ref _failedOperations);
                    }

                    // Scatter across page boundary
                    Interlocked.Increment(ref _totalOperations);
                    using var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY);
                    scatter.PrepareRead(pageBase + 0xFF0, 32); // Crosses page
                    scatter.Execute();
                    var result = scatter.Read(pageBase + 0xFF0, 32);
                    if (result is not null)
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestScatterOverlappingAddresses()
    {
        Parallel.For(0, THREAD_COUNT, _ =>
        {
            for (int i = 0; i < ITERATIONS_PER_TEST; i++)
            {
                try
                {
                    using var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY);

                    // Prepare overlapping reads (same page, different offsets)
                    scatter.PrepareRead(Heap, 64);
                    scatter.PrepareRead(Heap + 32, 64);  // Overlaps with first
                    scatter.PrepareRead(Heap + 48, 64);  // Overlaps with both
                    scatter.PrepareReadValue<ulong>(Heap);
                    scatter.PrepareReadValue<ulong>(Heap + 4); // Overlapping value read
                    scatter.PrepareReadArray<int>(Heap, 32);
                    scatter.PrepareReadArray<int>(Heap + 8, 32); // Overlapping array

                    Interlocked.Increment(ref _totalOperations);
                    scatter.Execute();

                    int success = 0;
                    if (scatter.Read(Heap, 64) is not null) success++;
                    if (scatter.Read(Heap + 32, 64) is not null) success++;
                    if (scatter.ReadValue<ulong>(Heap, out var overlapVal)) success++;
                    if (scatter.ReadArray<int>(Heap, 32) is not null) success++;

                    if (success > 0)
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestInvalidAddressGracefulFailure()
    {
        Parallel.For(0, THREAD_COUNT, threadIdx =>
        {
            var random = new Random(Environment.TickCount + threadIdx);

            for (int i = 0; i < ITERATIONS_PER_TEST / 5; i++)
            {
                try
                {
                    // Test with obviously invalid addresses - should fail gracefully, not crash
                    ulong[] invalidAddresses =
                    [
                        0x0,
                        0x1,
                        0xDEADBEEF,
                        0xFFFFFFFFFFFFFFFF,
                        0x7FFFFFFFFFFFFFFF,
                        0x8000000000000000,
                        (ulong)random.NextInt64(),
                    ];

                    foreach (var addr in invalidAddresses)
                    {
                        Interlocked.Increment(ref _totalOperations);
                        // These should fail gracefully (return false/null), not throw
                        _ = _vmm.MemReadValue<ulong>(Vmm.PID_PHYSICALMEMORY, addr, out _);
                        Interlocked.Increment(ref _successfulOperations); // Success = didn't crash
                    }

                    // Zero-size operations (edge case)
                    Interlocked.Increment(ref _totalOperations);
                    var emptyResult = _vmm.MemReadArray<byte>(Vmm.PID_PHYSICALMEMORY, Heap, 0);
                    Interlocked.Increment(ref _successfulOperations);

                    // Scatter with no preparations
                    Interlocked.Increment(ref _totalOperations);
                    using var emptyScatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY);
                    emptyScatter.Execute(); // Should be no-op
                    Interlocked.Increment(ref _successfulOperations);
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestRandomizedScatterPatterns()
    {
        Parallel.For(0, THREAD_COUNT, threadIdx =>
        {
            var random = new Random(Environment.TickCount + threadIdx);

            for (int i = 0; i < ITERATIONS_PER_TEST; i++)
            {
                try
                {
                    using var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY);

                    // Random number of preparations (1-50)
                    int prepCount = random.Next(1, 51);
                    var addresses = new List<(ulong addr, int size)>();

                    for (int p = 0; p < prepCount; p++)
                    {
                        ulong offset = (ulong)random.Next(0, Math.Max(1, HeapLen - 256));
                        int size = random.Next(1, 257);
                        addresses.Add((Heap + offset, size));
                        scatter.PrepareRead(Heap + offset, size);
                    }

                    Interlocked.Increment(ref _totalOperations);
                    scatter.Execute();

                    // Verify random subset of reads
                    int verifyCount = Math.Min(10, addresses.Count);
                    int successCount = 0;
                    for (int v = 0; v < verifyCount; v++)
                    {
                        var (addr, size) = addresses[random.Next(addresses.Count)];
                        if (scatter.Read(addr, size) is not null)
                            successCount++;
                    }

                    if (successCount > 0)
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestStringEncodings()
    {
        Parallel.For(0, THREAD_COUNT, threadIdx =>
        {
            var random = new Random(Environment.TickCount + threadIdx);

            for (int i = 0; i < ITERATIONS_PER_TEST; i++)
            {
                try
                {
                    ulong offset = (ulong)random.Next(0, Math.Max(1, HeapLen - 256));
                    int[] lengths = [1, 2, 4, 8, 16, 32, 64, 128, 255];
                    int len = lengths[random.Next(lengths.Length)];

                    // Test all common encodings
                    Encoding[] encodings = [Encoding.UTF8, Encoding.Unicode, Encoding.ASCII, Encoding.UTF32];

                    foreach (var encoding in encodings)
                    {
                        Interlocked.Increment(ref _totalOperations);
                        var str = _vmm.MemReadString(Vmm.PID_PHYSICALMEMORY, Heap + offset, len, encoding);
                        // String read should not crash regardless of memory content
                        Interlocked.Increment(ref _successfulOperations);
                    }

                    // Scatter string reads
                    Interlocked.Increment(ref _totalOperations);
                    using var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY);
                    scatter.PrepareRead(Heap + offset, len);
                    scatter.Execute();
                    var scatterStr = scatter.ReadString(Heap + offset, len, Encoding.Unicode);
                    Interlocked.Increment(ref _successfulOperations);
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestScatterFlagCombinations()
    {
        VmmFlags[] flagCombinations =
        [
            VmmFlags.NONE,
            VmmFlags.NOCACHE,
            VmmFlags.ZEROPAD_ON_FAIL,
            VmmFlags.NOCACHE | VmmFlags.ZEROPAD_ON_FAIL,
            VmmFlags.NOPAGING,
            VmmFlags.NOPAGING_IO,
            VmmFlags.NOCACHEPUT,
            VmmFlags.CACHE_RECENT_ONLY,
            VmmFlags.NO_PREDICTIVE_READ,
            VmmFlags.SCATTER_FORCE_PAGEREAD,
            VmmFlags.NOCACHE | VmmFlags.NO_PREDICTIVE_READ,
        ];

        Parallel.For(0, THREAD_COUNT, threadIdx =>
        {
            var random = new Random(Environment.TickCount + threadIdx);

            for (int i = 0; i < ITERATIONS_PER_TEST; i++)
            {
                try
                {
                    var flags = flagCombinations[random.Next(flagCombinations.Length)];

                    Interlocked.Increment(ref _totalOperations);
                    using var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY, flags);

                    for (int j = 0; j < 8; j++)
                    {
                        scatter.PrepareReadValue<ulong>(Heap + (ulong)(j * 8));
                    }

                    scatter.Execute();

                    int success = 0;
                    for (int j = 0; j < 8; j++)
                    {
                        if (scatter.ReadValue<ulong>(Heap + (ulong)(j * 8), out _))
                            success++;
                    }

                    if (success > 0)
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestMemoryPrefetch()
    {
        Parallel.For(0, THREAD_COUNT, threadIdx =>
        {
            var random = new Random(Environment.TickCount + threadIdx);

            for (int i = 0; i < ITERATIONS_PER_TEST; i++)
            {
                try
                {
                    // Generate random page addresses to prefetch
                    int pageCount = random.Next(1, 17);
                    var pages = new ulong[pageCount];
                    for (int p = 0; p < pageCount; p++)
                    {
                        ulong offset = (ulong)(random.Next(0, Math.Max(1, HeapLen / 0x1000)) * 0x1000);
                        pages[p] = Heap + offset;
                    }

                    Interlocked.Increment(ref _totalOperations);
                    if (_vmm.MemPrefetchPages(Vmm.PID_PHYSICALMEMORY, pages))
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);

                    // Read the prefetched pages
                    foreach (var page in pages)
                    {
                        Interlocked.Increment(ref _totalOperations);
                        if (_vmm.MemReadValue<ulong>(Vmm.PID_PHYSICALMEMORY, page, out _))
                            Interlocked.Increment(ref _successfulOperations);
                        else
                            Interlocked.Increment(ref _failedOperations);
                    }
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestWriteVerification()
    {
        Parallel.For(0, THREAD_COUNT, threadIdx =>
        {
            var random = new Random(Environment.TickCount + threadIdx);

            for (int i = 0; i < ITERATIONS_PER_TEST; i++)
            {
                try
                {
                    // Use thread-specific offset to avoid collisions
                    ulong baseOffset = (ulong)(threadIdx * 0x100 + 0x500);

                    // Write random patterns and verify
                    Interlocked.Increment(ref _totalOperations);
                    ulong pattern1 = (ulong)random.NextInt64();
                    if (_vmm.MemWriteValue(Vmm.PID_PHYSICALMEMORY, Heap + baseOffset, pattern1) &&
                        _vmm.MemReadValue<ulong>(Vmm.PID_PHYSICALMEMORY, Heap + baseOffset, out var read1) &&
                        read1 == pattern1)
                    {
                        Interlocked.Increment(ref _successfulOperations);
                    }
                    else
                    {
                        Interlocked.Increment(ref _failedOperations);
                    }

                    // Write array and verify
                    Interlocked.Increment(ref _totalOperations);
                    var writeArr = new int[4];
                    for (int j = 0; j < 4; j++) writeArr[j] = random.Next();
                    if (_vmm.MemWriteArray(Vmm.PID_PHYSICALMEMORY, Heap + baseOffset + 0x10, writeArr))
                    {
                        var readArr = _vmm.MemReadArray<int>(Vmm.PID_PHYSICALMEMORY, Heap + baseOffset + 0x10, 4);
                        if (readArr is not null && readArr.SequenceEqual(writeArr))
                            Interlocked.Increment(ref _successfulOperations);
                        else
                            Interlocked.Increment(ref _failedOperations);
                    }
                    else
                    {
                        Interlocked.Increment(ref _failedOperations);
                    }

                    // Write span and verify
                    Interlocked.Increment(ref _totalOperations);
                    Span<byte> writeSpan = stackalloc byte[32];
                    random.NextBytes(writeSpan);
                    var writeSpanCopy = writeSpan.ToArray();
                    if (_vmm.MemWriteSpan(Vmm.PID_PHYSICALMEMORY, Heap + baseOffset + 0x20, writeSpan))
                    {
                        Span<byte> readSpan = stackalloc byte[32];
                        if (_vmm.MemReadSpan(Vmm.PID_PHYSICALMEMORY, Heap + baseOffset + 0x20, readSpan) &&
                            readSpan.SequenceEqual(writeSpanCopy))
                        {
                            Interlocked.Increment(ref _successfulOperations);
                        }
                        else
                        {
                            Interlocked.Increment(ref _failedOperations);
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref _failedOperations);
                    }
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestLargeBlockOperations()
    {
        Parallel.For(0, THREAD_COUNT, _ =>
        {
            for (int i = 0; i < ITERATIONS_PER_TEST / 10; i++)
            {
                try
                {
                    // Large contiguous reads
                    int[] largeSizes = [0x1000, 0x2000, 0x4000, 0x8000, 0x10000];
                    foreach (var size in largeSizes)
                    {
                        if (size > HeapLen) continue;

                        Interlocked.Increment(ref _totalOperations);
                        var data = _vmm.MemRead(Vmm.PID_PHYSICALMEMORY, Heap, (uint)size, out var cbRead);
                        if (data is not null && cbRead > 0)
                            Interlocked.Increment(ref _successfulOperations);
                        else
                            Interlocked.Increment(ref _failedOperations);
                    }

                    // Large scatter operations
                    Interlocked.Increment(ref _totalOperations);
                    using var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY);

                    // Prepare many pages
                    int pageCount = Math.Min(16, HeapLen / 0x1000);
                    for (int p = 0; p < pageCount; p++)
                    {
                        scatter.PrepareRead(Heap + (ulong)(p * 0x1000), 0x1000);
                    }

                    scatter.Execute();

                    int success = 0;
                    for (int p = 0; p < pageCount; p++)
                    {
                        if (scatter.Read(Heap + (ulong)(p * 0x1000), 0x1000) is not null)
                            success++;
                    }

                    if (success > 0)
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);

                    // Large array reads
                    Interlocked.Increment(ref _totalOperations);
                    var largeArray = _vmm.MemReadArray<ulong>(Vmm.PID_PHYSICALMEMORY, Heap, Math.Min(512, HeapLen / 8));
                    if (largeArray is not null)
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestRapidSearchCancellation()
    {
        var searchPatterns = new byte[][]
        {
            [0x00],
            [0xFF],
            [0x48, 0x89],
            [0x90, 0x90, 0x90],
            [0xCC, 0xCC],
            [0x00, 0x00, 0x00, 0x00],
        };

        Parallel.For(0, THREAD_COUNT, threadIdx =>
        {
            var random = new Random(Environment.TickCount + threadIdx);

            for (int i = 0; i < ITERATIONS_PER_TEST / 5; i++)
            {
                try
                {
                    var pattern = searchPatterns[random.Next(searchPatterns.Length)];
                    var searchItem = new VmmSearch.SearchItem(pattern, align: (uint)random.Next(1, 9));

                    Interlocked.Increment(ref _totalOperations);

                    using var cts = new CancellationTokenSource();

                    // Random cancellation delay (0-10ms)
                    int cancelDelay = random.Next(0, 11);

                    var searchTask = _vmm.MemSearchAsync(
                        Vmm.PID_PHYSICALMEMORY,
                        [searchItem],
                        addr_min: Heap,
                        addr_max: Heap + (ulong)HeapLen,
                        cMaxResult: (uint)random.Next(1, 100),
                        ct: cts.Token);

                    if (cancelDelay > 0)
                    {
                        Thread.Sleep(cancelDelay);
                    }

                    // 50% chance to cancel
                    if (random.Next(2) == 0)
                    {
                        cts.Cancel();
                    }

                    try
                    {
                        var result = searchTask.GetAwaiter().GetResult();
                        Interlocked.Increment(ref _successfulOperations);
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Increment(ref _successfulOperations);
                    }
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestScatterStringReads()
    {
        Parallel.For(0, THREAD_COUNT, threadIdx =>
        {
            var random = new Random(Environment.TickCount + threadIdx);

            for (int i = 0; i < ITERATIONS_PER_TEST; i++)
            {
                try
                {
                    using var scatter = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY);

                    // Prepare multiple string reads at different offsets
                    var stringReads = new List<(ulong addr, int len, Encoding enc)>();
                    Encoding[] encodings = [Encoding.UTF8, Encoding.Unicode, Encoding.ASCII];

                    for (int s = 0; s < 5; s++)
                    {
                        ulong offset = (ulong)random.Next(0, Math.Max(1, HeapLen - 128));
                        int len = random.Next(8, 65);
                        var enc = encodings[random.Next(encodings.Length)];
                        stringReads.Add((Heap + offset, len, enc));
                        scatter.PrepareRead(Heap + offset, len);
                    }

                    Interlocked.Increment(ref _totalOperations);
                    scatter.Execute();

                    int success = 0;
                    foreach (var (addr, len, enc) in stringReads)
                    {
                        var str = scatter.ReadString(addr, len, enc);
                        if (str is not null)
                            success++;
                    }

                    if (success > 0)
                        Interlocked.Increment(ref _successfulOperations);
                    else
                        Interlocked.Increment(ref _failedOperations);
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    private static void StressTestChaosMonkey()
    {
        // This test randomly picks operations and parameters to maximize code path coverage
        Parallel.For(0, THREAD_COUNT, threadIdx =>
        {
            var random = new Random(Environment.TickCount + threadIdx);

            for (int i = 0; i < ITERATIONS_PER_TEST * 2; i++)
            {
                try
                {
                    int operation = random.Next(20);
                    ulong randomOffset = (ulong)random.Next(0, Math.Max(1, HeapLen));
                    int randomSize = random.Next(1, 513);

                    Interlocked.Increment(ref _totalOperations);

                    switch (operation)
                    {
                        case 0: // Random value type read
                            _ = _vmm.MemReadValue<byte>(Vmm.PID_PHYSICALMEMORY, Heap + randomOffset, out _);
                            break;
                        case 1:
                            _ = _vmm.MemReadValue<ushort>(Vmm.PID_PHYSICALMEMORY, Heap + randomOffset, out _);
                            break;
                        case 2:
                            _ = _vmm.MemReadValue<uint>(Vmm.PID_PHYSICALMEMORY, Heap + randomOffset, out _);
                            break;
                        case 3:
                            _ = _vmm.MemReadValue<ulong>(Vmm.PID_PHYSICALMEMORY, Heap + randomOffset, out _);
                            break;
                        case 4: // Random array read
                            _ = _vmm.MemReadArray<byte>(Vmm.PID_PHYSICALMEMORY, Heap + randomOffset, randomSize);
                            break;
                        case 5:
                            _ = _vmm.MemReadArray<int>(Vmm.PID_PHYSICALMEMORY, Heap + randomOffset, randomSize / 4 + 1);
                            break;
                        case 6: // Random span read
                            var buffer = new byte[randomSize];
                            _ = _vmm.MemReadSpan(Vmm.PID_PHYSICALMEMORY, Heap + randomOffset, buffer.AsSpan());
                            break;
                        case 7: // Random string read
                            _ = _vmm.MemReadString(Vmm.PID_PHYSICALMEMORY, Heap + randomOffset, randomSize, Encoding.UTF8);
                            break;
                        case 8:
                            _ = _vmm.MemReadString(Vmm.PID_PHYSICALMEMORY, Heap + randomOffset, randomSize, Encoding.Unicode);
                            break;
                        case 9: // Pooled read
                            using (var p = _vmm.MemReadPooled<byte>(Vmm.PID_PHYSICALMEMORY, Heap + randomOffset, randomSize))
                            { }
                            break;
                        case 10: // LeechCore read
                            _ = _vmm.LeechCore.Read(Heap + randomOffset, (uint)randomSize);
                            break;
                        case 11: // LeechCore value
                            _ = _vmm.LeechCore.ReadValue<ulong>(Heap + randomOffset, out _);
                            break;
                        case 12: // Quick scatter
                            using (var s = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY))
                            {
                                s.PrepareRead(Heap + randomOffset, randomSize);
                                s.Execute();
                                _ = s.Read(Heap + randomOffset, randomSize);
                            }
                            break;
                        case 13: // Scatter with reset
                            using (var s = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY))
                            {
                                s.PrepareReadValue<ulong>(Heap);
                                s.Execute();
                                s.Reset();
                                s.PrepareReadValue<uint>(Heap + randomOffset);
                                s.Execute();
                                _ = s.ReadValue<uint>(Heap + randomOffset, out _);
                            }
                            break;
                        case 14: // Write byte
                            _ = _vmm.MemWriteValue(Vmm.PID_PHYSICALMEMORY, Heap + 0x800 + (randomOffset % 0x100), (byte)random.Next(256));
                            break;
                        case 15: // Write ulong
                            _ = _vmm.MemWriteValue(Vmm.PID_PHYSICALMEMORY, Heap + 0x800 + (randomOffset % 0x100), (ulong)random.NextInt64());
                            break;
                        case 16: // Page read
                            _ = _vmm.MemReadPage(Vmm.PID_PHYSICALMEMORY, Heap & ~0xFFFUL);
                            break;
                        case 17: // Scatter with various types
                            using (var s = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY))
                            {
                                s.PrepareReadValue<byte>(Heap);
                                s.PrepareReadValue<short>(Heap + 1);
                                s.PrepareReadValue<int>(Heap + 3);
                                s.PrepareReadValue<long>(Heap + 7);
                                s.PrepareReadValue<Guid>(Heap + 15);
                                s.Execute();
                            }
                            break;
                        case 18: // Multiple overlapping scatters
                            using (var s = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY))
                            {
                                for (int j = 0; j < random.Next(5, 20); j++)
                                {
                                    s.PrepareRead(Heap + (ulong)random.Next(0, 256), random.Next(1, 64));
                                }
                                s.Execute();
                            }
                            break;
                        case 19: // Scatter pooled
                            using (var s = new VmmScatterManaged(_vmm, Vmm.PID_PHYSICALMEMORY))
                            {
                                s.PrepareReadArray<long>(Heap + randomOffset, 8);
                                s.Execute();
                                using var pooled = s.ReadPooled<long>(Heap + randomOffset, 8);
                                _ = pooled?.Memory.Length;
                            }
                            break;
                    }

                    Interlocked.Increment(ref _successfulOperations);
                }
                catch (Exception ex)
                {
                    _exceptions.Add(ex);
                    Interlocked.Increment(ref _failedOperations);
                }
            }
        });
    }

    #endregion

    #region Initialization

    private static void InitDMA()
    {
        const string dumpFile = "dump.raw";
        const ulong PTR_STR_UNICODE = 0x0;
        const ulong PTR_HEAP = 0x8;
        const ulong INT_HEAPLEN = 0x10;
        const string EXPECTED_STR = "Hello, World!";

        if (!File.Exists(dumpFile))
            throw new FileNotFoundException("The specified memory dump file was not found!", dumpFile);

        string[] args =
        [
            "-_internal_physical_memory_only",
            "-printf",
            "-v",
            "-f",
            $"file://file={dumpFile},write=1",
            "-waitinitialize",
            "-norefresh"
        ];

        var vmm = new Vmm(args);
        try
        {
            var ptrStrUnicode = vmm.MemReadValue<ulong>(Vmm.PID_PHYSICALMEMORY, PTR_STR_UNICODE);
            var unicodeStr = vmm.MemReadString(Vmm.PID_PHYSICALMEMORY, ptrStrUnicode, 64, Encoding.Unicode);
            ArgumentOutOfRangeException.ThrowIfNotEqual(unicodeStr, EXPECTED_STR, nameof(unicodeStr));

            Heap = vmm.MemReadValue<ulong>(Vmm.PID_PHYSICALMEMORY, PTR_HEAP);
            ArgumentOutOfRangeException.ThrowIfZero(Heap, nameof(Heap));
            HeapLen = vmm.MemReadValue<int>(Vmm.PID_PHYSICALMEMORY, INT_HEAPLEN);
            _vmm = vmm;

            Console.WriteLine($"Initialized with Heap @ 0x{Heap:X}, Length: {HeapLen}");
        }
        catch
        {
            vmm.Dispose();
            throw;
        }
    }

    #endregion

    #region Test Types

    [StructLayout(LayoutKind.Sequential)]
    private struct TestStruct
    {
        public ulong Field1;
        public uint Field2;
        public ushort Field3;
        public byte Field4;
        public byte Field5;
    }

    #endregion
}
