/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using VmmSharpEx;
using VmmSharpEx_Tests.Fixtures;
using Xunit.Abstractions;

namespace VmmSharpEx_Tests;

[Collection(nameof(VmmCollection))]
public class VmmSharpEx_MiscTests
{
    private readonly VmmFixture _fixture;
    private readonly Vmm _vmm;
    private readonly ITestOutputHelper _output;

    public VmmSharpEx_MiscTests(VmmFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _vmm = fixture.Vmm; // Shortcut
        _output = output;
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