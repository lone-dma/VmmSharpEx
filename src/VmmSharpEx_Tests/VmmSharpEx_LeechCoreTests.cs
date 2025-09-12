using System.Runtime.InteropServices;
using System.Text;
using VmmSharpEx;
using VmmSharpEx.Options;
using VmmSharpEx_Tests.Fixtures;

namespace VmmSharpEx_Tests;

// Physical memory may not necessarily be contiguous so tests should avoid large buffers.
// Check each virtual address directly with MemVirt2Phys to ensure it is mapped.
// For safety we only work within a single page of memory at the base of the code cave.

[Collection(nameof(VmmCollection))]
public unsafe class VmmSharpEx_LeechCoreTests
{
    private readonly VmmFixture _fixture;
    private readonly LeechCore _lc;
    private readonly ulong _pa;

    public VmmSharpEx_LeechCoreTests(VmmFixture fixture)
    {
        _fixture = fixture;
        _lc = fixture.Vmm.LeechCore;
        _pa = fixture.Vmm.MemVirt2Phys(fixture.PID, fixture.CodeCave);
        if (_pa == 0)
            throw new InvalidOperationException("Unable to map virtual address of Code Cave to Physical Memory!");
    }

    private struct TestStruct
    {
        public ulong A;
        public uint B;
        public short C;
    }

    [Fact]
    public void LeechCore_WriteReadValue_RoundTrip()
    {
        var input = new TestStruct { A = 0x0123456789ABCDEFUL, B = 0xAABBCCDDu, C = unchecked((short)0xFEDC) };
        int cb = sizeof(TestStruct);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cb, 0x1000, nameof(cb)); // Ensure we don't exceed a page

        Assert.True(_lc.WriteValue(_pa, input));
        Assert.True(_lc.ReadValue<TestStruct>(_pa, out var got));
        Assert.Equal(input.A, got.A);
        Assert.Equal(input.B, got.B);
        Assert.Equal(input.C, got.C);
    }

    [Fact]
    public void LeechCore_WriteReadArray_RoundTrip()
    {
        var input = Enumerable.Range(0, 64).Select(i => (ushort)(i ^ 0x55AA)).ToArray();
        int cb = input.Length * sizeof(ushort);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cb, 0x1000, nameof(cb)); // Ensure we don't exceed a page

        Assert.True(_lc.WriteArray(_pa, input));
        var got = _lc.ReadArray<ushort>(_pa, input.Length);
        Assert.NotNull(got);
        Assert.True(input.AsSpan().SequenceEqual(got));
    }

    [Fact]
    public void LeechCore_WriteReadSpan_RoundTrip()
    {
        Span<byte> input = stackalloc byte[256];
        for (int i = 0; i < input.Length; i++) input[i] = (byte)(i * 3);

        int cb = input.Length * sizeof(byte);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cb, 0x1000, nameof(cb)); // Ensure we don't exceed a page

        Assert.True(_lc.WriteSpan(_pa, input));

        Span<byte> output = stackalloc byte[input.Length];
        Assert.True(_lc.ReadSpan(_pa, output));
        Assert.True(input.SequenceEqual(output));
    }

    [Fact]
    public unsafe void LeechCore_WriteRead_RawPointer_RoundTrip()
    {
        const int cb = 256;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cb, 0x1000, nameof(cb)); // Ensure we don't exceed a page

        var src = Marshal.AllocHGlobal(cb);
        var dst = Marshal.AllocHGlobal(cb);
        try
        {
            var srcSpan = new Span<byte>((void*)src, cb);
            for (int i = 0; i < cb; i++) srcSpan[i] = (byte)(255 - (i & 0xFF));

            Assert.True(_lc.Write(_pa, src, (uint)cb));
            Assert.True(_lc.Read(_pa, (void*)dst, (uint)cb));

            var dstSpan = new ReadOnlySpan<byte>((void*)dst, cb);
            Assert.True(srcSpan.SequenceEqual(dstSpan));
        }
        finally
        {
            Marshal.FreeHGlobal(src);
            Marshal.FreeHGlobal(dst);
        }
    }

    [Fact]
    public void LeechCore_ReadPooledArray_Smoke()
    {
        var input = Enumerable.Range(0, 64).Select(i => (int)(i * i)).ToArray();
        int cb = input.Length * sizeof(int);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cb, 0x1000, nameof(cb)); // Ensure we don't exceed a page
        Assert.True(_lc.WriteArray(_pa, input));

        using var lease = _lc.ReadPooledArray<int>(_pa, input.Length);
        Assert.NotNull(lease);
        Assert.Equal(input.Length, lease.Span.Length);
        Assert.True(input.AsSpan().SequenceEqual(lease.Span));
    }

    [Fact]
    public void LeechCore_GetSetOption_Verbose_RoundTrip()
    {
        var original = _lc.GetOption(LcOption.CORE_VERBOSE);
        Assert.True(original.HasValue);

        ulong newVal = original.Value == 0 ? 1UL : 0UL;
        try
        {
            Assert.True(_lc.SetOption(LcOption.CORE_VERBOSE, newVal));
            var after = _lc.GetOption(LcOption.CORE_VERBOSE);
            Assert.True(after.HasValue);
            Assert.Equal(newVal, after.Value);
        }
        finally
        {
            _lc.SetOption(LcOption.CORE_VERBOSE, original!.Value);
        }
    }

    [Fact]
    public void LeechCore_WriteRead_StringUnicode_RoundTrip_PhysOnly()
    {
        var s = "LeechCore-Σ-OK";
        var bytes = Encoding.Unicode.GetBytes(s);
        int cb = bytes.Length * sizeof(byte);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cb, 0x1000, nameof(cb)); // Ensure we don't exceed a page

        Assert.True(_lc.WriteArray(_pa, bytes));

        var gotBytes = _lc.ReadArray<byte>(_pa, bytes.Length);
        Assert.NotNull(gotBytes);
        var read = Encoding.Unicode.GetString(gotBytes);
        Assert.Equal(s, read);
    }
}