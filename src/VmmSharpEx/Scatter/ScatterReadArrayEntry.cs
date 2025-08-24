// Original Credit to lone-dma

using System.Runtime.CompilerServices;

namespace VmmSharpEx.Scatter
{
    public sealed class ScatterReadArrayEntry<T> : IScatterEntry
        where T : unmanaged
    {
        private static readonly int _cbSingle = Unsafe.SizeOf<T>();
        private readonly int _count;
        private T[] _result;
        /// <summary>
        /// Result for this read. Be sure to check <see cref="IsFailed"/>
        /// </summary>
        internal T[] Result => _result;
        /// <summary>
        /// Virtual Address to read from.
        /// </summary>
        public ulong Address { get; }
        /// <summary>
        /// Count of bytes to read.
        /// </summary>
        public int CB { get; }
        /// <summary>
        /// True if this read has failed, otherwise False.
        /// </summary>
        public bool IsFailed { get; set; }

        internal ScatterReadArrayEntry(ulong address, int count) 
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0, nameof(count));
            Address = address;
            CB = count * _cbSingle;
            _count = count;
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
                var arr = new T[_count];
                if (!IScatterEntry.ProcessData<T>(hScatter, Address, CB, arr))
                {
                    IsFailed = true;
                }
                else
                {
                    _result = arr;
                }
            }
            catch
            {
                IsFailed = true;
            }
        }
    }
}
