// Original Credit to lone-dma

using Microsoft.Extensions.ObjectPool;
using System.Runtime.CompilerServices;
using System.Text;

namespace VmmSharpEx.Scatter
{
    /// <summary>
    /// Single scatter read index. May contain multiple child entries.
    /// </summary>
    public sealed class ScatterReadIndex : IResettable
    {
        private static readonly ObjectPool<ScatterReadIndex> _pool =
            new DefaultObjectPoolProvider() { MaximumRetained = int.MaxValue - 1 }
            .Create<ScatterReadIndex>();

        /// <summary>
        /// All read entries for this index.
        /// [KEY] = ID
        /// [VALUE] = IScatterEntry
        /// </summary>
        internal Dictionary<int, IScatterEntry> Entries { get; } = new();
        /// <summary>
        /// Event is fired after the completion of all reads within this index and it's parent round.
        /// NOTE: Exception(s) that occur within subscriber code are caught and ignored.
        /// </summary>
        public event EventHandler<ScatterReadIndex> Completed;
        internal void OnCompleted()
        {
            try
            {
                Completed?.Invoke(this, this);
            }
            catch { }
        }

        [Obsolete("For internal use only. Construct a ScatterReadMap to begin using this API.")]
        public ScatterReadIndex() { }

        /// <summary>
        /// Rent from the Object Pool.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ScatterReadIndex Rent() => _pool.Get();

        /// <summary>
        /// Add a scatter read value entry to this index.
        /// Use <see cref="TryGetValue{TOut}"/> or <see cref="GetValueRef{TOut}(int)"/> to obtain the result.
        /// </summary>
        /// <typeparam name="T">Type to read.</typeparam>
        /// <param name="id">Unique ID for this entry.</param>
        /// <param name="address">Virtual Address to read from.</param>
        public void AddValueEntry<T>(int id, ulong address)
            where T : unmanaged
        {
            var entry = ScatterReadValueEntry<T>.Rent();
            entry.Configure(address);
            Entries.Add(id, entry);
        }

        /// <summary>
        /// Add a scatter read array entry to this index.
        /// Use <see cref="TryGetArray{TOut}(int, out Span{TOut})"/> to obtain the result.
        /// </summary>
        /// <typeparam name="T">Type to read.</typeparam>
        /// <param name="id">Unique ID for this entry.</param>
        /// <param name="address">Virtual Address to read from.</param>
        /// <param name="count">Number of array *elements* to read.</param>
        public void AddArrayEntry<T>(int id, ulong address, int count)
            where T : unmanaged
        {
            var entry = ScatterReadArrayEntry<T>.Rent();
            entry.Configure(address, count);
            Entries.Add(id, entry);
        }

        /// <summary>
        /// Add a scatter read string entry to this index.
        /// Use <see cref="TryGetString(int, out string)"/> to obtain the result.
        /// </summary>
        /// <param name="id">Unique ID for this entry.</param>
        /// <param name="address">Virtual Address to read from.</param>
        /// <param name="cb">Number of bytes to read.</param>
        /// <param name="encoding">Encoding to decode string with.</param>
        public void AddStringEntry(int id, ulong address, int cb, Encoding encoding)
        {
            var entry = ScatterReadStringEntry.Rent();
            entry.Configure(address, cb, encoding);
            Entries.Add(id, entry);
        }

        /// <summary>
        /// Try obtain a value result from the requested Entry ID.
        /// </summary>
        /// <typeparam name="TOut">Result Value Type <typeparamref name="TOut"/></typeparam>
        /// <param name="id">ID for entry to lookup.</param>
        /// <param name="result">Result field to populate.</param>
        /// <returns>True if successful, otherwise False.</returns>
        public bool TryGetValue<TOut>(int id, out TOut result)
            where TOut : unmanaged
        {
            if (Entries.TryGetValue(id, out var entry) && entry is ScatterReadValueEntry<TOut> casted && !casted.IsFailed)
            {
                result = casted.Result;
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Try obtain an array result from the requested Entry ID.
        /// </summary>
        /// <typeparam name="TOut">Result Array Type <typeparamref name="TOut"/></typeparam>
        /// <param name="id">ID for entry to lookup.</param>
        /// <param name="result">Result field to populate.</param>
        /// <returns>True if successful, otherwise False.</returns>
        public bool TryGetArray<TOut>(int id, out Span<TOut> result)
            where TOut : unmanaged
        {
            if (Entries.TryGetValue(id, out var entry) && entry is ScatterReadArrayEntry<TOut> casted && !casted.IsFailed)
            {
                result = casted.Result;
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Try obtain a string result from the requested Entry ID.
        /// </summary>
        /// <param name="id">ID for entry to lookup.</param>
        /// <param name="result">Result field to populate.</param>
        /// <returns>True if successful, otherwise False.</returns>
        public bool TryGetString(int id, out string result)
        {
            if (Entries.TryGetValue(id, out var entry) && entry is ScatterReadStringEntry casted && !casted.IsFailed)
            {
                result = casted.Result;
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Try obtain a ref result from the requested Entry ID.
        /// NOTE: Only for <see cref="ScatterReadValueEntry{T}"/>
        /// WARNING: Must check the returned ref result for NULLPTR with <see cref="Unsafe.IsNullRef"/>
        /// </summary>
        /// <typeparam name="TOut">Result Type <typeparamref name="TOut"/></typeparam>
        /// <param name="id">ID for entry to lookup.</param>
        /// <returns>Ref if successful, otherwise NULL.</returns>
        public ref TOut GetValueRef<TOut>(int id)
            where TOut : unmanaged
        {
            if (Entries.TryGetValue(id, out var entry) && entry is ScatterReadValueEntry<TOut> casted && !casted.IsFailed)
            {
                return ref casted.Result;
            }
            return ref Unsafe.NullRef<TOut>();
        }

        internal void Return()
        {
            _pool.Return(this);
        }

        /// <summary>
        /// Internal Only - DO NOT CALL
        /// </summary>
        public bool TryReset()
        {
            Completed = null;
            foreach (var entry in Entries.Values)
            {
                entry.Return();
            }
            Entries.Clear();
            return true;
        }
    }
}
