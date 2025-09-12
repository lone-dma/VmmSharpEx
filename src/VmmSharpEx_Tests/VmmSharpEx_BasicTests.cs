using System.Text;
using VmmSharpEx;
using VmmSharpEx_Tests.State;

namespace VmmSharpEx_Tests
{
    public class VmmSharpEx_BasicTests : IClassFixture<VmmFixture>
    {
        private readonly VmmFixture _fixture;
        private readonly Vmm _vmm;

        public VmmSharpEx_BasicTests(VmmFixture fixture)
        {
            _fixture = fixture;
            _vmm = fixture.Vmm; // Shortcut
        }

        [Fact]
        public void CodeCave_ReadHello()
        {
            string result = _vmm.MemReadString(_fixture.PID, _fixture.CodeCave, 12, Encoding.Unicode);
            Assert.StartsWith("hello", result, StringComparison.OrdinalIgnoreCase);
        }
    }
}