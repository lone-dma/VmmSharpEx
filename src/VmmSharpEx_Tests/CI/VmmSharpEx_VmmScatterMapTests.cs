/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using VmmSharpEx;
using VmmSharpEx.Scatter;
using VmmSharpEx_Tests.CI.Internal;

namespace VmmSharpEx_Tests.CI;

[Collection(nameof(CICollection))]
public unsafe class VmmSharpEx_VmmScatterMapTests : CITest
{
    private readonly Vmm _vmm;
    private readonly ulong _heapBase;
    private readonly int _heapLen;

    public VmmSharpEx_VmmScatterMapTests(CIVmmFixture fixture)
    {
        _vmm = fixture.Vmm;
        _heapBase = fixture.Heap;
        _heapLen = fixture.HeapLen;
        Assert.True(_heapLen > 0x800000, "Heap length too small for tests.");
    }

    private ulong HeapAddr(int offset)
    {
        Assert.InRange(offset, 0, _heapLen - 1);
        return _heapBase + (ulong)offset;
    }

    private VmmScatterMap CreateMap() => new VmmScatterMap(_vmm, Vmm.PID_PHYSICALMEMORY);

    [Fact]
    public void ScatterMap_VmmScatter_AddRound_Execute_ReadBytes()
    {
        using var map = CreateMap();
        var round = map.AddRound();
        ulong addr = HeapAddr(0x1000);
        var pattern = Enumerable.Range(0, 32).Select(i => (byte)(i + 0x10)).ToArray();
        Assert.True(_vmm.MemWriteArray<byte>(Vmm.PID_PHYSICALMEMORY, addr, pattern));
        Assert.True(round.PrepareRead(addr, (uint)pattern.Length));
        map.Execute();
        var bytes = round.Read(addr, (uint)pattern.Length, out _);
        Assert.NotNull(bytes);
        Assert.Equal(pattern, bytes);
    }

    [Fact]
    public void ScatterMap_VmmScatter_MultipleRounds_AllExecute()
    {
        using var map = CreateMap();
        var round1 = map.AddRound();
        var round2 = map.AddRound();
        ulong addr1 = HeapAddr(0x2000);
        ulong addr2 = HeapAddr(0x3000);
        var data1 = Enumerable.Range(0, 16).Select(i => (ushort)(i * 2)).ToArray();
        var data2 = Enumerable.Range(0, 8).Select(i => (int)(i * 3)).ToArray();
        Assert.True(_vmm.MemWriteArray<ushort>(Vmm.PID_PHYSICALMEMORY, addr1, data1));
        Assert.True(_vmm.MemWriteArray<int>(Vmm.PID_PHYSICALMEMORY, addr2, data2));
        Assert.True(round1.PrepareReadArray<ushort>(addr1, data1.Length));
        Assert.True(round2.PrepareReadArray<int>(addr2, data2.Length));
        map.Execute();
        var result1 = round1.ReadArray<ushort>(addr1, data1.Length);
        var result2 = round2.ReadArray<int>(addr2, data2.Length);
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(data1, result1);
        Assert.Equal(data2, result2);
    }

    [Fact]
    public void ScatterMap_VmmScatterSlim_CompletedEvent_Fires()
    {
        using var map = CreateMap();
        var round = map.AddRound();
        int fired = 0;
        map.Completed += (_, __) => fired++;
        Assert.True(round.PrepareRead(HeapAddr(0x4000), 8));
        map.Execute();
        Assert.Equal(1, fired);
    }

    [Fact]
    public void ScatterMap_VmmScatterSlim_AddRound_Execute_ReadBytes()
    {
        using var map = CreateMap();
        var round = map.AddRound();
        ulong addr = HeapAddr(0x1000);
        var pattern = Enumerable.Range(0, 32).Select(i => (byte)(i + 0x10)).ToArray();
        Assert.True(_vmm.MemWriteArray<byte>(Vmm.PID_PHYSICALMEMORY, addr, pattern));
        Assert.True(round.PrepareRead(addr, (uint)pattern.Length));
        map.Execute();
        var bytes = round.Read(addr, (uint)pattern.Length);
        Assert.NotNull(bytes);
        Assert.Equal(pattern, bytes);
    }

    [Fact]
    public void ScatterMap_VmmScatterSlim_MultipleRounds_AllExecute()
    {
        using var map = CreateMap();
        var round1 = map.AddRound();
        var round2 = map.AddRound();
        ulong addr1 = HeapAddr(0x2000);
        ulong addr2 = HeapAddr(0x3000);
        var data1 = Enumerable.Range(0, 16).Select(i => (ushort)(i * 2)).ToArray();
        var data2 = Enumerable.Range(0, 8).Select(i => (int)(i * 3)).ToArray();
        Assert.True(_vmm.MemWriteArray<ushort>(Vmm.PID_PHYSICALMEMORY, addr1, data1));
        Assert.True(_vmm.MemWriteArray<int>(Vmm.PID_PHYSICALMEMORY, addr2, data2));
        Assert.True(round1.PrepareReadArray<ushort>(addr1, data1.Length));
        Assert.True(round2.PrepareReadArray<int>(addr2, data2.Length));
        map.Execute();
        var result1 = round1.ReadArray<ushort>(addr1, data1.Length);
        var result2 = round2.ReadArray<int>(addr2, data2.Length);
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(data1, result1);
        Assert.Equal(data2, result2);
    }

    [Fact]
    public void ScatterMap_VmmScatter_CompletedEvent_Fires()
    {
        using var map = CreateMap();
        var round = map.AddRound();
        int fired = 0;
        map.Completed += (_, __) => fired++;
        Assert.True(round.PrepareRead(HeapAddr(0x4000), 8));
        map.Execute();
        Assert.Equal(1, fired);
    }
}
