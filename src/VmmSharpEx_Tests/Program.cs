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
            using var search = proc.CreateSearch();
            search.AddEntry(new byte[] { 0x00, 0x00, 0x01, 0x00, 0x01 });
            var result = search.Result;
            Console.WriteLine($"Found {result.Results.Count} results.");
        }
    }
}
