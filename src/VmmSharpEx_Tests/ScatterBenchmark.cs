using VmmSharpEx;
using VmmSharpEx.Scatter;

namespace VmmSharpEx_Tests
{
    internal static class ScatterBenchmark
    {
        private static readonly Vmm _vmm;
        private static readonly uint _pid;
        private static readonly ulong _baseAddress;
        static ScatterBenchmark()
        {
            _vmm = Program.Vmm;
            if (!_vmm.PidGetFromName("explorer.exe", out _pid))
                throw new InvalidOperationException("Failed to get PID");
            _baseAddress = _vmm.ProcessGetModuleBase(_pid, "explorer.exe");
            if (_baseAddress == 0)
                throw new InvalidOperationException("Failed to get base address");
        }

        public static void Run()
        {
            using var map = new ScatterReadMap(_vmm, _pid);
            var rd1 = map.AddRound(useCache: true);
            for (int i = 0; i < 100; i++)
            {
                rd1[i].AddArrayEntry<byte>(0, _baseAddress + 0x1000ul * (uint)i, 0x1000);
                rd1[i].Completed += (sender, cb) =>
                {
                    if (cb.TryGetArray<byte>(0, out var bytes))
                    {
                        
                    }
                };
            }
            map.Execute();
        }
    }
}
