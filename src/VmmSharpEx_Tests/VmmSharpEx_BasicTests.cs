using System.Text;
using VmmSharpEx;
using VmmSharpEx_Tests.Fixtures;

namespace VmmSharpEx_Tests;

[Collection(nameof(VmmCollection))]
public class VmmSharpEx_BasicTests
{
    private readonly VmmFixture _fixture;
    private readonly Vmm _vmm;

    public VmmSharpEx_BasicTests(VmmFixture fixture)
    {
        _fixture = fixture;
        _vmm = fixture.Vmm; // Shortcut
    }
}