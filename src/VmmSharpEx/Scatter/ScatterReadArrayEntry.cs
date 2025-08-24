// Original Credit to lone-dma

using Microsoft.Extensions.ObjectPool;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace VmmSharpEx.Scatter
{
    public sealed class ScatterReadArrayEntry<T> : IScatterEntry
        where T : unmanaged
    {
        private static readonly int _cbSingle = Unsafe.SizeOf<T>();
        /// <summary>
        /// Object Pool for <see cref="ScatterReadArrayEntry{T}"/>"/>
        /// </summary>
        internal static ObjectPool<ScatterReadArrayEntry<T>> Pool { get; } = 
            new DefaultObjectPoolProvider() { MaximumRetained = int.MaxValue - 1 }
            .Create<ScatterReadArrayEntry<T>>();
        private int _count;
        private T[] _array;
        /// <summary>
        /// Result for this read as <see cref="Span{T}"/>. Be sure to check <see cref="IsFailed"/>
        /// </summary>
        internal Span<T> Result => _array.AsSpan(0, _count);
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

        public ScatterReadArrayEntry() { }

        /// <summary>
        /// Parse the memory buffer and set the result value.
        /// Only called internally via API.
        /// </summary>
        /// <param name="hScatter">Scatter read handle.</param>
        public void SetResult(LeechCore.LcScatterHandle hScatter)
        {
            try
            {
                if (!IScatterEntry.ProcessData<T>(hScatter, Address, CB, _array.AsSpan(0, _count)))
                {
                    IsFailed = true;
                }
            }
            catch
            {
                IsFailed = true;
            }
        }

        internal void Configure(ulong address, int count)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0, nameof(count));
            _array = ArrayPool<T>.Shared.Rent(count);
            Address = address;
            CB = count * _cbSingle;
            _count = count;
        }

        /// <summary>
        /// Internal Only - DO NOT CALL
        /// </summary>
        public void Return()
        {
            Pool.Return(this);
        }

        /// <summary>
        /// Internal Only - DO NOT CALL
        /// </summary>
        public bool TryReset()
        {
            _count = default;
            ArrayPool<T>.Shared.Return(_array);
            _array = default;
            Address = default;
            CB = default;
            IsFailed = default;
            return true;
        }
    }
}
