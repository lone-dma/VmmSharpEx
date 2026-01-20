/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using System.Security.Cryptography;
using VmmSharpEx;
using VmmSharpEx_Tests.CI.Internal;
using Xunit.Abstractions;

namespace VmmSharpEx_Tests.CI;

[Collection(nameof(CICollection))]
public class VmmSharpEx_VmmSearchTests : CITest
{
    private static readonly uint _pid = Vmm.PID_PHYSICALMEMORY;
    private readonly CIVmmFixture _fixture;
    private readonly Vmm _vmm;

    public VmmSharpEx_VmmSearchTests(CIVmmFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _vmm = fixture.Vmm; // Shortcut
    }

    [Fact]
    public void VmmSearch_Success()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(16);
        var addr = _fixture.Heap + (uint)_fixture.HeapLen / 2;
        _vmm.MemWriteArray(_pid, addr, randomBytes);
        var addrMin = addr >= 0x1000 ? addr - 0x1000UL : 0;
        var addrMax = addr + 0x1000UL;

        byte[] skipMask = [
            0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0xff, 0xff,
            0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0];

        var items = new[]
        {
            new VmmSearch.SearchItem(randomBytes, skipMask)
        };

        var result = _vmm.MemSearch(
            pid: _pid,
            searchItems: items,
            addr_min: addrMin,
            addr_max: addrMax);

        Assert.NotEmpty(result.Results);
    }

    [Fact]
    public async Task VmmSearch_Async()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(16);
        var addr = _fixture.Heap + (uint)_fixture.HeapLen / 2;
        _vmm.MemWriteArray(_pid, addr, randomBytes);
        var addrMin = addr >= 0x1000 ? addr - 0x1000UL : 0;
        var addrMax = addr + 0x1000UL;

        byte[] skipMask = [
            0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0xff, 0xff,
            0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0];

        var items = new[]
        {
            new VmmSearch.SearchItem(randomBytes, skipMask)
        };

        var result = await _vmm.MemSearchAsync(
            pid: _pid,
            searchItems: items,
            addr_min: addrMin,
            addr_max: addrMax);

        Assert.NotEmpty(result.Results);
    }

    [Fact]
    public void VmmSearch_Fail()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(16);
        var searchBytes = new byte[16];
        randomBytes.CopyTo(searchBytes);
        randomBytes[0] = 0xFF;
        searchBytes[0] = 0x00;
        var addr = _fixture.Heap + (uint)_fixture.HeapLen / 2;
        _vmm.MemWriteArray(_pid, addr, randomBytes);
        byte[] skipMask = [
            0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0xff, 0xff,
            0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0];

        var items = new[]
        {
            new VmmSearch.SearchItem(searchBytes, skipMask)
        };

        var result = _vmm.MemSearch(
            pid: _pid,
            searchItems: items,
            addr_min: addr,
            addr_max: addr + (ulong)randomBytes.Length);

        Assert.Empty(result.Results);
    }

    [Fact]
    public void VmmSearch_Disposed()
    {
        // Legacy CreateSearch-based API removed; verify argument validation still works.
        Assert.Throws<ArgumentNullException>(() => _vmm.MemSearch(_pid, null!));
    }

    [Fact]
    public void VmmSearch_EmptySearch()
    {
        var result = _vmm.MemSearch(_pid, Array.Empty<VmmSearch.SearchItem>());
        Assert.False(result.IsSuccess);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task VmmSearch_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            _vmm.MemSearch(_pid, Array.Empty<VmmSearch.SearchItem>(), ct: cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _vmm.MemSearchAsync(_pid, Array.Empty<VmmSearch.SearchItem>(), ct: cts.Token));
    }
}