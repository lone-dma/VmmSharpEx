using System.Buffers;

namespace VmmSharpEx.Internal
{
    internal sealed class PooledArray<T> : IMemoryOwner<T>
        where T : unmanaged
    {
        private readonly int _length;
        private T[] _array;
        public Memory<T> Memory => _array.AsMemory(0, _length);

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
