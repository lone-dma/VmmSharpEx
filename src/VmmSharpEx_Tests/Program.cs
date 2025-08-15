using VmmSharpEx;

namespace VmmSharpEx_Tests
{
    internal class Program
    {
        private static Vmm _vmm;

        static void Main()
        {
            string[] args = new[]
            {
                "-printf",
                "-v",
                "-device",
                "fpga",
                "-waitinitialize"
            };
            _vmm = new Vmm(args);
            var proc = _vmm.CreateProcess("explorer.exe");
        }
    }
}
