/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using Microsoft.Extensions.ObjectPool;
using System.Buffers;
using System.Runtime.CompilerServices;
using VmmSharpEx.Internal;
using VmmSharpEx.Pools;

namespace VmmSharpEx.Scatter.V2
{
    internal sealed class ScatterReadArrayEntry<T> : IScatterEntry
        where T : unmanaged
    {
        private static readonly ObjectPool<ScatterReadArrayEntry<T>> _pool = VmmPoolManager.ObjectPoolProvider
            .Create<ScatterReadArrayEntry<T>>();

        private int _count;
        private T[] _array;
        internal Span<T> Result => _array.AsSpan(0, _count);
        public ulong Address { get; private set; }
        public int CB { get; private set; }
        public bool IsFailed { get; set; }

        public ScatterReadArrayEntry() { }

        internal static ScatterReadArrayEntry<T> Create(ulong address, int count)
        {
            if (count < 0) // Don't throw exceptions in this path
            {
                count = 0;
            }
            int cb = checked(count * Unsafe.SizeOf<T>());
            var rented = _pool.Get();
            rented.CB = cb;
            rented._array = ArrayPool<T>.Shared.Rent(count);
            rented.Address = address;
            rented._count = count;
            return rented;
        }

        public void SetResult(LeechCore.LcScatterHandle hScatter)
        {
            try
            {
                if (!IScatterEntry.ProcessData<T>(hScatter, Address, _array.AsSpan(0, _count)))
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
            if (Interlocked.Exchange(ref _array, null) is T[] array)
            {
                ArrayPool<T>.Shared.Return(array);
            }
            _count = default;
            Address = default;
            CB = default;
            IsFailed = default;
            return true;
        }
    }
}
