using VmmSharpEx.Internal;

namespace VmmSharpEx.Scatter
{
    /// <summary>
    /// Represents an x64 Pointer for Scatter operations.
    /// Validated by VmmSharpEx during read operations.
    /// </summary>
    public readonly struct ScatterPointer
    {
        public static implicit operator ScatterPointer(ulong x) => x;
        public static implicit operator ulong(ScatterPointer x) => x.Value;
        public readonly ulong Value;

        /// <summary>
        /// True if the pointer is a valid virtual address, otherwise False.
        /// </summary>
        public bool IsValid => Utilities.IsValidVirtualAddress(Value);
    }
}
