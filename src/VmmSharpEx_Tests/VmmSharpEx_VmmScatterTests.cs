/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using System.Text;
using VmmSharpEx;
using VmmSharpEx.Options;
using VmmSharpEx.Scatter;
using VmmSharpEx_Tests.Fixtures;

namespace VmmSharpEx_Tests;

[Collection(nameof(VmmCollection))]
public unsafe class VmmSharpEx_VmmScatterTests
{
    private readonly Vmm _vmm;
    private readonly ulong _heapBase;
    private readonly int _heapLen;

    public VmmSharpEx_VmmScatterTests(VmmFixture fixture)
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

    private VmmScatter CreateScatter(VmmFlags flags = VmmFlags.NONE) => _vmm.CreateScatter(Vmm.PID_PHYSICALMEMORY, flags);

    [Fact]
    public void Scatter_PrepareRead_Execute_ReadBytes()
    {
        using var scatter = CreateScatter();
        ulong addr = HeapAddr(0x100);
        // Write known pattern first.
        var pattern = Enumerable.Range(0, 32).Select(i => (byte)(i + 0x20)).ToArray();
        Assert.True(_vmm.MemWriteArray<byte>(Vmm.PID_PHYSICALMEMORY, addr, pattern));
        Assert.True(scatter.PrepareRead(addr, (uint)pattern.Length));
        Assert.True(scatter.IsPrepared);
        scatter.Execute();
        var bytes = scatter.Read(addr, (uint)pattern.Length, out uint cbRead);
        Assert.NotNull(bytes);
        Assert.Equal((uint)pattern.Length, cbRead);
        Assert.Equal(pattern, bytes);
    }

    [Fact]
    public void Scatter_PrepareReadArray_ReadArray()
    {
        using var scatter = CreateScatter();
        ulong addr = HeapAddr(0x200);
        var source = Enumerable.Range(1, 64).Select(i => (ushort)(i * 3)).ToArray();
        Assert.True(_vmm.MemWriteArray<ushort>(Vmm.PID_PHYSICALMEMORY, addr, source));
        Assert.True(scatter.PrepareReadArray<ushort>(addr, source.Length));
        scatter.Execute();
        using var result = scatter.ReadPooled<ushort>(addr, source.Length);
        Assert.NotNull(result);
        Assert.Equal(source.Length, result.Memory.Span.Length);
        for (int i = 0; i < source.Length; i++) Assert.Equal(source[i], result.Memory.Span[i]);
    }

    [Fact]
    public void Scatter_PrepareReadSpan_ReadSpan()
    {
        using var scatter = CreateScatter();
        ulong addr = HeapAddr(0x300);
        var source = Enumerable.Range(0, 128).Select(i => (int)(i * 7)).ToArray();
        Assert.True(_vmm.MemWriteArray<int>(Vmm.PID_PHYSICALMEMORY, addr, source));
        Assert.True(scatter.PrepareReadArray<int>(addr, source.Length));
        scatter.Execute();
        var dst = new int[source.Length];
        Assert.True(scatter.ReadSpan<int>(addr, dst));
        Assert.Equal(source, dst);
    }

    [Fact]
    public void Scatter_PrepareReadValue_ReadValue()
    {
        using var scatter = CreateScatter();
        ulong addr = HeapAddr(0x400);
        const long expected = -0x123456789ABCDEFL;
        Assert.True(_vmm.MemWriteValue<long>(Vmm.PID_PHYSICALMEMORY, addr, expected));
        Assert.True(scatter.PrepareReadValue<long>(addr));
        scatter.Execute();
        Assert.True(scatter.ReadValue<long>(addr, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Scatter_PrepareWriteValue_ThenReadBack()
    {
        using var scatter = CreateScatter();
        ulong addr = HeapAddr(0x500);
        const ulong value = 0xDEADBEEFCAFEBABEUL;
        Assert.True(scatter.PrepareWriteValue(addr, value));
        scatter.Execute();
        Assert.True(_vmm.MemReadValue<ulong>(Vmm.PID_PHYSICALMEMORY, addr, out var actual));
        Assert.Equal(value, actual);
    }

    [Fact]
    public void Scatter_PrepareWriteSpan_ThenReadBack()
    {
        using var scatter = CreateScatter();
        ulong addr = HeapAddr(0x600);
        var source = Enumerable.Range(0, 40).Select(i => (byte)(i ^ 0xAA)).ToArray();
        Assert.True(scatter.PrepareWriteSpan<byte>(addr, source));
        scatter.Execute();
        var read = _vmm.MemRead(Vmm.PID_PHYSICALMEMORY, addr, (uint)source.Length, out uint cbRead);
        Assert.NotNull(read);
        Assert.Equal((uint)source.Length, cbRead);
        Assert.Equal(source, read);
    }

    [Fact]
    public void Scatter_PrepareReadPtr_ReadPtr()
    {
        using var scatter = CreateScatter();
        // Write a known valid-looking user-mode virtual address into heap then read it as a pointer.
        // Typical image base for 64-bit processes often around 0x0000000140000000; use that.
        ulong storedPtrAddr = HeapAddr(0x650);
        const ulong fakeValidVa = 0x0000000140000000UL; // High enough and page-aligned.
        Assert.True(_vmm.MemWriteValue<ulong>(Vmm.PID_PHYSICALMEMORY, storedPtrAddr, fakeValidVa));
        Assert.True(scatter.PrepareReadPtr(storedPtrAddr));
        scatter.Execute();
        Assert.True(scatter.ReadPtr(storedPtrAddr, out var ptr));
        ptr.ThrowIfInvalidVA();
        Assert.Equal(fakeValidVa, (ulong)ptr);
    }

    [Fact]
    public void Scatter_ReadString()
    {
        using var scatter = CreateScatter();
        // Write a unicode string then read back using scatter.
        ulong addr = HeapAddr(0x700);
        const string testStr = "ScatterStringTest";
        var bytes = Encoding.Unicode.GetBytes(testStr + '\0');
        Assert.True(_vmm.MemWriteArray<byte>(Vmm.PID_PHYSICALMEMORY, addr, bytes));
        Assert.True(scatter.PrepareRead(addr, (uint)bytes.Length));
        scatter.Execute();
        var read = scatter.ReadString(addr, bytes.Length, Encoding.Unicode);
        Assert.Equal(testStr, read);
    }

    [Fact]
    public void Scatter_MultipleMixedOperations_SingleExecute()
    {
        using var scatter = CreateScatter();
        ulong addrValue = HeapAddr(0x800);
        ulong addrArray = HeapAddr(0x900);
        ulong addrBytes = HeapAddr(0xA00);
        const int value = 0x55667788;
        var arraySrc = Enumerable.Range(1, 16).Select(i => (uint)(i * 5)).ToArray();
        var bytesSrc = Enumerable.Range(0, 24).Select(i => (byte)(0xF0 + i)).ToArray();
        Assert.True(scatter.PrepareWriteValue(addrValue, value));
        Assert.True(scatter.PrepareWriteSpan<uint>(addrArray, arraySrc));
        Assert.True(scatter.PrepareWriteSpan<byte>(addrBytes, bytesSrc));
        scatter.Execute();
        // Prepare reads for same locations.
        Assert.True(scatter.PrepareReadValue<int>(addrValue));
        Assert.True(scatter.PrepareReadArray<uint>(addrArray, arraySrc.Length));
        Assert.True(scatter.PrepareRead(addrBytes, (uint)bytesSrc.Length));
        scatter.Execute();
        Assert.True(scatter.ReadValue<int>(addrValue, out var valueRead));
        Assert.Equal(value, valueRead);
        using var arrRead = scatter.ReadPooled<uint>(addrArray, arraySrc.Length);
        Assert.NotNull(arrRead);
        Assert.Equal(arraySrc.Length, arrRead.Memory.Span.Length);
        for (int i = 0; i < arraySrc.Length; i++) Assert.Equal(arraySrc[i], arrRead.Memory.Span[i]);
        var bytesRead = scatter.Read(addrBytes, (uint)bytesSrc.Length, out var cbRead);
        Assert.NotNull(bytesRead);
        Assert.Equal((uint)bytesSrc.Length, cbRead);
        Assert.Equal(bytesSrc, bytesRead);
    }

    [Fact]
    public void Scatter_CompletedEvent_Fires()
    {
        using var scatter = CreateScatter();
        int fired = 0;
        scatter.Completed += (_, __) => fired++;
        Assert.True(scatter.PrepareRead(HeapAddr(0xB00), 16));
        scatter.Execute();
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Scatter_Clear_ResetsState()
    {
        using var scatter = CreateScatter();
        int fired = 0;
        scatter.Completed += (_, __) => fired++;
        Assert.True(scatter.PrepareRead(HeapAddr(0xC00), 8));
        scatter.Clear();
        Assert.False(scatter.IsPrepared);
        // Execute should not fire event since nothing prepared.
        scatter.Execute();
        Assert.Equal(0, fired);
    }

    [Fact]
    public void Scatter_ToString_ReturnsState()
    {
        using var scatter = CreateScatter();
        string s = scatter.ToString();
        Assert.Contains("VmmScatter", s);
    }
}
