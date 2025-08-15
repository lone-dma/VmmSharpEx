using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using VmmSharpEx.Internal;

namespace VmmSharpEx;

/// <summary>
/// VmmSearch represents a binary search in memory.
/// </summary>
public unsafe sealed class VmmSearch : IDisposable
{
    private readonly ConcurrentBag<Vmmi.VMMDLL_MEM_SEARCH_CONTEXT_SEARCHENTRY> _searches = new();
    private readonly SearchResultsContainer _managed = new();
    private readonly Thread _thread;
    private readonly Vmm _vmm;
    private readonly uint _pid;
    private readonly Vmmi.SearchResultCallback _searchResultCallback; // Root the delegate to prevent it from being garbage collected.
    private Vmmi.VMMDLL_MEM_SEARCH_CONTEXT* _native;
    private bool _started;
    private bool _disposed;

    /// <summary>
    /// Get the result of the search: Blocking / wait until finish.
    /// </summary>
    /// <returns></returns>
    public SearchResultsContainer Result
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, "Object disposed.");
            if (!_started)
                Start();
            _thread.Join();
            return Poll();
        }
    }

    private VmmSearch() { }

    internal VmmSearch(Vmm vmm, uint pid, ulong addr_min = 0, ulong addr_max = ulong.MaxValue, uint cMaxResult = 0, uint readFlags = 0)
    {
        if (cMaxResult == 0) 
            cMaxResult = 0x10000;
        _thread = new Thread(Worker)
        {
            IsBackground = true
        };
        _vmm = vmm;
        _pid = pid;
        _managed.AddrMin = addr_min;
        _managed.AddrMax = addr_max;
        _native = (Vmmi.VMMDLL_MEM_SEARCH_CONTEXT*)NativeMemory.Alloc((nuint)sizeof(Vmmi.VMMDLL_MEM_SEARCH_CONTEXT) + 8);
        _searchResultCallback = SearchResultCallback;
        *_native = new Vmmi.VMMDLL_MEM_SEARCH_CONTEXT
        {
            dwVersion = Vmmi.VMMDLL_MEM_SEARCH_VERSION,
            vaMin = addr_min,
            vaMax = addr_max,
            cMaxResult = cMaxResult,
            ReadFlags = readFlags,
            pfnResultOptCB = Marshal.GetFunctionPointerForDelegate(_searchResultCallback)
        };
    }

    /// <summary>
    /// ToString override.
    /// </summary>
    public override string ToString()
    {
        return "VmmSearch";
    }

    ~VmmSearch() => Dispose(false);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, true) == false)
        {
            if (!_thread.IsAlive)
            {
                NativeMemory.Free(_native);
            }
            else // Abort the search if it is still running
            {
                _native->fAbortRequested = 1;
                if (_thread.Join(TimeSpan.FromSeconds(1))) // Added timeout to prevent deadlock, but should never happen.
                    NativeMemory.Free(_native);
            }
            _native = null;
        }
    }

    /// <summary>
    /// Add an entry to the search. Must be done before search is started, or no effect will take place.
    /// </summary>
    /// <param name="search">Search filter (max 32 bytes). Excess will be truncated.</param>
    /// <param name="skipmask">Skip mask (max 32 bytes). Excess will be truncated.</param>
    /// <param name="align">Alignment</param>
    /// <returns>TRUE if added OK otherwise FALSE.</returns>
    public void AddEntry(byte[] search, byte[] skipmask = null, uint align = 1)
    {
        const int maxLength = 32;
        ObjectDisposedException.ThrowIf(_disposed, "Object disposed.");
        var e = new Vmmi.VMMDLL_MEM_SEARCH_CONTEXT_SEARCHENTRY
        {
            cbAlign = align,
            cb = (uint)search.Length
        };
        var pbSearch = new Span<byte>(e.pb, maxLength);
        search.AsSpan(0, Math.Min(search.Length, maxLength)).CopyTo(pbSearch);
        if (skipmask is not null && skipmask.Length > 0)
        {
            var pbSkipMask = new Span<byte>(e.pbSkipMask, maxLength);
            skipmask.AsSpan(0, Math.Min(skipmask.Length, maxLength)).CopyTo(pbSkipMask);
        }

        _searches.Add(e);
    }

    /// <summary>
    /// Start the search. Non-blocking.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, "Object disposed.");
        if (Interlocked.Exchange(ref _started, true) == true) // Ensure start is only called once.
            return;
        if (_searches.IsEmpty)
        {
            _managed.IsCompleted = true;
            _managed.IsCompletedSuccess = false;
            return;
        }
        _thread.Start();
    }

    /// <summary>
    /// Abort the search. Blocking / wait until abort is complete.
    /// </summary>
    public void Abort() => Dispose();

    /// <summary>
    /// Poll the search for results. Non-blocking.
    /// </summary>
    /// <returns></returns>
    public SearchResultsContainer Poll()
    {
        ObjectDisposedException.ThrowIf(_disposed, "Object disposed.");
        if (!_started)
            Start();
        _managed.AddrCurrent = _native->vaCurrent;
        _managed.AddrMin = _native->vaMin;
        _managed.AddrMax = _native->vaMax;
        _managed.TotalReadBytes = _native->cbReadTotal;
        return _managed;
    }

    private void Worker()
    {
        if (_disposed)
            return;
        var searches = _searches.ToArray();
        var hSearches = GCHandle.Alloc(searches, GCHandleType.Pinned);
        try
        {
            _native->cSearch = (uint)searches.Length;
            _native->search = hSearches.AddrOfPinnedObject();
            bool fResult = Vmmi.VMMDLL_MemSearch(_vmm, _pid, _native, IntPtr.Zero, IntPtr.Zero);
            _managed.IsCompletedSuccess = fResult && _native->fAbortRequested == 0;
            _managed.IsCompleted = true;
        }
        finally
        {
            hSearches.Free();
        }
    }

    private bool SearchResultCallback(Vmmi.VMMDLL_MEM_SEARCH_CONTEXT ctx, ulong va, uint iSearch)
    {
        var e = new SearchResultEntry
        {
            Address = va,
            SearchTermId = iSearch
        };
        _managed.Results.Add(e);
        return true;
    }

    /// <summary>
    /// Class with info about the search results. Find the actual results in the result field.
    /// </summary>
    public sealed class SearchResultsContainer
    {
        /// <summary>
        /// Indicates that the search has been completed.
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// If isCompletedSuccess is true this indicates if the search was completed successfully.
        /// </summary>
        public bool IsCompletedSuccess { get; set; }

        /// <summary>
        /// Address to start searching from - default 0.
        /// </summary>
        public ulong AddrMin { get; set; }

        /// <summary>
        /// Address to stop searching at - default MAXUINT64.
        /// </summary>
        public ulong AddrMax { get; set; }

        /// <summary>
        /// Current address being searched in search thread.
        /// </summary>
        public ulong AddrCurrent { get; set; }

        /// <summary>
        /// Number of bytes that have been procssed in search.
        /// </summary>
        public ulong TotalReadBytes { get; set; }

        /// <summary>
        /// The actual results.
        /// </summary>
        public ConcurrentBag<SearchResultEntry> Results { get; } = new();
    }

    /// <summary>
    /// Struct with info about a single search result. Address, search term id.
    /// </summary>
    public readonly struct SearchResultEntry
    {
        public readonly ulong Address { get; init; }
        public readonly ulong SearchTermId { get; init; }
    }
}