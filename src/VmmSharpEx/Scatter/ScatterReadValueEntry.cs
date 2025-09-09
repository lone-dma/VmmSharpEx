using Microsoft.Extensions.ObjectPool;
using VmmSharpEx.Internal;
using VmmSharpEx.Pools;

namespace VmmSharpEx.Scatter
{
    internal sealed class ScatterReadValueEntry<T> : IScatterEntry
        where T : unmanaged
    {
        private static readonly ObjectPool<ScatterReadValueEntry<T>> _pool = VmmPoolManager.ObjectPoolProvider
            .Create<ScatterReadValueEntry<T>>();

        private T _result = default;
        internal ref T Result => ref _result;
        public ulong Address { get; private set; }
        public int CB { get; } = SizeCache<T>.Size;
        public bool IsFailed { get; set; }

        public ScatterReadValueEntry() { }

        internal static ScatterReadValueEntry<T> Create(ulong address)
        {
            var rented = _pool.Get();
            rented.Address = address;
            return rented;
        }

        public unsafe void SetResult(LeechCore.LcScatterHandle hScatter)
        {
            try
            {
                fixed (void* pb = &_result)
                {
                    var data = new Span<byte>(pb, CB);
                    if (!IScatterEntry.ProcessData<byte>(hScatter, Address, data))
                    {
                        IsFailed = true;
                    }
                    else if (_result is VmmPointer ptr && !ptr.IsValid)
                    {
                        IsFailed = true; // Invalid pointer value
                    }
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
            _result = default;
            Address = default;
            IsFailed = default;
            return true;
        }
    }
}
