// Original Credit to lone-dma

using Microsoft.Extensions.ObjectPool;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace VmmSharpEx.Scatter
{
    internal sealed class ScatterReadArrayEntry<T> : IScatterEntry
        where T : unmanaged
    {
        private static readonly int _cbSingle = Unsafe.SizeOf<T>();
        private static readonly ObjectPool<ScatterReadArrayEntry<T>> _pool = 
            new DefaultObjectPoolProvider() { MaximumRetained = int.MaxValue - 1 }
            .Create<ScatterReadArrayEntry<T>>();

        private int _count;
        private IMemoryOwner<T> _mem;
        internal Span<T> Result => _mem.Memory.Span.Slice(0, _count);
        public ulong Address { get; private set; }
        public int CB { get; private set; }
        public bool IsFailed { get; set; }

        public ScatterReadArrayEntry() { }

        internal static ScatterReadArrayEntry<T> Create(ulong address, int count)
        {
            var rented = _pool.Get();
            if (count < 0)
            {
                count = 0;
            }
            rented._mem = MemoryPool<T>.Shared.Rent(count);
            rented.Address = address;
            rented.CB = count * _cbSingle;
            rented._count = count;
            return rented;
        }

        public void SetResult(LeechCore.LcScatterHandle hScatter)
        {
            try
            {
                if (!IScatterEntry.ProcessData<T>(hScatter, Address, Result))
                {
                    IsFailed = true;
                }
            }
            catch
            {
                IsFailed = true;
            }
        }

        public void Return()
        {
            _pool.Return(this);
        }

        public bool TryReset()
        {
            _mem?.Dispose();
            _mem = default;
            _count = default;
            Address = default;
            CB = default;
            IsFailed = default;
            return true;
        }
    }
}
