using System.Runtime.InteropServices;
using System.Text;
using VmmSharpEx;
using VmmSharpEx.Options;
using VmmSharpEx_Tests.Fixtures;

namespace VmmSharpEx_Tests;

[Collection(nameof(VmmCollection))]
public class VmmSharpEx_LeechCoreTests
{
    private readonly VmmFixture _fixture;
    private readonly LeechCore _lc;
    private readonly ulong _paBase;

    public VmmSharpEx_LeechCoreTests(VmmFixture fixture)
    {
        _fixture = fixture;
        _lc = fixture.Vmm.LeechCore;

        // Only used to obtain a starting physical address for the test region.
        _paBase = fixture.Vmm.MemVirt2Phys(_fixture.PID, fixture.CodeCave);
        if (_paBase == 0)
            throw new InvalidOperationException("Failed to translate code cave virtual address to physical address!");
    }

    private ulong PA(ulong off) => _paBase + off;

    private struct TestStruct
    {
        public ulong A;
        public uint B;
        public short C;
    }

    [Fact]
    public void LeechCore_WriteReadValue_RoundTrip()
    {
        var pa = PA(0x1000);
        var input = new TestStruct { A = 0x0123456789ABCDEFUL, B = 0xAABBCCDDu, C = unchecked((short)0xFEDC) };

        Assert.True(_lc.WriteValue(pa, input));
        Assert.True(_lc.ReadValue<TestStruct>(pa, out var got));
        Assert.Equal(input.A, got.A);
        Assert.Equal(input.B, got.B);
        Assert.Equal(input.C, got.C);
    }

    [Fact]
    public void LeechCore_WriteReadArray_RoundTrip()
    {
        var pa = PA(0x3000);
        var input = Enumerable.Range(0, 512).Select(i => (ushort)(i ^ 0x55AA)).ToArray();

        Assert.True(_lc.WriteArray(pa, input));
        var got = _lc.ReadArray<ushort>(pa, input.Length);
        Assert.NotNull(got);
        Assert.True(input.AsSpan().SequenceEqual(got));
    }

    [Fact]
    public void LeechCore_WriteReadSpan_RoundTrip()
    {
        var pa = PA(0x5000);
        Span<byte> input = stackalloc byte[1024];
        for (int i = 0; i < input.Length; i++) input[i] = (byte)(i * 3);

        Assert.True(_lc.WriteSpan(pa, input));

        Span<byte> output = stackalloc byte[input.Length];
        Assert.True(_lc.ReadSpan(pa, output));
        Assert.True(input.SequenceEqual(output));
    }

    [Fact]
    public unsafe void LeechCore_WriteRead_RawPointer_RoundTrip()
    {
        var pa = PA(0x7000);
        int cb = 4096;

        var src = Marshal.AllocHGlobal(cb);
        var dst = Marshal.AllocHGlobal(cb);
        try
        {
            var srcSpan = new Span<byte>((void*)src, cb);
            for (int i = 0; i < cb; i++) srcSpan[i] = (byte)(255 - (i & 0xFF));

            Assert.True(_lc.Write(pa, src, (uint)cb));
            Assert.True(_lc.Read(pa, (void*)dst, (uint)cb));

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
        var pa = PA(0x9000);
        var input = Enumerable.Range(0, 256).Select(i => (int)(i * i)).ToArray();
        Assert.True(_lc.WriteArray(pa, input));

        using var lease = _lc.ReadPooledArray<int>(pa, input.Length);
        Assert.NotNull(lease);
        Assert.Equal(input.Length, lease.Span.Length);
        Assert.True(input.AsSpan().SequenceEqual(lease.Span));
    }

    [Fact]
    public void LeechCore_ReadScatter_Pages()
    {
        var page0 = PA(0xB000) & ~0xfffUL;
        var page1 = page0 + 0x1000;

        var buf0 = Enumerable.Range(0, 4096).Select(i => (byte)(i & 0xFF)).ToArray();
        var buf1 = Enumerable.Range(0, 4096).Select(i => (byte)((i * 5) & 0xFF)).ToArray();

        Assert.True(_lc.WriteSpan(page0, buf0.AsSpan()));
        Assert.True(_lc.WriteSpan(page1, buf1.AsSpan()));

        Span<ulong> pas = stackalloc ulong[2] { page0, page1 };
        using var sh = _lc.ReadScatter(pas);

        Assert.True(sh.Results.TryGetValue(page0, out var s0));
        Assert.True(sh.Results.TryGetValue(page1, out var s1));

        Assert.True(buf0.AsSpan().SequenceEqual(s0.Data));
        Assert.True(buf1.AsSpan().SequenceEqual(s1.Data));
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
        var pa = PA(0xD000);
        var s = "LeechCore-Σ-OK";
        var bytes = Encoding.Unicode.GetBytes(s);

        Assert.True(_lc.WriteArray(pa, bytes));

        var gotBytes = _lc.ReadArray<byte>(pa, bytes.Length);
        Assert.NotNull(gotBytes);
        var read = Encoding.Unicode.GetString(gotBytes);
        Assert.Equal(s, read);
    }
}