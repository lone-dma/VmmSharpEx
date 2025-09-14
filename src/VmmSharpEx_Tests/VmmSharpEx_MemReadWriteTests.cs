using System.Text;
using VmmSharpEx;
using VmmSharpEx_Tests.Fixtures;

namespace VmmSharpEx_Tests;

[Collection(nameof(VmmCollection))]
public class VmmSharpEx_MemReadWriteTests
{
    private readonly Vmm _vmm;
    private readonly uint _pid;
    private readonly ulong _codeCave;

    public VmmSharpEx_MemReadWriteTests(VmmFixture fixture)
    {
        _vmm = fixture.Vmm;
        _pid = fixture.PID;
        _codeCave = fixture.CodeCave;
    }

    private ulong Region(ulong offset) => _codeCave + offset;

    private struct TestStruct
    {
        public ulong X;
        public uint Y;
        public short Z;
    }

    [Fact]
    public unsafe void WriteRead_Value_RoundTrip()
    {
        var addr = Region(0x1000);
        var input = new TestStruct { X = 0x1122334455667788UL, Y = 0xAABBCCDDu, Z = unchecked((short)0xFEDC) };

        Assert.True(_vmm.MemWriteValue(_pid, addr, input));

        Assert.True(_vmm.MemReadValue<TestStruct>(_pid, addr, out var output));
        Assert.Equal(input.X, output.X);
        Assert.Equal(input.Y, output.Y);
        Assert.Equal(input.Z, output.Z);
    }

    [Fact]
    public unsafe void WriteRead_Array_RoundTrip()
    {
        var addr = Region(0x2000);
        var input = Enumerable.Range(0, 256).Select(i => i * 7).ToArray();

        Assert.True(_vmm.MemWriteArray(_pid, addr, input));

        var output = _vmm.MemReadArray<int>(_pid, addr, input.Length);
        Assert.NotNull(output);
        Assert.Equal(input.Length, output.Length);
        Assert.True(input.SequenceEqual(output));
    }

    [Fact]
    public unsafe void WriteRead_Span_RoundTrip()
    {
        var addr = Region(0x3000);
        var inputArr = Enumerable.Range(0, 512).Select(i => (byte)(i & 0xFF)).ToArray();
        var input = inputArr.AsSpan();

        Assert.True(_vmm.MemWriteSpan(_pid, addr, input));

        Span<byte> output = stackalloc byte[input.Length];
        Assert.True(_vmm.MemReadSpan(_pid, addr, output));
        Assert.True(input.SequenceEqual(output.ToArray()));
    }

    [Fact]
    public unsafe void WriteRead_PooledArray_RoundTrip()
    {
        var addr = Region(0x4000);
        var input = Enumerable.Range(0, 1024).Select(i => (ushort)(i ^ 0x55AA)).ToArray();

        Assert.True(_vmm.MemWriteArray(_pid, addr, input));

        using var lease = _vmm.MemReadPooledArray<ushort>(_pid, addr, input.Length);
        Assert.NotNull(lease);
        Assert.Equal(input.Length, lease.Span.Length);
        Assert.True(input.AsSpan().SequenceEqual(lease.Span));
    }

    [Fact]
    public unsafe void WriteRead_RawBytes_And_ReadString_Unicode()
    {
        var addr = Region(0x5000);
        var s = "UnitTest-Σ🚀";
        var bytes = Encoding.Unicode.GetBytes(s + "\0"); // NT-terminated

        Assert.True(_vmm.MemWriteArray(_pid, addr, bytes));

        var read = _vmm.MemReadString(_pid, addr, bytes.Length, Encoding.Unicode);
        Assert.NotNull(read);
        Assert.Equal(s, read);
    }

    [Fact]
    public unsafe void PrefetchPages_Smoke()
    {
        // Prefetch three pages near our CodeCave working regions
        Span<ulong> vas =
        [
            Region(0x1000),
            Region(0x2000),
            Region(0x3000),
        ];
        Assert.True(_vmm.MemPrefetchPages(_pid, vas));
    }
}