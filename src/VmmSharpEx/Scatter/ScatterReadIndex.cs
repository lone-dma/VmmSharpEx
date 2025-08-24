// Original Credit to lone-dma

using System.Runtime.CompilerServices;

namespace VmmSharpEx.Scatter
{
    /// <summary>
    /// Single scatter read index. May contain multiple child entries.
    /// </summary>
    public sealed class ScatterReadIndex
    {
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

        internal ScatterReadIndex() { }

        /// <summary>
        /// Add a scatter read entry to this index.
        /// </summary>
        /// <typeparam name="T">Type to read.</typeparam>
        /// <param name="id">Unique ID for this entry.</param>
        /// <param name="address">Virtual Address to read from.</param>
        /// <param name="cb">(Reference Types Only) Count of bytes to read. For unmanaged structs/value types this is sized automatically.</param>
        public ScatterReadEntry<T> AddEntry<T>(int id, ulong address, int cb = 0)
        {
            var entry = new ScatterReadEntry<T>(address, cb);
            Entries.Add(id, entry);
            return entry;
        }

        /// <summary>
        /// Try obtain a result from the requested Entry ID.
        /// </summary>
        /// <typeparam name="TOut">Result Type <typeparamref name="TOut"/></typeparam>
        /// <param name="id">ID for entry to lookup.</param>
        /// <param name="result">Result field to populate.</param>
        /// <returns>True if successful, otherwise False.</returns>
        public bool TryGetResult<TOut>(int id, out TOut result)
        {
            if (Entries.TryGetValue(id, out var entry) && entry is ScatterReadEntry<TOut> casted && !casted.IsFailed)
            {
                result = casted.Result;
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Try obtain a ref result from the requested Entry ID.
        /// WARNING: Must check the returned ref result for NULLPTR with <see cref="Unsafe.IsNullRef"/>
        /// </summary>
        /// <typeparam name="TOut">Result Type <typeparamref name="TOut"/></typeparam>
        /// <param name="id">ID for entry to lookup.</param>
        /// <returns>Ref if successful, otherwise NULL.</returns>
        public ref TOut GetRef<TOut>(int id)
        {
            if (Entries.TryGetValue(id, out var entry) && entry is ScatterReadEntry<TOut> casted && !casted.IsFailed)
            {
                return ref casted.Result;
            }
            return ref Unsafe.NullRef<TOut>();
        }
    }
}
