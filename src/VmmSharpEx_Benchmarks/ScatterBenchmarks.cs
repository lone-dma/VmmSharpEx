namespace VmmSharpEx_Benchmarks
{
    // TODO: Need to fix this to allow net9.0-windows, cannot use the windows TFM in BenchmarkDotNet by default, but there is probably a workaround.
    // WORKAROUND: Temporarily add net9.0 to TargetFrameworks in VmmSharpEx.csproj.

    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net90)]
    public class ScatterBenchmarks
    {
        private Vmm _vmm;
        private uint _pid;
        private List<ulong> _vas;

        [GlobalSetup(Targets = new[] { nameof(VmmScatter), nameof(VmmScatter_WithClear), nameof(ScatterReadMap) })]
        public void SetupScatter()
        {
            _vmm ??= new Vmm("-device", "fpga", "-norefresh");
            if (!_vmm.PidGetFromName("explorer.exe", out _pid))
                throw new InvalidOperationException("Failed to setup VMM");
            var vads = _vmm.Map_GetVad(_pid);
            if (vads.Length == 0)
                throw new InvalidOperationException("Failed to setup VMM");
            var vas = new List<ulong>();
            foreach (var vad in vads)
            {
                if (vad.vaEnd - vad.vaStart >= 0x1000ul)
                {
                    vas.Add(vad.vaStart);
                }
            }
            _vas = vas;
        }

        [Benchmark]
        public void VmmScatter()
        {
            using var scatter = _vmm.CreateScatter(_pid, VmmSharpEx.Options.VmmFlags.NOCACHE);
            foreach (var va in _vas)
            {
                scatter.PrepareReadArray<byte>(va, 0x1000);
            }
            scatter.Execute();
            foreach (var va in _vas)
            {
                if (scatter.ReadArray<byte>(va, 0x1000) is PooledMemory<byte> array)
                {
                    using (array)
                    {
                        // no-op
                    }
                }
            }
        }

        private static VmmScatter _scatter;
        [Benchmark]
        public void VmmScatter_WithClear()
        {
            _scatter ??= _vmm.CreateScatter(_pid, VmmSharpEx.Options.VmmFlags.NOCACHE);
            _scatter.Clear();
            foreach (var va in _vas)
            {
                _scatter.PrepareReadArray<byte>(va, 0x1000);
            }
            _scatter.Execute();
            foreach (var va in _vas)
            {
                if (_scatter.ReadArray<byte>(va, 0x1000) is PooledMemory<byte> array)
                {
                    using (array)
                    {
                        // no-op
                    }
                }
            }
        }

        [Benchmark]
        public void ScatterReadMap()
        {
            using var map = new ScatterReadMap(_vmm, _pid);
            var rd1 = map.AddRound(useCache: false);
            int i = 0;
            foreach (var va in _vas)
            {
                var idx = rd1[i++];
                idx.AddArrayEntry<byte>(0, va, 0x1000);
                idx.Completed += (sender, idx) =>
                {
                    if (idx.TryGetArray<byte>(0, out var array))
                    {
                        // already populated
                    }
                };
            }
            map.Execute();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _scatter?.Dispose();
            _scatter = default;
            _vmm?.Dispose();
            _vmm = default;
            _pid = default;
            _vas = default;
        }
    }

}
