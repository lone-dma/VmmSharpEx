// Original Credit to lone-dma

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VmmSharpEx.Internal;

namespace VmmSharpEx.Scatter
{
    public sealed class ScatterReadEntry<T> : IScatterEntry
    {
        private static readonly bool _isValueType = !RuntimeHelpers.IsReferenceOrContainsReferences<T>();
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

        internal ScatterReadEntry(ulong address, int cb) 
        {
            Address = address;
            if (cb == 0 && _isValueType)
                cb = Unsafe.SizeOf<T>();
            CB = cb;
        }

        /// <summary>
        /// Parse the memory buffer and set the result value.
        /// Only called internally via API.
        /// </summary>
        /// <param name="hScatter">Scatter read handle.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetResult(VmmSharpEx.LeechCore.LcScatterHandle hScatter)
        {
            try
            {
                if (_isValueType)
                {
                    SetValueResult(hScatter);
                }
                else
                {
                    SetClassResult(hScatter);
                }
            }
            catch
            {
                IsFailed = true;
            }
        }

        /// <summary>
        /// Set the Result from a Value Type.
        /// </summary>
        /// <param name="hScatter">Scatter read handle.</param>
        private unsafe void SetValueResult(VmmSharpEx.LeechCore.LcScatterHandle hScatter)
        {
            int cb = Unsafe.SizeOf<T>();
#pragma warning disable CS8500
            fixed (void* pb = &_result)
            {
                var buffer = new Span<byte>(pb, cb);
                if (!ProcessData(hScatter, buffer))
                {
                    IsFailed = true;
                    return;
                }
            }
#pragma warning restore CS8500
        }

        /// <summary>
        /// Set the Result from a Class Type.
        /// </summary>
        /// <param name="hScatter">Scatter read handle.</param>
        private void SetClassResult(LeechCore.LcScatterHandle hScatter)
        {
            if (this is ScatterReadEntry<byte[]> r1) // Memory Buffer
            {
                byte[] buffer = new byte[CB];
                if (!ProcessData<byte>(hScatter, buffer.AsSpan()))
                {
                    IsFailed = true;
                }
                else
                {
                    r1._result = buffer;
                }
            }
            else
            {
                throw new NotImplementedException($"Type {typeof(T)} not supported!");
            }
        }

        /// <summary>
        /// Process the Scatter Read bytes into the result buffer.
        /// *Callers should verify buffer size*
        /// </summary>
        /// <typeparam name="TBuf">Buffer type</typeparam>
        /// <param name="hScatter">Scatter read handle.</param>
        /// <param name="bufferIn">Result buffer</param>
        /// <exception cref="Exception"></exception>
        private bool ProcessData<TBuf>(LeechCore.LcScatterHandle hScatter, Span<TBuf> bufferIn)
            where TBuf : unmanaged
        {
            var buffer = MemoryMarshal.Cast<TBuf, byte>(bufferIn);
            uint pageOffset = Utilities.BYTE_OFFSET(Address); // Get object offset from the page start address

            var bytesCopied = 0; // track number of bytes copied to ensure nothing is missed
            uint cb = Math.Min((uint)CB, 0x1000 - pageOffset); // bytes to read this page

            uint numPages = Utilities.ADDRESS_AND_SIZE_TO_SPAN_PAGES(Address, (uint)CB); // number of pages to read from (in case result spans multiple pages)
            ulong basePageAddr = Utilities.PAGE_ALIGN(Address);

            for (int p = 0; p < numPages; p++)
            {
                ulong pageAddr = basePageAddr + 0x1000 * (uint)p; // get current page addr
                if (hScatter.Results.TryGetValue(pageAddr, out var scatter)) // retrieve page of mem needed
                {
                    scatter.Data
                        .Slice((int)pageOffset, (int)cb)
                        .CopyTo(buffer.Slice(bytesCopied, (int)cb)); // Copy bytes to buffer
                    bytesCopied += (int)cb;
                }
                else // read failed -> break
                {
                    return false;
                }

                cb = 0x1000; // set bytes to read next page
                if (bytesCopied + cb > CB) // partial chunk last page
                {
                    cb = (uint)CB - (uint)bytesCopied;
                }

                pageOffset = 0x0; // Next page (if any) should start at 0x0
            }

            if (bytesCopied != CB)
            {
                return false;
            }
            return true;
        }
    }
}
