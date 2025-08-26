// Original Credit to lone-dma

using Microsoft.Extensions.ObjectPool;
using System.Runtime.CompilerServices;

namespace VmmSharpEx.Scatter
{
    public sealed class ScatterReadValueEntry<T> : IScatterEntry
        where T : unmanaged
    {
        private static readonly int _cb = Unsafe.SizeOf<T>();
        private static readonly ObjectPool<ScatterReadValueEntry<T>> _pool = 
            new DefaultObjectPoolProvider() { MaximumRetained = int.MaxValue - 1 }
            .Create<ScatterReadValueEntry<T>>();

        private T _result = default;
        /// <summary>
        /// Result for this read. Be sure to check <see cref="IsFailed"/>
        /// </summary>
        internal ref T Result => ref _result;
        /// <summary>
        /// Virtual Address to read from.
        /// </summary>
        public ulong Address { get; private set; }
        /// <summary>
        /// Count of bytes to read.
        /// </summary>
        public int CB { get; } = _cb;
        /// <summary>
        /// True if this read has failed, otherwise False.
        /// </summary>
        public bool IsFailed { get; set; }

        [Obsolete("For internal use only. Construct a ScatterReadMap to begin using this API.")]
        public ScatterReadValueEntry() { }

        /// <summary>
        /// Rent from the Object Pool.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ScatterReadValueEntry<T> Rent() => _pool.Get();

        /// <summary>
        /// Parse the memory buffer and set the result value.
        /// Only called internally via API.
        /// </summary>
        /// <param name="hScatter">Scatter read handle.</param>
        public unsafe void SetResult(LeechCore.LcScatterHandle hScatter)
        {
            try
            {
                fixed (void* pb = &_result)
                {
                    var buffer = new Span<byte>(pb, CB);
                    if (!IScatterEntry.ProcessData<byte>(hScatter, Address, buffer))
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

        internal void Configure(ulong address)
        {
            Address = address;
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
            _result = default;
            Address = default;
            IsFailed = default;
            return true;
        }
    }
}
