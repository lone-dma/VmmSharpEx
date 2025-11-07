/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using System.Text;
using VmmSharpEx;
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
    public void Scatter_Basic_Value_Array_String()
    {
        var addrValue = Region(0x1000);
        var addrArray = Region(0x2000);
        var addrString = Region(0x3000);

        // Arrange memory
        ulong value = 0x1122334455667788UL;
        Assert.True(_vmm.MemWriteValue(_pid, addrValue, value));

        var bytes = Enumerable.Range(0, 256).Select(i => (byte)(i ^ 0x3C)).ToArray();
        Assert.True(_vmm.MemWriteArray(_pid, addrArray, bytes));

        var text = "ScatterBasic-OK";
        var textBytes = Encoding.ASCII.GetBytes(text + "\0");
        Assert.True(_vmm.MemWriteArray(_pid, addrString, textBytes));

        using var s = _vmm.CreateScatter(_pid);

        // Prepare reads (order independent)
        Assert.True(s.PrepareReadValue<ulong>(addrValue));
        Assert.True(s.PrepareReadArray<byte>(addrArray, bytes.Length));
        Assert.True(s.PrepareRead(addrString, (uint)textBytes.Length));

        // Execute
        s.Execute();

        // Validate value
        Assert.True(s.ReadValue(addrValue, out ulong gotVal));
        Assert.Equal(value, gotVal);

        // Validate array
        using (var lease = s.ReadArray<byte>(addrArray, bytes.Length))
        {
            Assert.NotNull(lease);
            Assert.Equal(bytes.Length, lease.Span.Length);
            Assert.True(bytes.AsSpan().SequenceEqual(lease.Span));
        }

        // Validate string
        var gotStr = s.ReadString(addrString, textBytes.Length, Encoding.ASCII);
        Assert.Equal(text, gotStr);
    }

    [Fact]
    public void Scatter_PrepareWrite_Value_And_Span()
    {
        var addrValue = Region(0x4000);
        var addrSpan = Region(0x5000);

        ulong value = 0xCAFEBABEDEADBEEFUL;
        Span<byte> span = stackalloc byte[128];
        for (int i = 0; i < span.Length; i++) span[i] = (byte)((i * 17) & 0xFF);

        using var s = _vmm.CreateScatter(_pid);

        Assert.True(s.PrepareWriteValue(addrValue, value));
        Assert.True(s.PrepareWriteSpan(addrSpan, span));

        s.Execute();

        // Verify via direct reads
        Assert.True(_vmm.MemReadValue<ulong>(_pid, addrValue, out var gotV));
        Assert.Equal(value, gotV);

        Span<byte> verify = stackalloc byte[span.Length];
        Assert.True(_vmm.MemReadSpan(_pid, addrSpan, verify));
        Assert.True(span.SequenceEqual(verify));
    }

    [Fact]
    public void Scatter_CrossPage_Array_Read()
    {
        // Prepare two pages of patterned data
        var basePage = Region(0x6000) & ~0xfffUL; // page align inside region
        var pattern = Enumerable.Range(0, 8192).Select(i => (byte)((i * 5) & 0xFF)).ToArray();
        Assert.True(_vmm.MemWriteArray(_pid, basePage, pattern));

        // Choose a span that crosses page boundary
        var start = basePage + 0xF00; // near end of page
        var length = 2048;            // crosses into next page
        var expected = pattern.AsSpan(0xF00, length).ToArray();

        using var s = _vmm.CreateScatter(_pid);
        Assert.True(s.PrepareReadArray<byte>(start, length));
        s.Execute();

        using var lease = s.ReadArray<byte>(start, length);
        Assert.NotNull(lease);
        Assert.Equal(length, lease.Span.Length);
        Assert.True(expected.AsSpan().SequenceEqual(lease.Span));
    }

    [Fact]
    public void ScatterMap_MultiRound_Dependent_Reads()
    {
        // Round1 reads pointer, Round2 reads buffer at that pointer
        var addrPtr = Region(0x7000);
        var addrBuf = Region(0x8000);
        var buffer = Enumerable.Range(0, 96).Select(i => (byte)(255 - i)).ToArray();

        Assert.True(_vmm.MemWriteArray(_pid, addrBuf, buffer));
        Assert.True(_vmm.MemWriteValue(_pid, addrPtr, addrBuf)); // write VA as pointer

        using var map = _vmm.CreateScatterMap(_pid);
        var rd1 = map.AddRound();
        var rd2 = map.AddRound();

        bool seen1 = false, seen2 = false;

        Assert.True(rd1.PrepareReadValue<ulong>(addrPtr));

        rd1.Completed += (_, __) =>
        {
            // Read pointer from rd1’s results
            Assert.True(rd1.ReadValue(addrPtr, out ulong p));
            Assert.NotEqual<ulong>(0, p);

            // Prepare rd2 using pointer discovered in rd1
            Assert.True(rd2.PrepareReadArray<byte>(p, buffer.Length));
            rd2.Completed += (_, ___) =>
            {
                using var lease = rd2.ReadArray<byte>(p, buffer.Length);
                Assert.NotNull(lease);
                Assert.True(buffer.AsSpan().SequenceEqual(lease.Span));
                seen2 = true;
            };
            seen1 = true;
        };

        map.Execute();
        Assert.True(seen1);
        Assert.True(seen2);
    }
}
