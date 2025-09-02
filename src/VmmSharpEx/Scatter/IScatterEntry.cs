using System.Runtime.InteropServices;
using Microsoft.Extensions.ObjectPool;
using VmmSharpEx.Internal;

namespace VmmSharpEx.Scatter
{
    internal interface IScatterEntry : IResettable
    {
        /// <summary>
        /// Virtual Address to read from.
        /// </summary>
        ulong Address { get; }
        /// <summary>
        /// Count of bytes to read.
        /// </summary>
        int CB { get; }
        /// <summary>
        /// TRUE if this read has failed, otherwise FALSE.
        /// </summary>
        bool IsFailed { get; set; }

        /// <summary>
        /// Parse the Scatter Read and set the result value.
        /// </summary>
        /// <param name="hScatter">Scatter read handle.</param>
        void SetResult(LeechCore.LcScatterHandle hScatter);
        /// <summary>
        /// Return this instance to the Object Pool.
        /// </summary>
        void Return();

        #region Static Interface

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
            var resultOut = MemoryMarshal.Cast<TBuf, byte>(result);
            int cbTotal = resultOut.Length; // After casting Length will be adjusted to number of byte elements for our total count of bytes
            int pageOffset = (int)Utilities.BYTE_OFFSET(addr); // Get object offset from the page start address
            
            int cb = Math.Min(cbTotal, 0x1000 - pageOffset); // bytes to read current page
            int cbRead = 0; // track number of bytes copied to ensure nothing is missed

            int numPages = (int)Utilities.ADDRESS_AND_SIZE_TO_SPAN_PAGES(addr, (uint)cbTotal); // number of pages to read from (in case result spans multiple pages)
            ulong basePageAddr = Utilities.PAGE_ALIGN(addr);

            for (uint p = 0; p < numPages; p++)
            {
                ulong pageAddr = basePageAddr + 0x1000 * p; // get current page addr
                if (hScatter.Results.TryGetValue(pageAddr, out var scatter)) // retrieve page of mem needed
                {
                    scatter.Data
                        .Slice(pageOffset, cb)
                        .CopyTo(resultOut.Slice(cbRead, cb)); // Copy bytes to buffer
                    cbRead += cb;
                }
                else // read failed -> break
                {
                    return false;
                }

                cb = Math.Clamp(cbTotal - cbRead, 0, 0x1000); // Size the next read
                pageOffset = 0x0; // Next page (if any) should start at 0x0
            }

            if (cbRead != cbTotal)
            {
                return false;
            }
            return true;
        }

        #endregion
    }
}
