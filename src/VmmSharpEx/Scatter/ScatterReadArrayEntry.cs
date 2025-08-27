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
        /// <summary>
        /// Result for this read as <see cref="Span{T}"/>. Be sure to check <see cref="IsFailed"/>
        /// </summary>
        internal Span<T> Result => _mem.Memory.Span.Slice(0, _count);
        /// <summary>
        /// Virtual Address to read from.
        /// </summary>
        public ulong Address { get; private set; }
        /// <summary>
        /// Count of bytes to read.
        /// </summary>
        public int CB { get; private set; }
        /// <summary>
        /// True if this read has failed, otherwise False.
        /// </summary>
        public bool IsFailed { get; set; }

        [Obsolete("For internal use only. Construct a ScatterReadMap to begin using this API.")]
        public ScatterReadArrayEntry() { }

        /// <summary>
        /// Rent from the Object Pool.
        /// </summary>
        /// <returns></returns>
        internal static ScatterReadArrayEntry<T> Rent(ulong address, int count)
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

        /// <summary>
        /// Parse the memory buffer and set the result value.
        /// Only called internally via API.
        /// </summary>
        /// <param name="hScatter">Scatter read handle.</param>
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

        /// <summary>
        /// Internal Only - DO NOT CALL
        /// </summary>
        public void Return()
        {
            _pool.Return(this);
        }

        /// <summary>
        /// Internal Only - DO NOT CALL
        /// </summary>
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
