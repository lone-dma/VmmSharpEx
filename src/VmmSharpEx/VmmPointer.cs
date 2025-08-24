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
        public static implicit operator ulong(VmmPointer x) => x._pointer;
        private readonly ulong _pointer;

        /// <summary>
        /// True if the pointer is a valid virtual address, otherwise False.
        /// </summary>
        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Utilities.IsValidVirtualAddress(_pointer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ThrowIfInvalid()
        {
            if (!IsValid)
                throw new InvalidOperationException($"Pointer 0x{_pointer:X} is not a valid x64 virtual address!");
        }
    }
}
