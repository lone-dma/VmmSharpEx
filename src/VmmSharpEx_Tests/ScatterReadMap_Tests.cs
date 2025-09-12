using System.Text;
using VmmSharpEx;
using VmmSharpEx.Scatter;
using VmmSharpEx_Tests.Fixtures;

namespace VmmSharpEx_Tests;

[Collection(nameof(VmmCollection))]
public class ScatterReadMap_Tests
{
    private readonly Vmm _vmm;
    private readonly uint _pid;
    private readonly ulong _codeCave;

    public ScatterReadMap_Tests(VmmFixture fixture)
    {
        _vmm = fixture.Vmm;
        _pid = fixture.PID;
        _codeCave = fixture.CodeCave;
    }

    private ulong Region(ulong offset) => _codeCave + offset;

    [Fact]
    public void ScatterReadMap_Basic_Value_Array_String()
    {
        // Arrange distinct regions
        var addrValue = Region(0x60000);
        var addrArray = Region(0x61000);
        var addrString = Region(0x62000);

        // Prepare memory contents
        var value = 0xDEADBEEFCAFEBABEUL;
        Assert.True(_vmm.MemWriteValue(_pid, addrValue, value));

        var bytes = Enumerable.Range(0, 128).Select(i => (byte)(i ^ 0xA5)).ToArray();
        Assert.True(_vmm.MemWriteArray(_pid, addrArray, bytes));

        var text = "HelloScatter";
        var textBytes = Encoding.ASCII.GetBytes(text + "\0");
        Assert.True(_vmm.MemWriteArray(_pid, addrString, textBytes));

        // Act via ScatterReadMap
        using var map = new ScatterReadMap(_vmm, _pid);
        var round = map.AddRound();

        // entries
        round[0].AddValueEntry<ulong>(0, addrValue);
        round[0].AddArrayEntry<byte>(1, addrArray, bytes.Length);
        round[0].AddStringEntry(2, addrString, textBytes.Length, Encoding.ASCII);

        bool seen = false;
        round[0].Completed += (_, idx) =>
        {
            // Assert inside completion
            Assert.True(idx.TryGetValue<ulong>(0, out var gotVal));
            Assert.Equal(value, gotVal);

            Assert.True(idx.TryGetArray<byte>(1, out var arr));
            Assert.Equal(bytes.Length, arr.Length);
            Assert.True(bytes.AsSpan().SequenceEqual(arr));

            Assert.True(idx.TryGetString(2, out var gotStr));
            Assert.Equal(text, gotStr);

            seen = true;
        };

        map.Execute();
        Assert.True(seen);
    }

    [Fact]
    public void ScatterReadMap_CrossPage_Array_Reads()
    {
        // Create a buffer that spans page boundary: start near end of page
        var page = Region(0x70000) & ~0xfffUL;
        var start = page + 0x700; // 0x700 into page
        var length = 3000;        // crosses into next page (0x700 + 3000 > 4096)

        // Prepare 2 pages of patterned data
        var twoPages = Enumerable.Range(0, 8192).Select(i => (byte)(i * 3)).ToArray();
        Assert.True(_vmm.MemWriteArray(_pid, page, twoPages));

        // Expected slice
        var expected = twoPages.AsSpan(0x700, length).ToArray();

        using var map = new ScatterReadMap(_vmm, _pid);
        var round = map.AddRound();

        round[0].AddArrayEntry<byte>(0, start, length);

        bool seen = false;
        round[0].Completed += (_, idx) =>
        {
            Assert.True(idx.TryGetArray<byte>(0, out var result));
            Assert.Equal(length, result.Length);
            Assert.True(expected.AsSpan().SequenceEqual(result));
            seen = true;
        };

        map.Execute();
        Assert.True(seen);
    }

    [Fact]
    public void ScatterReadMap_MultiRound_Dependent_Reads()
    {
        // Round 1 writes a pointer, Round 2 uses it to fetch data
        var addrPtr = Region(0x80000);
        var addrBuf = Region(0x81000);
        var buf = Enumerable.Range(0, 64).Select(i => (byte)(255 - i)).ToArray();

        Assert.True(_vmm.MemWriteArray(_pid, addrBuf, buf));
        Assert.True(_vmm.MemWriteValue(_pid, addrPtr, addrBuf)); // write pointer value (VA)

        using var map = new ScatterReadMap(_vmm, _pid);
        var rd1 = map.AddRound();
        var rd2 = map.AddRound(useCache: false);

        bool s1 = false, s2 = false;

        rd1[0].AddValueEntry<ulong>(0, addrPtr);
        rd1[0].Completed += (_, i1) =>
        {
            s1 = true;
            Assert.True(i1.TryGetValue<ulong>(0, out var p));
            rd2[0].AddArrayEntry<byte>(1, p, buf.Length);
            rd2[0].Completed += (_, i2) =>
            {
                Assert.True(i2.TryGetArray<byte>(1, out var outBuf));
                Assert.True(buf.AsSpan().SequenceEqual(outBuf));
                s2 = true;
            };
        };

        map.Execute();
        Assert.True(s1);
        Assert.True(s2);
    }
}