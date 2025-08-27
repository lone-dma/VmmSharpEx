// Original Credit to lone-dma

using Microsoft.Extensions.ObjectPool;
using VmmSharpEx.Internal;
using VmmSharpEx.Options;

namespace VmmSharpEx.Scatter
{
    /// <summary>
    /// Defines a Scatter Read Round. Each round will execute a single scatter read. If you have reads that
    /// are dependent on previous reads (chained pointers for example), you may need multiple rounds.
    /// </summary>
    public sealed class ScatterReadRound : IResettable
    {
        private static readonly ObjectPool<ScatterReadRound> _pool =
            new DefaultObjectPoolProvider() { MaximumRetained = int.MaxValue - 1 }
            .Create<ScatterReadRound>();

        private readonly Dictionary<int, ScatterReadIndex> _indexes = new();
        private bool _useCache;

        [Obsolete("For internal use only. Construct a ScatterReadMap to begin using this API.")]
        public ScatterReadRound() { }

        /// <summary>
        /// Rent from the Object Pool.
        /// </summary>
        /// <returns></returns>
        internal static ScatterReadRound Rent(bool useCache)
        {
            var rented = _pool.Get();
            rented._useCache = useCache;
            return rented;
        }

        /// <summary>
        /// Returns the requested ScatterReadIndex.
        /// </summary>
        /// <param name="index">Index to retrieve.</param>
        /// <returns>ScatterReadIndex object.</returns>
        public ScatterReadIndex this[int index]
        {
            get
            {
                if (_indexes.TryGetValue(index, out var existing))
                {
                    return existing;
                }
                return _indexes[index] = ScatterReadIndex.Rent();
            }
        }

        /// <summary>
        /// Returns the requested ScatterReadIndex.
        /// NOTE: You can index directly via the indexer, this method is just a convenience wrapper.
        /// </summary>
        /// <param name="index">Index to retrieve.</param>
        /// <returns>ScatterReadIndex object.</returns>
        public ScatterReadIndex GetIndex(int index) => this[index];

        #region Scatter Read Implementation

        /// <summary>
        /// Execute this scatter read round.
        /// </summary>
        /// <param name="vmm"></param>
        /// <param name="pid"></param>
        internal void Execute(Vmm vmm, uint pid)
        {
            ReadScatter(vmm, pid, _indexes.Values.SelectMany(x => x.Entries.Values).ToArray(), _useCache);
            foreach (var index in _indexes)
            {
                index.Value.OnCompleted();
            }
        }

        [ThreadStatic]
        private static HashSet<ulong> _pagesTls;

        private static void ReadScatter(Vmm vmm, uint pid, ReadOnlySpan<IScatterEntry> entries, bool useCache = true)
        {
            if (entries.Length == 0)
            {
                return;
            }

            _pagesTls ??= new HashSet<ulong>(512);
            _pagesTls.Clear();

            // Setup pages to read
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];

                if (!Utilities.IsValidVirtualAddress(entry.Address) || entry.CB <= 0 || (uint)entry.CB > ScatterReadMap.MaxReadSize)
                {
                    entry.IsFailed = true;
                    continue;
                }

                uint numPages = Utilities.ADDRESS_AND_SIZE_TO_SPAN_PAGES(entry.Address, (uint)entry.CB);
                ulong basePage = Utilities.PAGE_ALIGN(entry.Address);

                for (uint p = 0; p < numPages; p++)
                {
                    _pagesTls.Add(basePage + 0x1000ul * p);
                }
            }

            if (_pagesTls.Count == 0)
            {
                return;
            }

            var flags = useCache ? VmmFlags.NONE : VmmFlags.NOCACHE;
            // Read pages
            using var hScatter = vmm.MemReadScatter(pid, flags, _pagesTls.ToArray());
            // Set results
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.IsFailed)
                {
                    continue;
                }

                entry.SetResult(hScatter);
            }
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
            _useCache = default;
            foreach (var index in _indexes.Values)
            {
                index.Return();
            }
            _indexes.Clear();
            return true;
        }

        #endregion
    }
}
