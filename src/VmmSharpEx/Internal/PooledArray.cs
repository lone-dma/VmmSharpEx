using System.Buffers;

namespace VmmSharpEx.Internal
{
    /// <summary>
    /// Custom pooled array implementation.
    /// The built-in <see cref="MemoryPool{T}"/> will allocate an array that can be larger than the requester length.
    /// This implementation guarantees that the exposed length is exactly the requested length.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class PooledArray<T> : IMemoryOwner<T>
        where T : unmanaged
    {
        private readonly int _length;
        private T[] _array;
        public Memory<T> Memory => _array.AsMemory(0, _length);

        private PooledArray() { }

        public PooledArray(int length)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(length, 0);
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
    }
}
