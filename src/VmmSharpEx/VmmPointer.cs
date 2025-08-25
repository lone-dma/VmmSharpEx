using System.Runtime.CompilerServices;
using VmmSharpEx.Internal;

namespace VmmSharpEx
{
    /// <summary>
    /// Represents a pointer in the target x64 Windows System.
    /// </summary>
    public readonly struct VmmPointer
    {
        public static implicit operator VmmPointer(ulong x) => x;
        public static implicit operator ulong(VmmPointer x) => x.Value;
        public readonly ulong Value;

        /// <summary>
        /// True if the pointer is a valid virtual address, otherwise False.
        /// </summary>
        public readonly bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Utilities.IsValidVirtualAddress(Value);
        }

        /// <summary>
        /// Throws an <see cref="InvalidOperationException"/> if the pointer is not valid.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void ThrowIfInvalid()
        {
            if (!IsValid)
                throw new InvalidOperationException($"Pointer 0x{Value:X} is not a valid x64 virtual address!");
        }
    }
}
