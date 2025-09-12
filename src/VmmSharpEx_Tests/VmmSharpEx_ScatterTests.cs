using System.Text;
using VmmSharpEx;
using VmmSharpEx.Scatter;
using VmmSharpEx_Tests.Fixtures;

namespace VmmSharpEx_Tests;

[Collection(nameof(VmmCollection))]
public class VmmSharpEx_ScatterTests
{
    private readonly Vmm _vmm;
    private readonly uint _pid;
    private readonly ulong _codeCave;

    public VmmSharpEx_ScatterTests(VmmFixture fixture)
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

    [InlineData(100)]
    [InlineData(6)]
    [InlineData(0)]
    [Theory]
    public void ScatterReadMap_MultiIndex_Value_Array_String_Loop(int count)
    {
        // Prepare expected data per index
        var values = new ulong[count];
        var arrays = new byte[count][];
        var strings = new string[count];
        var addrValue = new ulong[count];
        var addrArray = new ulong[count];
        var addrString = new ulong[count];

        for (int ix = 0; ix < count; ix++)
        {
            int i = ix;
            addrValue[i] = Region(0x90000 + (ulong)(i * 0x3000));
            addrArray[i] = Region(0x90000 + (ulong)(i * 0x3000) + 0x800);
            addrString[i] = Region(0x90000 + (ulong)(i * 0x3000) + 0x1000);

            values[i] = 0xABCDEF0000000000UL + (ulong)i;
            arrays[i] = Enumerable.Range(0, 64 + i).Select(b => (byte)((b + i) ^ 0x5A)).ToArray();
            strings[i] = $"ScatterIdx-{i:00}";
            var strBytes = Encoding.ASCII.GetBytes(strings[i] + "\0");

            Assert.True(_vmm.MemWriteValue(_pid, addrValue[i], values[i]));
            Assert.True(_vmm.MemWriteArray(_pid, addrArray[i], arrays[i]));
            Assert.True(_vmm.MemWriteArray(_pid, addrString[i], strBytes));
        }

        using var map = new ScatterReadMap(_vmm, _pid);
        var rd = map.AddRound();

        var seen = new bool[count];

        for (int ix = 0; ix < count; ix++)
        {
            int i = ix; // capture
            rd[i].AddValueEntry<ulong>(0, addrValue[i]);
            rd[i].AddArrayEntry<byte>(1, addrArray[i], arrays[i].Length);
            rd[i].AddStringEntry(2, addrString[i], strings[i].Length + 1, Encoding.ASCII);

            rd[i].Completed += (_, cb) =>
            {
                Assert.True(cb.TryGetValue<ulong>(0, out var v));
                Assert.Equal(values[i], v);

                Assert.True(cb.TryGetArray<byte>(1, out var arr));
                Assert.Equal(arrays[i].Length, arr.Length);
                Assert.True(arrays[i].AsSpan().SequenceEqual(arr));

                Assert.True(cb.TryGetString(2, out var s));
                Assert.Equal(strings[i], s);

                seen[i] = true;
            };
        }

        map.Execute();
        Assert.All(seen, b => Assert.True(b));
    }

    [InlineData(11)]
    [InlineData(5)]
    [InlineData(0)]
    [Theory]
    public void ScatterReadMap_MultiIndex_CrossPage_Loop(int count)
    {
        var starts = new ulong[count];
        var lens = new int[count];
        var expected = new byte[count][];

        for (int ix = 0; ix < count; ix++)
        {
            int i = ix;
            var basePage = Region(0xA0000 + (ulong)(i * 0x4000)) & ~0xfffUL;
            var pattern = Enumerable.Range(0, 8192).Select(x => (byte)((x + i) * 7)).ToArray();
            Assert.True(_vmm.MemWriteArray(_pid, basePage, pattern)); // 2 pages

            var startOffset = 0x700 + (i * 37); // vary offset but keep within page
            var len = 1200 + (i * 111);         // ensure spanning into next page
            starts[i] = basePage + (ulong)startOffset;
            lens[i] = len;
            expected[i] = pattern.AsSpan(startOffset, len).ToArray();
        }

        using var map = new ScatterReadMap(_vmm, _pid);
        var rd = map.AddRound();

        var seen = new bool[count];

        for (int ix = 0; ix < count; ix++)
        {
            int i = ix;
            rd[i].AddArrayEntry<byte>(0, starts[i], lens[i]);
            rd[i].Completed += (_, cb) =>
            {
                Assert.True(cb.TryGetArray<byte>(0, out var result));
                Assert.Equal(lens[i], result.Length);
                Assert.True(expected[i].AsSpan().SequenceEqual(result));
                seen[i] = true;
            };
        }

        map.Execute();
        Assert.All(seen, b => Assert.True(b));
    }

    [InlineData(100)]
    [InlineData(4)]
    [InlineData(0)]
    [Theory]
    public void ScatterReadMap_MultiRound_MultiIndex_Dependent_Loop(int count)
    {
        var ptrAddrs = new ulong[count];
        var bufAddrs = new ulong[count];
        var bufs = new byte[count][];

        for (int ix = 0; ix < count; ix++)
        {
            int i = ix;
            ptrAddrs[i] = Region(0xB0000 + (ulong)(i * 0x3000));
            bufAddrs[i] = Region(0xB0000 + (ulong)(i * 0x3000) + 0x1000);
            bufs[i] = Enumerable.Range(0, 96 + (i * 7)).Select(x => (byte)(255 - ((x + i) & 0xFF))).ToArray();

            Assert.True(_vmm.MemWriteArray(_pid, bufAddrs[i], bufs[i]));
            Assert.True(_vmm.MemWriteValue(_pid, ptrAddrs[i], bufAddrs[i]));
        }

        using var map = new ScatterReadMap(_vmm, _pid);
        var rd1 = map.AddRound();
        var rd2 = map.AddRound(useCache: false);

        var seen1 = new bool[count];
        var seen2 = new bool[count];

        for (int ix = 0; ix < count; ix++)
        {
            int i = ix;
            rd1[i].AddValueEntry<ulong>(0, ptrAddrs[i]);
            rd1[i].Completed += (_, idx1) =>
            {
                Assert.True(idx1.TryGetValue<ulong>(0, out var p));
                rd2[i].AddArrayEntry<byte>(1, p, bufs[i].Length);
                rd2[i].Completed += (_, idx2) =>
                {
                    Assert.True(idx2.TryGetArray<byte>(1, out var outBuf));
                    Assert.Equal(bufs[i].Length, outBuf.Length);
                    Assert.True(bufs[i].AsSpan().SequenceEqual(outBuf));
                    seen2[i] = true;
                };
                seen1[i] = true;
            };
        }

        map.Execute();
        Assert.All(seen1, b => Assert.True(b));
        Assert.All(seen2, b => Assert.True(b));
    }
}