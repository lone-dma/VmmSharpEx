using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using VmmSharpEx.Internal;

namespace VmmSharpEx;

/// <summary>
/// VmmSearch represents a binary search in memory.
/// </summary>
public unsafe sealed class VmmSearch : IDisposable // ToDo: Refactor further
{
    #region Base Functionality

    private readonly ConcurrentBag<Vmmi.VMMDLL_MEM_SEARCH_CONTEXT_SEARCHENTRY> _searches = new();
    private readonly Thread _thread;
    private readonly Vmm _vmm;
    private readonly uint _pid;
    private readonly Vmmi.SearchResultCallback _searchResultCallback; // Root the delegate to prevent it from being garbage collected.
    private readonly Vmmi.VMMDLL_MEM_SEARCH_CONTEXT* _native;

    private SearchResultsContainer _managed;
    private bool _disposed;

    public bool Disposed => _disposed;

    private VmmSearch()
    {
        ;
    }

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
        _managed = new SearchResultsContainer
        {
            AddrMin = addr_min,
            AddrMax = addr_max,
            Results = new()
        };
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
            _native->fAbortRequested = 1; // Abort the search if it is still running.
            while (_thread.IsAlive)
                Thread.SpinWait(10); // Wait for the thread to finish.
            NativeMemory.Free(_native);
        }
    }

    #endregion // Base Functionality

    #region Specific Functionality

    /// <summary>
    /// Struct with info about the search results. Find the actual results in the result field.
    /// </summary>
    public struct SearchResultsContainer
    {
        /// Indicates that the search has been started. i.e. start() or result() have been called.
        public bool IsStarted;

        /// Indicates that the search has been completed.
        public bool IsCompleted;

        /// If isCompletedSuccess is true this indicates if the search was completed successfully.
        public bool IsCompletedSuccess;

        /// Address to start searching from - default 0.
        public ulong AddrMin;

        /// Address to stop searching at - default MAXUINT64.
        public ulong AddrMax;

        /// Current address being searched in search thread.
        public ulong AddrCurrent;

        /// Number of bytes that have been procssed in search.
        public ulong TotalReadBytes;

        /// The actual results.
        public ConcurrentBag<SearchResultEntry> Results;
    }

    /// <summary>
    /// Struct with info about a single search result. Address, search term id.
    /// </summary>
    public readonly struct SearchResultEntry
    {
        public readonly ulong Address { get; init; }
        public readonly ulong SearchTermId { get; init; }
    }

    /// <summary>
    /// Add a search term to the search. Should be done before search is started.
    /// </summary>
    /// <param name="search"></param>
    /// <param name="skipmask"></param>
    /// <param name="align"></param>
    /// <returns></returns>
    public unsafe uint AddSearch(byte[] search, byte[] skipmask = null, uint align = 1)
    {
        ObjectDisposedException.ThrowIf(Disposed, "Object disposed.");
        if (_managed.IsStarted) 
            return uint.MaxValue;
        if (search.Length == 0 || search.Length > 32) 
            return uint.MaxValue;
        if (skipmask != null && skipmask.Length != search.Length) 
            return uint.MaxValue;
        var e = new Vmmi.VMMDLL_MEM_SEARCH_CONTEXT_SEARCHENTRY
        {
            cbAlign = align,
            cb = (uint)search.Length
        };
        var pbSearch = new Span<byte>(e.pb, 32);
        search.CopyTo(pbSearch);
        if (skipmask is not null)
        {
            var pbSkipMask = new Span<byte>(e.pbSkipMask, 32);
            skipmask.CopyTo(pbSkipMask);
        }

        _searches.Add(e);
        return (uint)_searches.Count - 1;
    }

    private void Worker()
    {
        var searches = _searches.ToArray();
        var hSearches = GCHandle.Alloc(searches, GCHandleType.Pinned);
        _native->cSearch = (uint)searches.Length;
        _native->search = hSearches.AddrOfPinnedObject();
        bool fResult = Vmmi.VMMDLL_MemSearch(_vmm, _pid, _native, IntPtr.Zero, IntPtr.Zero);
        hSearches.Free();
        _managed.IsCompletedSuccess = fResult && _native->fAbortRequested == 0;
        _managed.IsCompleted = true;
    }

    /// <summary>
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(Disposed, "Object disposed.");
        if (_managed.IsStarted) 
            return;
        if (_searches.IsEmpty) 
            return;
        _managed.IsStarted = true;
        _thread.Start();
    }

    /// <summary>
    /// Abort the search. Blocking / wait until abort is complete.
    /// </summary>
    public void Abort()
    {
        ObjectDisposedException.ThrowIf(Disposed, "Object disposed.");
        if (!_managed.IsStarted) 
            return;
        _native->fAbortRequested = 1;
        _thread.Join();
    }

    /// <summary>
    /// Poll the search for results. Non-blocking.
    /// </summary>
    /// <returns></returns>
    public SearchResultsContainer Poll()
    {
        ObjectDisposedException.ThrowIf(Disposed, "Object disposed.");
        if (!_managed.IsStarted) 
            Start();
        _managed.AddrCurrent = _native->vaCurrent;
        _managed.AddrMin = _native->vaMin;
        _managed.AddrMax = _native->vaMax;
        _managed.TotalReadBytes = _native->cbReadTotal;
        return _managed;
    }

    /// <summary>
    /// Get the result of the search: Blocking / wait until finish.
    /// </summary>
    /// <returns></returns>
    public SearchResultsContainer Result()
    {
        ObjectDisposedException.ThrowIf(Disposed, "Object disposed.");
        if (!_managed.IsStarted) 
            Start();
        _thread.Join();
        return Poll();
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

    #endregion // Specific Functionality
}