/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using VmmSharpEx;
using VmmSharpEx.Options;
using VmmSharpEx_Tests.CI.Internal;

namespace VmmSharpEx_Tests.CI;

[Collection(nameof(CICollection))]
public unsafe class VmmSharpEx_VmmTests : CITest
{
    private readonly Vmm _vmm;
    private readonly ulong _heapBase;
    private readonly int _heapLen;

    public VmmSharpEx_VmmTests(CIVmmFixture fixture)
    {
        _vmm = fixture.Vmm;
        Assert.NotNull(_vmm);
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
    public void ConfigGet_CoreLeechcoreHandle_IsNonZero()
    {
        var h = _vmm.ConfigGet(VmmOption.CORE_LEECHCORE_HANDLE);
        Assert.True(h.HasValue);
        Assert.NotEqual(0ul, h.Value);
    }

    [Fact]
    public void ForceFullRefresh_DoesNotThrow()
    {
        _vmm.ForceFullRefresh();
    }

    [Fact]
    public void MemWriteRead_Value_UInt64()
    {
        ulong addr = HeapAddr(0x00);
        const ulong expected = 0x1122334455667788UL;
        Assert.True(_vmm.MemWriteValue(Vmm.PID_PHYSICALMEMORY, addr, expected));
        Assert.True(_vmm.MemReadValue(Vmm.PID_PHYSICALMEMORY, addr, out ulong actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void MemWriteRead_Span_Int32()
    {
        ulong addr = HeapAddr(0x80);
        var data = Enumerable.Range(0, 16).Select(i => i * 7).ToArray();
        Assert.True(_vmm.MemWriteSpan<int>(Vmm.PID_PHYSICALMEMORY, addr, data));
        var readBack = new int[data.Length];
        Assert.True(_vmm.MemReadSpan<int>(Vmm.PID_PHYSICALMEMORY, addr, readBack));
        Assert.Equal(data, readBack);
    }

    [Fact]
    public void MemWriteRead_Array_Byte()
    {
        ulong addr = HeapAddr(0x140);
        var bytes = new byte[64];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i ^ 0xA5);
        Assert.True(_vmm.MemWriteArray<byte>(Vmm.PID_PHYSICALMEMORY, addr, bytes));
        var arr = _vmm.MemRead(Vmm.PID_PHYSICALMEMORY, addr, (uint)bytes.Length, out uint cbRead);
        Assert.NotNull(arr);
        Assert.Equal((uint)bytes.Length, cbRead);
        Assert.Equal(bytes, arr);
    }

    [Fact]
    public void MemWriteRead_IntPtr_Buffer()
    {
        ulong addr = HeapAddr(0x1C0);
        int len = 32;
        byte* unmanaged = (byte*)NativeMemory.Alloc((nuint)len);
        try
        {
            for (int i = 0; i < len; i++) unmanaged[i] = (byte)(0xF0 + i);
            Assert.True(_vmm.MemWrite(Vmm.PID_PHYSICALMEMORY, addr, (IntPtr)unmanaged, (uint)len));
            byte* readBuf = (byte*)NativeMemory.Alloc((nuint)len);
            try
            {
                Assert.True(_vmm.MemRead(Vmm.PID_PHYSICALMEMORY, addr, (IntPtr)readBuf, (uint)len, out uint cbRead));
                Assert.Equal((uint)len, cbRead);
                for (int i = 0; i < len; i++) Assert.Equal(unmanaged[i], readBuf[i]);
            }
            finally { NativeMemory.Free(readBuf); }
        }
        finally { NativeMemory.Free(unmanaged); }
    }

    [Fact]
    public void MemWriteRead_VoidPointer_Buffer()
    {
        ulong addr = HeapAddr(0x220);
        int len = 48;
        byte* src = (byte*)NativeMemory.Alloc((nuint)len);
        byte* dst = (byte*)NativeMemory.Alloc((nuint)len);
        try
        {
            for (int i = 0; i < len; i++) src[i] = (byte)(i * 3);
            Assert.True(_vmm.MemWrite(Vmm.PID_PHYSICALMEMORY, addr, src, (uint)len));
            Assert.True(_vmm.MemRead(Vmm.PID_PHYSICALMEMORY, addr, dst, (uint)len, out uint cbRead));
            Assert.Equal((uint)len, cbRead);
            for (int i = 0; i < len; i++) Assert.Equal(src[i], dst[i]);
        }
        finally { NativeMemory.Free(src); NativeMemory.Free(dst); }
    }

    [Fact]
    public void MemWriteRead_String_Unicode()
    {
        ulong addr = HeapAddr(0x300);
        const string testStr = "Test_Unicode_String";
        var bytes = Encoding.Unicode.GetBytes(testStr + '\0');
        Assert.True(_vmm.MemWriteArray<byte>(Vmm.PID_PHYSICALMEMORY, addr, bytes));
        var read = _vmm.MemReadString(Vmm.PID_PHYSICALMEMORY, addr, bytes.Length, Encoding.Unicode);
        Assert.Equal(testStr, read);
    }

    [Fact]
    public void MemReadArray_PooledMemory_UInt32()
    {
        ulong addr = HeapAddr(0x400);
        var src = Enumerable.Range(1, 32).Select(i => (uint)(i * 0x10u)).ToArray();
        Assert.True(_vmm.MemWriteArray<uint>(Vmm.PID_PHYSICALMEMORY, addr, src));
        using var pooled = _vmm.MemReadPooled<uint>(Vmm.PID_PHYSICALMEMORY, addr, src.Length);
        Assert.NotNull(pooled);
        Assert.Equal(src.Length, pooled.Memory.Span.Length);
        for (int i = 0; i < src.Length; i++) Assert.Equal(src[i], pooled.Memory.Span[i]);
    }

    [Fact]
    public void MemPrefetchPages_Succeeds()
    {
        // Use first 3 page-aligned addresses in heap range.
        var startPage = _heapBase & ~0xffful;
        var pages = new ulong[3];
        for (int i = 0; i < pages.Length; i++) pages[i] = startPage + (ulong)(0x1000 * i);
        Assert.True(_vmm.MemPrefetchPages(Vmm.PID_PHYSICALMEMORY, pages));
    }

    [Fact]
    public void MemReadScatter_ReadsPages()
    {
        // Prepare patterns at page starts then scatter read.
        var startPage = _heapBase & ~0xffful;
        var mems = new LeechCore.MEM_SCATTER[3];
        for (int i = 0; i < mems.Length; i++)
        {
            ulong pageAddr = startPage + (ulong)(i * 0x1000);
            mems[i] = new LeechCore.MEM_SCATTER { qwA = pageAddr, cb = 0x1000 };
            var pattern = new byte[16];
            for (int j = 0; j < pattern.Length; j++) pattern[j] = (byte)(i * 0x10 + j);
            Assert.True(_vmm.MemWriteArray<byte>(Vmm.PID_PHYSICALMEMORY, pageAddr, pattern));
        }
        using var scatter = _vmm.MemReadScatter(Vmm.PID_PHYSICALMEMORY, VmmFlags.NONE, mems);
        Assert.NotNull(scatter);
        foreach (var mem in mems)
        {
            Assert.True(scatter.Results.ContainsKey(mem.qwA));
            var data = scatter.Results[mem.qwA];
            Assert.True(data.Data.Length >= 16);
        }
    }
}