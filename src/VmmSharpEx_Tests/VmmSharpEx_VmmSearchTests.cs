/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using System.Security.Cryptography;
using VmmSharpEx;
using VmmSharpEx_Tests.Fixtures;
using Xunit.Abstractions;

namespace VmmSharpEx_Tests;

[Collection(nameof(VmmCollection))]
public class VmmSharpEx_VmmSearchTests
{
    private static readonly uint _pid = Vmm.PID_PHYSICALMEMORY;
    private readonly VmmFixture _fixture;
    private readonly Vmm _vmm;

    public VmmSharpEx_VmmSearchTests(VmmFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _vmm = fixture.Vmm; // Shortcut
    }

    [Fact]
    public void VmmSearch_Success()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(16);
        _vmm.MemWriteArray(_pid, _fixture.Heap + (uint)_fixture.HeapLen / 2, randomBytes);
        var search = _vmm.CreateSearch(_pid);
        Assert.NotNull(search);
        byte[] skipMask = [
            0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0xff, 0xff,
            0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0];
        search.AddEntry(randomBytes, skipMask);
        var result = search.GetResult();
        Assert.NotNull(result);
        Assert.True(result.IsCompleted);
        Assert.True(result.IsCompletedSuccess);
        Assert.True(result.Results.Count > 0);
        search.Dispose();
    }

    [Fact]
    public async Task VmmSearch_Async()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(16);
        _vmm.MemWriteArray(_pid, _fixture.Heap + (uint)_fixture.HeapLen / 2, randomBytes);
        var search = _vmm.CreateSearch(_pid);
        Assert.NotNull(search);
        byte[] skipMask = [
            0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0xff, 0xff,
            0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0];
        search.AddEntry(randomBytes, skipMask);
        var result = await search.GetResultAsync();
        Assert.NotNull(result);
        Assert.True(result.IsCompleted);
        Assert.True(result.IsCompletedSuccess);
        Assert.True(result.Results.Count > 0);
        search.Dispose();
    }

    [Fact]
    public void VmmSearch_Fail()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(16);
        var searchBytes = new byte[16];
        randomBytes.CopyTo(searchBytes);
        randomBytes[0] = 0xFF;
        searchBytes[0] = 0x00;
        _vmm.MemWriteArray(_pid, _fixture.Heap + (uint)_fixture.HeapLen / 2, randomBytes);
        var search = _vmm.CreateSearch(_pid);
        Assert.NotNull(search);
        byte[] skipMask = [
            0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0xff, 0xff,
            0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0];
        search.AddEntry(searchBytes, skipMask);
        var result = search.GetResult();
        Assert.NotNull(result);
        Assert.True(result.IsCompleted);
        Assert.True(result.IsCompletedSuccess);
        Assert.Empty(result.Results);
        search.Dispose();
    }

    [Fact]
    public void VmmSearch_Disposed()
    {
        var search = _vmm.CreateSearch(_pid);
        search.Dispose();
        Assert.Throws<ObjectDisposedException>(() => search.Poll());
    }

    [Fact]
    public void VmmSearch_DoubleStart()
    {
        using var search = _vmm.CreateSearch(_pid);
        search.Start();
        Assert.Throws<InvalidOperationException>(() => search.Start());
    }
}