// Original Credit to lone-dma

using System.Runtime.CompilerServices;

namespace VmmSharpEx.Scatter
{
    public sealed class ScatterReadValueEntry<T> : IScatterEntry
        where T : unmanaged
    {
        private static readonly int _cb = Unsafe.SizeOf<T>();
        private T _result;
        /// <summary>
        /// Result for this read. Be sure to check <see cref="IsFailed"/>
        /// </summary>
        internal ref T Result => ref _result;
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

        internal ScatterReadValueEntry(ulong address) 
        {
            Address = address;
            CB = _cb;
        }

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
                    if (!IScatterEntry.ProcessData(hScatter, Address, CB, buffer))
                    {
                        IsFailed = true;
                    }
                }
            }
            catch
            {
                IsFailed = true;
            }
        }
    }
}
