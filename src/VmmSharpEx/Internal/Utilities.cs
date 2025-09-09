using System.Runtime.CompilerServices;

namespace VmmSharpEx.Internal
{
    internal static class Utilities
    {
        /// <summary>
        /// Checks if a Virtual Address is valid.
        /// </summary>
        /// <param name="va">Virtual Address to validate.</param>
        /// <returns>True if valid, otherwise False.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidVirtualAddress(ulong va)
        {
            return va >= 0x10000 && ((long)va << 16) >> 16 == (long)va;
        }

        /// <summary>
        /// The PAGE_ALIGN macro returns a page-aligned virtual address for a given virtual address.
        /// https://learn.microsoft.com/windows-hardware/drivers/ddi/wdm/nf-wdm-page_align
        /// </summary>
        /// <param name="va">Virtual address.</param>
        /// <returns>Page-aligned virtual address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong PAGE_ALIGN(ulong va) => va & ~(0x1000ul - 1);

        /// <summary>
        /// The BYTE_OFFSET macro takes a virtual address and returns the byte offset of that address within the page.
        /// https://learn.microsoft.com/windows-hardware/drivers/ddi/wdm/nf-wdm-byte_offset
        /// </summary>
        /// <param name="va">virtual address.</param>
        /// <returns>Offset portion of the virtual address within the page.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint BYTE_OFFSET(ulong va) => (uint)(va & (0x1000ul - 1));

        /// <summary>
        /// The ADDRESS_AND_SIZE_TO_SPAN_PAGES macro returns the number of pages that a virtual range spans.
        /// The virtual range is defined by a virtual address and the size in bytes of a transfer request.
        /// https://learn.microsoft.com/windows-hardware/drivers/ddi/wdm/nf-wdm-address_and_size_to_span_pages
        /// </summary>
        /// <param name="va">Virtual address that is the base of the range.</param>
        /// <param name="size">Specifies the size in bytes.</param>
        /// <returns>Returns the number of pages spanned by the virtual range starting at Va.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ADDRESS_AND_SIZE_TO_SPAN_PAGES(ulong va, ulong size) =>
            (BYTE_OFFSET(va) + size + (0x1000ul - 1)) >> 12;
    }
}
