/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using System.Runtime.InteropServices;
using VmmSharpEx;
using VmmSharpEx_Tests.CI.Internal;

namespace VmmSharpEx_Tests.CI;

[Collection(nameof(CICollection))]
[Trait("RunScope", "CI")]
public unsafe class VmmSharpEx_LeechCoreTests : CITest
{
    private readonly Vmm _vmm;
    private readonly LeechCore _lc;
    private readonly ulong _heapBase;
    private readonly int _heapLen;

    public VmmSharpEx_LeechCoreTests(CIVmmFixture fixture)
    {
        _vmm = fixture.Vmm;
        _lc = _vmm.LeechCore;
        Assert.NotNull(_lc);
        _heapBase = fixture.Heap;
        _heapLen = fixture.HeapLen;
        Assert.True(_heapLen > 0x800000, "Heap length too small for tests.");
    }

    private ulong HeapAddr(int offset)
    {
        Assert.InRange(offset, 0, _heapLen - 1);
        return _heapBase + (ulong)offset;
    }

    [Fact]
    public void LeechCore_WriteReadValue_UInt64()
    {
        ulong pa = HeapAddr(0x100);
        const ulong expected = 0xCAFEBABE11223344UL;
        Assert.True(_lc.WriteValue(pa, expected));
        Assert.True(_lc.ReadValue(pa, out ulong actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void LeechCore_WriteReadArray_UInt32()
    {
        ulong pa = HeapAddr(0x180);
        var src = Enumerable.Range(0, 32).Select(i => (uint)(i * 0x11111111u)).ToArray();
        Assert.True(_lc.WriteArray(pa, src));
        using var pooled = _lc.ReadPooled<uint>(pa, src.Length);
        Assert.NotNull(pooled);
        Assert.Equal(src.Length, pooled.Memory.Span.Length);
        for (int i = 0; i < src.Length; i++) Assert.Equal(src[i], pooled.Memory.Span[i]);
    }

    [Fact]
    public void LeechCore_WriteReadSpan_Int16()
    {
        ulong pa = HeapAddr(0x220);
        short[] src = Enumerable.Range(1, 40).Select(i => (short)(i * 3)).ToArray();
        Assert.True(_lc.WriteSpan(pa, src));
        var dst = new short[src.Length];
        Assert.True(_lc.ReadSpan(pa, dst));
        Assert.Equal(src, dst);
    }

    [Fact]
    public void LeechCore_WriteRead_ByteArrayAndReadMethod()
    {
        ulong pa = HeapAddr(0x300);
        var bytes = new byte[128];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i ^ 0x5A);
        Assert.True(_lc.WriteArray(pa, bytes));
        var read = _lc.Read(pa, (uint)bytes.Length);
        Assert.NotNull(read);
        Assert.Equal(bytes, read);
    }

    [Fact]
    public void LeechCore_WriteRead_IntPtr()
    {
        ulong pa = HeapAddr(0x380);
        int len = 64;
        byte* src = (byte*)NativeMemory.Alloc((nuint)len);
        try
        {
            for (int i = 0; i < len; i++) src[i] = (byte)(0xA0 + i);
            Assert.True(_lc.Write(pa, (IntPtr)src, (uint)len));
            byte* dst = (byte*)NativeMemory.Alloc((nuint)len);
            try
            {
                Assert.True(_lc.Read(pa, (IntPtr)dst, (uint)len));
                for (int i = 0; i < len; i++) Assert.Equal(src[i], dst[i]);
            }
            finally { NativeMemory.Free(dst); }
        }
        finally { NativeMemory.Free(src); }
    }

    [Fact]
    public void LeechCore_WriteRead_VoidPointer()
    {
        ulong pa = HeapAddr(0x420);
        int len = 48;
        byte* src = (byte*)NativeMemory.Alloc((nuint)len);
        byte* dst = (byte*)NativeMemory.Alloc((nuint)len);
        try
        {
            for (int i = 0; i < len; i++) src[i] = (byte)(i * 7);
            Assert.True(_lc.Write(pa, src, (uint)len));
            Assert.True(_lc.Read(pa, dst, (uint)len));
            for (int i = 0; i < len; i++) Assert.Equal(src[i], dst[i]);
        }
        finally { NativeMemory.Free(src); NativeMemory.Free(dst); }
    }

    [Fact]
    public void LeechCore_ReadSpan_AfterWriteSpan_Byte()
    {
        ulong pa = HeapAddr(0x480);
        var src = Enumerable.Range(0, 96).Select(i => (byte)(i * 2 + 1)).ToArray();
        Assert.True(_lc.WriteSpan(pa, src));
        var dst = new byte[src.Length];
        Assert.True(_lc.ReadSpan(pa, dst));
        Assert.Equal(src, dst);
    }

    [Fact]
    public void LeechCore_ReadScatter_Pages()
    {
        // Ensure unique page-aligned addresses (different pages)
        ulong startPage = _heapBase & ~0xffful;
        var pages = new ulong[3];
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i] = startPage + (ulong)(i * 0x1000);
            var pattern = new byte[32];
            for (int j = 0; j < pattern.Length; j++) pattern[j] = (byte)(i * 0x10 + j);
            Assert.True(_lc.WriteSpan(pages[i], pattern));
        }
        using var scatter = _lc.ReadScatter(pages);
        Assert.NotNull(scatter);
        for (int idx = 0; idx < pages.Length; idx++)
        {
            ulong page = pages[idx];
            Assert.True(scatter.Results.ContainsKey(page));
            var data = scatter.Results[page];
            Assert.True(data.Data.Length >= 32);
            for (int k = 0; k < 8; k++) Assert.Equal((byte)(idx * 0x10 + k), data.Data[k]);
        }
    }
}