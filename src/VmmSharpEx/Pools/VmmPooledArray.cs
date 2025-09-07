using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;

namespace VmmSharpEx.Pools
{
    /// <summary>
    /// Custom pooled array implementation.
    /// The built-in <see cref="MemoryPool{T}"/>/<see cref="ArrayPool{T}"/> will allocate an array that can be larger than the requested length.
    /// This implementation guarantees that the exposed length is exactly the requested length.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class VmmPooledArray<T> : IVmmPooledArray<T>
        where T : unmanaged
    {
        private readonly int _length;
        private T[] _array;
        public Memory<T> Memory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array.AsMemory(0, _length);
        }
        public Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array.AsSpan(0, _length);
        }

        public int Count => _length;

        public T this[int index] => Span[index];

        private VmmPooledArray() { }

        public VmmPooledArray(int length)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(length, 0, nameof(length));
            _length = length;
            _array = ArrayPool<T>.Shared.Rent(length);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _array, null) is T[] array)
            {
                ArrayPool<T>.Shared.Return(array);
            }
        }

        public Span<T>.Enumerator GetEnumerator() => Span.GetEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            var mem = Memory;
            for (int i = 0; i < mem.Length; i++)
            {
                yield return mem.Span[i];
            }
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            var mem = Memory;
            for (int i = 0; i < mem.Length; i++)
            {
                yield return mem.Span[i];
            }
        }
    }
}
