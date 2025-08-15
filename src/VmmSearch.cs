using System.Runtime.InteropServices;
using VmmSharpEx.Internal;

namespace VmmSharpEx;

/// <summary>
///     VmmSearch represents a binary search in memory.
/// </summary>
public sealed class VmmSearch : IDisposable
{
    #region Base Functionality

    private readonly Vmm _vmm;
    private readonly uint _pid;
    private readonly List<Vmmi.VMMDLL_MEM_SEARCH_CONTEXT_SEARCHENTRY> _terms;

    private SearchResult _result;
    private Vmmi.VMMDLL_MEM_SEARCH_CONTEXT _native;
    private Thread _thread;

    private IntPtr _ptrNative;

    public bool Disposed => _ptrNative == IntPtr.Zero;

    private VmmSearch()
    {
        ;
    }

    public VmmSearch(Vmm vmm, uint pid, ulong addr_min = 0, ulong addr_max = ulong.MaxValue, uint cMaxResult = 0, uint readFlags = 0)
    {
        if (cMaxResult == 0) cMaxResult = 0x10000;
        _vmm = vmm;
        _pid = pid;
        _result = new SearchResult
        {
            addrMin = addr_min,
            addrMax = addr_max,
            result = new List<SearchResultEntry>()
        };
        _terms = new List<Vmmi.VMMDLL_MEM_SEARCH_CONTEXT_SEARCHENTRY>();
        _native = new Vmmi.VMMDLL_MEM_SEARCH_CONTEXT
        {
            dwVersion = Vmmi.VMMDLL_MEM_SEARCH_VERSION,
            vaMin = addr_min,
            vaMax = addr_max,
            cMaxResult = cMaxResult,
            ReadFlags = readFlags,
            pfnResultOptCB = SearchResultCallback
        };
        _ptrNative = Marshal.AllocHGlobal(Marshal.SizeOf(_native));
        Marshal.StructureToPtr(_native, _ptrNative, false);
    }

    /// <summary>
    ///     ToString override.
    /// </summary>
    public override string ToString()
    {
        return "VmmSearch";
    }

    ~VmmSearch()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _ptrNative, IntPtr.Zero) is IntPtr h && h != IntPtr.Zero) Marshal.FreeHGlobal(h);
    }

    #endregion // Base Functionality

    #region Specific Functionality

    /// <summary>
    ///     Struct with info about the search results. Find the actual results in the result field.
    /// </summary>
    public struct SearchResult
    {
        /// Indicates that the search has been started. i.e. start() or result() have been called.
        public bool isStarted;

        /// Indicates that the search has been completed.
        public bool isCompleted;

        /// If isCompletedSuccess is true this indicates if the search was completed successfully.
        public bool isCompletedSuccess;

        /// Address to start searching from - default 0.
        public ulong addrMin;

        /// Address to stop searching at - default MAXUINT64.
        public ulong addrMax;

        /// Current address being searched in search thread.
        public ulong addrCurrent;

        /// Number of bytes that have been procssed in search.
        public ulong totalReadBytes;

        /// The actual results.
        public List<SearchResultEntry> result;
    }

    /// <summary>
    ///     Struct with info about a single search result. Address, search term id.
    /// </summary>
    public struct SearchResultEntry
    {
        public ulong address;
        public ulong search_term_id;
    }

    /// <summary>
    ///     Add a search term to the search. Should be done before search is started.
    /// </summary>
    /// <param name="search"></param>
    /// <param name="skipmask"></param>
    /// <param name="align"></param>
    /// <returns></returns>
    public unsafe uint AddSearch(byte[] search, byte[] skipmask = null, uint align = 1)
    {
        ObjectDisposedException.ThrowIf(Disposed, "Object disposed.");
        if (_result.isStarted) return uint.MaxValue;
        if (search.Length == 0 || search.Length > 32) return uint.MaxValue;
        if (skipmask != null && skipmask.Length != search.Length) return uint.MaxValue;
        var e = new Vmmi.VMMDLL_MEM_SEARCH_CONTEXT_SEARCHENTRY
        {
            cbAlign = align,
            cb = (uint)search.Length
        };
        fixed (byte* pbSearch = search)
        {
            Buffer.MemoryCopy(pbSearch, e.pb, 32, search.Length);
        }

        if (skipmask != null)
            fixed (byte* pbSkipMask = skipmask)
            {
                Buffer.MemoryCopy(pbSkipMask, e.pbSkipMask, 32, skipmask.Length);
            }

        _terms.Add(e);
        return (uint)_terms.Count - 1;
    }

    private void Start_DoWork()
    {
        var arrTerms = _terms.ToArray();
        var hndTerms = GCHandle.Alloc(arrTerms, GCHandleType.Pinned);
        _native.cSearch = (uint)_terms.Count;
        _native.search = hndTerms.AddrOfPinnedObject();
        Marshal.StructureToPtr(_native, _ptrNative, false);
        var fResult = Vmmi.VMMDLL_MemSearch2(_vmm, _pid, _ptrNative, IntPtr.Zero, IntPtr.Zero);
        hndTerms.Free();
        _result.isCompletedSuccess = fResult && !_native.fAbortRequested;
        _result.isCompleted = true;
    }

    /// <summary>
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(Disposed, "Object disposed.");
        if (_result.isStarted) return;
        if (_terms.Count == 0) return;
        _result.isStarted = true;
        _thread = new Thread(() => Start_DoWork());
        _thread.Start();
    }

    /// <summary>
    ///     Abort the search. Blocking / wait until abort is complete.
    /// </summary>
    public void Abort()
    {
        ObjectDisposedException.ThrowIf(Disposed, "Object disposed.");
        if (!_result.isStarted) return;
        _native.fAbortRequested = true;
        _thread.Join();
    }

    /// <summary>
    ///     Poll the search for results. Non-blocking.
    /// </summary>
    /// <returns></returns>
    public SearchResult Poll()
    {
        ObjectDisposedException.ThrowIf(Disposed, "Object disposed.");
        if (!_result.isStarted) Start();
        _result.addrCurrent = (ulong)Marshal.ReadInt64(_ptrNative, Marshal.OffsetOf<Vmmi.VMMDLL_MEM_SEARCH_CONTEXT>("vaCurrent").ToInt32());
        _result.addrMin = (ulong)Marshal.ReadInt64(_ptrNative, Marshal.OffsetOf<Vmmi.VMMDLL_MEM_SEARCH_CONTEXT>("vaMin").ToInt32());
        _result.addrMax = (ulong)Marshal.ReadInt64(_ptrNative, Marshal.OffsetOf<Vmmi.VMMDLL_MEM_SEARCH_CONTEXT>("vaMax").ToInt32());
        _result.totalReadBytes = (ulong)Marshal.ReadInt64(_ptrNative, Marshal.OffsetOf<Vmmi.VMMDLL_MEM_SEARCH_CONTEXT>("cbReadTotal").ToInt32());
        return _result;
    }

    /// <summary>
    ///     Get the result of the search: Blocking / wait until finish.
    /// </summary>
    /// <returns></returns>
    public SearchResult Result()
    {
        ObjectDisposedException.ThrowIf(Disposed, "Object disposed.");
        if (!_result.isStarted) Start();
        if (_result.isStarted) _thread.Join();
        return Poll();
    }

    private bool SearchResultCallback(Vmmi.VMMDLL_MEM_SEARCH_CONTEXT ctx, ulong va, uint iSearch)
    {
        var e = new SearchResultEntry
        {
            address = va,
            search_term_id = iSearch
        };
        _result.result.Add(e);
        return true;
    }

    #endregion // Specific Functionality
}