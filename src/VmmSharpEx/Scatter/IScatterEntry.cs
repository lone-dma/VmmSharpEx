// Original Credit to lone-dma

using System.Runtime.InteropServices;
using Microsoft.Extensions.ObjectPool;
using VmmSharpEx.Internal;

namespace VmmSharpEx.Scatter
{
    public interface IScatterEntry : IResettable
    {
        /// <summary>
        /// Address to read from.
        /// </summary>
        ulong Address { get; }
        /// <summary>
        /// Count of bytes to read.
        /// </summary>
        int CB { get; }
        /// <summary>
        /// True if this read has failed, otherwise False.
        /// </summary>
        bool IsFailed { get; set; }

        /// <summary>
        /// Parse the memory buffer and set the result value.
        /// </summary>
        /// <param name="hScatter">Scatter read handle.</param>
        void SetResult(LeechCore.LcScatterHandle hScatter);
        /// <summary>
        /// Return this instance to the Object Pool.
        /// </summary>
        void Return();

        #region Static Interface

        /// <summary>
        /// Maximum read size for any single entry.
        /// DEFAULT: No Limit (<see cref="uint.MaxValue"/>)
        /// </summary>
        public static uint MaxReadSize { get; set; } = uint.MaxValue;

        /// <summary>
        /// Process the Scatter Read bytes into the result buffer.
        /// </summary>
        /// <typeparam name="TBuf">Buffer type</typeparam>
        /// <param name="hScatter">Scatter read handle.</param>
        /// <param name="addr">Address of read.</param>
        /// <param name="result">Result buffer</param>
        /// <returns>TRUE if successful, otherwise FALSE.</returns>
        internal static unsafe bool ProcessData<TBuf>(LeechCore.LcScatterHandle hScatter, ulong addr, Span<TBuf> result)
            where TBuf : unmanaged
        {
            int cbTotal = sizeof(TBuf) * result.Length;
            var resultOut = MemoryMarshal.Cast<TBuf, byte>(result);
            uint pageOffset = Utilities.BYTE_OFFSET(addr); // Get object offset from the page start address

            var bytesCopied = 0; // track number of bytes copied to ensure nothing is missed
            uint cb = Math.Min((uint)cbTotal, 0x1000 - pageOffset); // bytes to read this page

            uint numPages = Utilities.ADDRESS_AND_SIZE_TO_SPAN_PAGES(addr, (uint)cbTotal); // number of pages to read from (in case result spans multiple pages)
            ulong basePageAddr = Utilities.PAGE_ALIGN(addr);

            for (int p = 0; p < numPages; p++)
            {
                ulong pageAddr = basePageAddr + 0x1000 * (uint)p; // get current page addr
                if (hScatter.Results.TryGetValue(pageAddr, out var scatter)) // retrieve page of mem needed
                {
                    scatter.Data
                        .Slice((int)pageOffset, (int)cb)
                        .CopyTo(resultOut.Slice(bytesCopied, (int)cb)); // Copy bytes to buffer
                    bytesCopied += (int)cb;
                }
                else // read failed -> break
                {
                    return false;
                }

                cb = 0x1000; // set bytes to read next page
                if (bytesCopied + cb > cbTotal) // partial chunk last page
                {
                    cb = (uint)cbTotal - (uint)bytesCopied;
                }

                pageOffset = 0x0; // Next page (if any) should start at 0x0
            }

            if (bytesCopied != cbTotal)
            {
                return false;
            }
            return true;
        }

        #endregion
    }
}
