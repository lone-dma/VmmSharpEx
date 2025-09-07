using System.Buffers;

namespace VmmSharpEx.Pools
{
    /// <summary>
    /// Represents a pooled array of unmanaged types.
    /// </summary>
    /// <typeparam name="T">Unmanaged value type.</typeparam>
    public interface IVmmPooledArray<T> : IMemoryOwner<T>, IEnumerable<T>, IReadOnlyList<T>, IDisposable
        where T : unmanaged
    {
        /// <summary>
        /// A span over the pooled array.
        /// The length of the span is exactly the length requested when the instance was created.
        /// </summary>
        Span<T> Span { get; }
    }
}
