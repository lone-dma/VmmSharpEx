/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using System.Text;
using VmmSharpEx;
using VmmSharpEx_Tests.Manual.Internal;
using Xunit.Abstractions;

namespace VmmSharpEx_Tests.Manual;

[Collection(nameof(ManualCollection))]
public class VmmSharpEx_VfsTests
{
    private readonly Vmm _vmm;
    private readonly ITestOutputHelper _output;

    public VmmSharpEx_VfsTests(ManualVmmFixture fixture, ITestOutputHelper output)
    {
        _vmm = fixture.Vmm;
        _output = output;
    }

    [Fact]
    public void TestVfsList()
    {
        var list = _vmm.VfsList("/");
        Assert.NotNull(list);
        Assert.NotEmpty(list);
        foreach (var item in list)
        {
            Assert.NotNull(item.name);
            Assert.NotEmpty(item.name);
            _output.WriteLine(item.name);
        }
    }

    [Fact]
    public void TestVfsRead()
    {
        const string file = "LICENSE.txt";
        var data = _vmm.VfsRead($"/{file}");
        Assert.NotNull(data);
        Assert.NotEmpty(data);
        _output.WriteLine(Encoding.UTF8.GetString(data));
    }
}
