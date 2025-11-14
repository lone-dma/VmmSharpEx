/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using VmmSharpEx;
using VmmSharpEx.Options;
using VmmSharpEx_Tests.Fixtures;
using Xunit.Abstractions;

namespace VmmSharpEx_Tests;

[Collection(nameof(VmmCollection))]
public class VmmSharpEx_BasicTests
{
    private readonly VmmFixture _fixture;
    private readonly Vmm _vmm;
    private readonly ITestOutputHelper _output;

    public VmmSharpEx_BasicTests(VmmFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _vmm = fixture.Vmm; // Shortcut
        _output = output;
    }

    [Fact]
    public void PidGetAllFromName_Contains_TargetProcess()
    {
        var pids = _vmm.PidGetAllFromName(VmmFixture.TargetProcess);
        Assert.NotNull(pids);
        Assert.Contains(_fixture.PID, pids);
    }

    [Fact]
    public void GetMemoryMap_ToString_And_File()
    {
        // Plain retrieval
        var map = _vmm.GetMemoryMap(applyMap: false);
        Assert.False(string.IsNullOrWhiteSpace(map));

        // Write to file
        var path = Path.Combine(Path.GetTempPath(), $"memmap_{Guid.NewGuid():N}.txt");
        try
        {
            var map2 = _vmm.GetMemoryMap(applyMap: false, outputFile: path);
            Assert.False(string.IsNullOrWhiteSpace(map2));
            Assert.True(File.Exists(path));
            var contents = File.ReadAllText(path);
            Assert.False(string.IsNullOrWhiteSpace(contents));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Config_GetSet_TickPeriod_RoundTrip()
    {
        var original = _vmm.ConfigGet(VmmOption.CONFIG_TICK_PERIOD);
        Assert.True(original.HasValue);

        var newVal = original.Value == 0 ? 1UL : original.Value + 1;
        try
        {
            Assert.True(_vmm.ConfigSet(VmmOption.CONFIG_TICK_PERIOD, newVal));
            var after = _vmm.ConfigGet(VmmOption.CONFIG_TICK_PERIOD);
            Assert.True(after.HasValue);
            Assert.Equal(newVal, after.Value);
        }
        finally
        {
            if (original.HasValue)
            {
                _vmm.ConfigSet(VmmOption.CONFIG_TICK_PERIOD, original.Value);
            }
        }
    }

    [Fact]
    public void Map_GetEAT_Kernel32_Smoke()
    {
        // Kernel32 is always present on Windows user-mode processes
        var eat = _vmm.Map_GetEAT(_fixture.PID, "kernel32.dll", out var info);
        Assert.NotNull(eat);
        // Expect at least some exports
        Assert.True(eat.Length > 0);
        Assert.True(info.cNumberOfFunctions >= 0u);
    }

    [Fact]
    public void Map_GetPool_BigOnly_Smoke()
    {
        var pool = _vmm.Map_GetPool(isBigPoolOnly: true);
        Assert.NotNull(pool);
        // May be empty on some systems, but should not throw
        Assert.True(pool.Length >= 0);
    }

    [Fact]
    public void Map_GetVM_Smoke()
    {
        var vms = _vmm.Map_GetVM();
        Assert.NotNull(vms);
        // May be empty if no Hyper-V/WSL/etc., but should not throw
        Assert.True(vms.Length >= 0);
    }

    [Fact]
    public void MemVirt2Phys_And_PrefetchPages_Smoke()
    {
        // Prefetch module base page
        var page = _fixture.ModuleBase & ~0xfffUL;
        Span<ulong> vas = stackalloc ulong[1] { page };
        Assert.True(_vmm.MemPrefetchPages(_fixture.PID, vas));

        // Try VA->PA translation (may return 0 if translation unavailable)
        var pa = _vmm.MemVirt2Phys(_fixture.PID, _fixture.ModuleBase);
        Assert.True(pa >= 0);
    }

    [Fact]
    public void CustomLogging()
    {
        Assert.True(_vmm.LogCallback(LoggingFn));
        _vmm.Log("Test log message from unit test", Vmm.LogLevel.Info);
        Assert.True(_vmm.LogCallback(null));
    }

    private void LoggingFn(IntPtr hVMM, uint MID, string uszModule, Vmm.LogLevel dwLogLevel, string uszLogMessage)
    {
        Assert.Equal(_vmm, hVMM);
        ArgumentException.ThrowIfNullOrWhiteSpace(uszModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(uszLogMessage);
        _output.WriteLine(uszLogMessage);
    }
}