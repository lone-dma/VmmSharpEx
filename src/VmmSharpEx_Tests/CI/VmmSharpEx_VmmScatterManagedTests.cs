/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using System.Text;
using VmmSharpEx;
using VmmSharpEx.Options;
using VmmSharpEx.Scatter;
using VmmSharpEx_Tests.CI.Internal;

namespace VmmSharpEx_Tests.CI;

[Collection(nameof(CICollection))]
public unsafe class VmmSharpEx_VmmScatterTests : CITest
{
    private readonly Vmm _vmm;
    private readonly ulong _heapBase;
    private readonly int _heapLen;

    public VmmSharpEx_VmmScatterTests(CIVmmFixture fixture)
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

    /// <summary>
    /// Get a page-aligned heap address with specified page offset.
    /// Page index 0 is the first full page that fits within the heap.
    /// </summary>
    private ulong PageAlignedHeapAddr(int pageIndex, int offsetInPage = 0)
    {
        // Align heap base UP to next page boundary to ensure we're within the heap
        ulong firstPageBase = (_heapBase + 0xFFFUL) & ~0xFFFUL;
        ulong addr = firstPageBase + (ulong)(pageIndex * 0x1000) + (ulong)offsetInPage;

        // Verify the address and entire read range is within heap bounds
        long heapOffset = (long)(addr - _heapBase);
        Assert.InRange(heapOffset, 0, _heapLen - 1);

        return addr;
    }

    private VmmScatter CreateScatter(VmmFlags flags = VmmFlags.NONE) => new VmmScatter(_vmm, Vmm.PID_PHYSICALMEMORY, flags);

    private byte[] CreatePattern(int length, byte seed = 0)
    {
        return Enumerable.Range(0, length).Select(i => (byte)((i + seed) & 0xFF)).ToArray();
    }

    private void WriteAndVerifyPattern(ulong addr, byte[] pattern)
    {
        Assert.True(_vmm.MemWriteArray<byte>(Vmm.PID_PHYSICALMEMORY, addr, pattern));
    }

    #region Basic Tests

    [Fact]
    public void Scatter_PrepareRead_Execute_ReadBytes()
    {
        using var scatter = CreateScatter();
        ulong addr = HeapAddr(0x100);
        var pattern = CreatePattern(32, 0x20);
        WriteAndVerifyPattern(addr, pattern);
        Assert.True(scatter.PrepareRead(addr, (uint)pattern.Length));
        scatter.Execute();
        var bytes = scatter.Read(addr, (uint)pattern.Length);
        Assert.NotNull(bytes);
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
    public void Scatter_PrepareReadPtr_ReadPtr()
    {
        using var scatter = CreateScatter();
        ulong storedPtrAddr = HeapAddr(0x650);
        const ulong fakeValidVa = 0x0000000140000000UL;
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
    public void Scatter_ToString_ReturnsState()
    {
        using var scatter = CreateScatter();
        string s = scatter.ToString();
        Assert.Contains("VmmScatter", s);
    }

    #endregion

    #region Single Page Full Read Tests (cb > 0x400, single page)

    [Theory]
    [InlineData(0x401)]  // Just above tiny threshold
    [InlineData(0x500)]  // Mid-range
    [InlineData(0x800)]  // Half page
    [InlineData(0xC00)]  // 3/4 page
    [InlineData(0xFFF)]  // Almost full page
    [InlineData(0x1000)] // Full page
    public void Scatter_SinglePage_AboveTinyThreshold(int cb)
    {
        using var scatter = CreateScatter();
        ulong addr = PageAlignedHeapAddr(5, 0); // Must be page-aligned for full page
        var pattern = CreatePattern(cb);
        WriteAndVerifyPattern(addr, pattern);

        Assert.True(scatter.PrepareRead(addr, (uint)cb));
        scatter.Execute();
        var result = scatter.Read(addr, (uint)cb);

        Assert.NotNull(result);
        Assert.Equal(pattern, result);
    }

    [Theory]
    [InlineData(0x500, 0x100)] // 0x500 bytes at offset 0x100
    [InlineData(0x800, 0x800)] // Half page at mid-page (ends at page boundary)
    public void Scatter_SinglePage_LargeWithOffset(int cb, int offsetInPage)
    {
        if (offsetInPage + cb > 0x1000)
            return;

        using var scatter = CreateScatter();
        ulong addr = PageAlignedHeapAddr(6, offsetInPage);
        var pattern = CreatePattern(cb);
        WriteAndVerifyPattern(addr, pattern);

        Assert.True(scatter.PrepareRead(addr, (uint)cb));
        scatter.Execute();
        var result = scatter.Read(addr, (uint)cb);

        Assert.NotNull(result);
        Assert.Equal(pattern, result);
    }

    #endregion

    #region Multi-Page Tests (spans 2+ pages)

    [Theory]
    [InlineData(0x1001)] // Just over 1 page
    [InlineData(0x1800)] // 1.5 pages
    [InlineData(0x2000)] // Exactly 2 pages
    [InlineData(0x2001)] // Just over 2 pages
    [InlineData(0x2800)] // 2.5 pages
    [InlineData(0x3000)] // Exactly 3 pages
    public void Scatter_MultiPage_PageAligned(int cb)
    {
        using var scatter = CreateScatter();
        ulong addr = PageAlignedHeapAddr(10, 0);
        var pattern = CreatePattern(cb);
        WriteAndVerifyPattern(addr, pattern);

        Assert.True(scatter.PrepareRead(addr, (uint)cb));
        scatter.Execute();
        var result = scatter.Read(addr, (uint)cb);

        Assert.NotNull(result);
        Assert.Equal(pattern, result);
    }

    [Theory]
    [InlineData(0x1000, 0x800)] // 1 page starting mid-page = spans 2 pages
    [InlineData(0x1000, 0x001)] // 1 page starting at offset 1 = spans 2 pages
    [InlineData(0x1000, 0xFFF)] // 1 page starting at end = spans 2 pages
    [InlineData(0x2000, 0x100)] // 2 pages with offset = spans 3 pages
    [InlineData(0x2000, 0x800)] // 2 pages mid-page = spans 3 pages
    public void Scatter_MultiPage_WithOffset_SpansBoundary(int cb, int offsetInPage)
    {
        using var scatter = CreateScatter();
        ulong addr = PageAlignedHeapAddr(15, offsetInPage);
        var pattern = CreatePattern(cb, (byte)offsetInPage);
        WriteAndVerifyPattern(addr, pattern);

        Assert.True(scatter.PrepareRead(addr, (uint)cb));
        scatter.Execute();
        var result = scatter.Read(addr, (uint)cb);

        Assert.NotNull(result);
        Assert.Equal(pattern, result);
    }

    [Fact]
    public void Scatter_MultiPage_CrossesThreePages()
    {
        using var scatter = CreateScatter();
        // Start near end of page, read enough to span 3 pages
        int offsetInPage = 0xF00; // 256 bytes from page end
        int cb = 0x1200; // Needs: 256 + 4096 + 256 = spans 3 pages
        ulong addr = PageAlignedHeapAddr(20, offsetInPage);
        var pattern = CreatePattern(cb);
        WriteAndVerifyPattern(addr, pattern);

        Assert.True(scatter.PrepareRead(addr, (uint)cb));
        scatter.Execute();
        var result = scatter.Read(addr, (uint)cb);

        Assert.NotNull(result);
        Assert.Equal(pattern, result);
    }

    #endregion

    #region Page Boundary Edge Cases

    [Theory]
    [InlineData(0xFFF, 2)]   // 2 bytes crossing boundary (1 byte on each page)
    [InlineData(0xFFC, 8)]   // 8 bytes crossing boundary
    [InlineData(0xFF0, 32)]  // 32 bytes crossing boundary
    [InlineData(0xF00, 512)] // 512 bytes crossing boundary
    public void Scatter_CrossesPageBoundary_SmallRead(int offsetInPage, int cb)
    {
        // This should span 2 pages (offset + size must exceed page boundary)
        Assert.True(offsetInPage + cb > 0x1000, $"Test data error: {offsetInPage} + {cb} should exceed 0x1000");

        using var scatter = CreateScatter();
        ulong addr = PageAlignedHeapAddr(25, offsetInPage);
        var pattern = CreatePattern(cb, 0xBB);
        WriteAndVerifyPattern(addr, pattern);

        Assert.True(scatter.PrepareRead(addr, (uint)cb));
        scatter.Execute();
        var result = scatter.Read(addr, (uint)cb);

        Assert.NotNull(result);
        Assert.Equal(pattern, result);
    }

    [Fact]
    public void Scatter_ExactlyAtPageBoundary_SingleByte()
    {
        using var scatter = CreateScatter();
        // Read single byte at very end of page
        ulong addr = PageAlignedHeapAddr(26, 0xFFF);
        byte expected = 0x42;
        Assert.True(_vmm.MemWriteValue<byte>(Vmm.PID_PHYSICALMEMORY, addr, expected));

        Assert.True(scatter.PrepareRead(addr, 1u));
        scatter.Execute();
        Assert.True(scatter.ReadValue<byte>(addr, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Scatter_ExactlyAtPageBoundary_CrossingSingleByte()
    {
        using var scatter = CreateScatter();
        // Read 2 bytes: one at end of page, one at start of next
        ulong addr = PageAlignedHeapAddr(27, 0xFFF);
        var pattern = new byte[] { 0xAB, 0xCD };
        WriteAndVerifyPattern(addr, pattern);

        Assert.True(scatter.PrepareRead(addr, 2));
        scatter.Execute();
        var result = scatter.Read(addr, 2);

        Assert.NotNull(result);
        Assert.Equal(pattern, result);
    }

    #endregion

    #region Multiple Prepare Calls (Overlapping/Same Page)

    [Fact]
    public void Scatter_MultiplePrepares_SamePage_UpgradesToFullPage()
    {
        using var scatter = CreateScatter();
        ulong pageBase = PageAlignedHeapAddr(30, 0);

        // Write patterns at different offsets
        var pattern1 = CreatePattern(32, 0x11);
        var pattern2 = CreatePattern(64, 0x22);
        WriteAndVerifyPattern(pageBase + 0x100, pattern1);
        WriteAndVerifyPattern(pageBase + 0x200, pattern2);

        // Prepare two reads on same page (both would be tiny pMEM individually)
        Assert.True(scatter.PrepareRead(pageBase + 0x100, 32));
        Assert.True(scatter.PrepareRead(pageBase + 0x200, 64)); // Should upgrade to full page

        scatter.Execute();

        var result1 = scatter.Read(pageBase + 0x100, 32);
        var result2 = scatter.Read(pageBase + 0x200, 64);

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(pattern1, result1);
        Assert.Equal(pattern2, result2);
    }

    [Fact]
    public void Scatter_MultiplePrepares_DifferentPages()
    {
        using var scatter = CreateScatter();

        var addrsAndPatterns = new List<(ulong addr, byte[] pattern)>();
        for (int i = 0; i < 5; i++)
        {
            ulong addr = PageAlignedHeapAddr(35 + i, 0x100 + i * 0x10);
            var pattern = CreatePattern(64, (byte)(i * 0x10));
            WriteAndVerifyPattern(addr, pattern);
            addrsAndPatterns.Add((addr, pattern));
            Assert.True(scatter.PrepareRead(addr, 64));
        }

        scatter.Execute();

        foreach (var (addr, pattern) in addrsAndPatterns)
        {
            var result = scatter.Read(addr, 64);
            Assert.NotNull(result);
            Assert.Equal(pattern, result);
        }
    }

    [Fact]
    public void Scatter_MultiplePrepares_MixedSizes()
    {
        using var scatter = CreateScatter();
        ulong baseAddr = PageAlignedHeapAddr(45, 0);

        // Mix of tiny, medium, and large reads across multiple pages
        var reads = new List<(ulong addr, int cb, byte[] pattern)>
        {
            (baseAddr + 0x0000, 8, CreatePattern(8, 0x01)),         // Tiny
            (baseAddr + 0x1000, 0x400, CreatePattern(0x400, 0x02)), // Max tiny
            (baseAddr + 0x2000, 0x800, CreatePattern(0x800, 0x03)), // Large single page
            (baseAddr + 0x3100, 0x1000, CreatePattern(0x1000, 0x04)), // Spans 2 pages
        };

        foreach (var (addr, cb, pattern) in reads)
        {
            WriteAndVerifyPattern(addr, pattern);
            Assert.True(scatter.PrepareRead(addr, (uint)cb));
        }

        scatter.Execute();

        foreach (var (addr, cb, pattern) in reads)
        {
            var result = scatter.Read(addr, (uint)cb);
            Assert.NotNull(result);
            Assert.Equal(pattern, result);
        }
    }

    #endregion

    #region Reset and Re-execute Tests

    [Fact]
    public void Scatter_ReExecute_SameEntries()
    {
        using var scatter = CreateScatter();
        ulong addr = HeapAddr(0x5100);

        // First execution
        var pattern1 = CreatePattern(32, 0xAA);
        WriteAndVerifyPattern(addr, pattern1);
        Assert.True(scatter.PrepareRead(addr, 32u));
        scatter.Execute();
        Assert.Equal(pattern1, scatter.Read(addr, 32u));

        // Change the data and re-execute (same prepared entries)
        var pattern2 = CreatePattern(32, 0xBB);
        WriteAndVerifyPattern(addr, pattern2);
        scatter.Execute();
        Assert.Equal(pattern2, scatter.Read(addr, 32u));
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void Scatter_PrepareRead_NegativeSize_ReturnsFalse()
    {
        using var scatter = CreateScatter();
        Assert.False(scatter.PrepareRead(HeapAddr(0), unchecked((uint)-1)));
    }

    [Fact]
    public void Scatter_Execute_NoPreparations_Throws()
    {
        using var scatter = CreateScatter();
        Assert.Throws<VmmException>(() => scatter.Execute());
    }

    [Fact]
    public void Scatter_Read_UnpreparedAddress_ReturnsNull()
    {
        using var scatter = CreateScatter();
        ulong preparedAddr = HeapAddr(0x6100);
        ulong unpreparedAddr = HeapAddr(0x7000);

        Assert.True(scatter.PrepareRead(preparedAddr, 32u));
        scatter.Execute();

        // Reading from unprepared address should return null
        Assert.Null(scatter.Read(unpreparedAddr, 32u));
    }

    #endregion
}
