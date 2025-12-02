/*  
*  C# API wrapper 'vmmsharp' for MemProcFS 'vmm.dll' and LeechCore 'leechcore.dll' APIs.
*  
*  Please see the example project in vmmsharp_example for additional information.
*  
*  Please consult the C/C++ header files vmmdll.h and leechcore.h for information about parameters and API usage.
*  
*  (c) Ulf Frisk, 2020-2025
*  Author: Ulf Frisk, pcileech@frizk.net
*  
*/

/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VmmSharpEx.Internal;

namespace VmmSharpEx;

/// <summary>
/// VmmSearch represents a binary search in memory.
/// </summary>
public sealed class VmmSearch : IDisposable
{
    private readonly SearchContext _managed = new();
    private readonly uint _pid;
    private readonly List<Vmmi.VMMDLL_MEM_SEARCH_CONTEXT_SEARCHENTRY> _searches = new();
    private readonly Vmmi.SearchResultCallback _searchResultCallback; // Root the delegate to prevent it from being garbage collected.
    private readonly Vmm _vmm;
    private Task<SearchContext> _worker;
    private bool _disposed;
    private unsafe Vmmi.VMMDLL_MEM_SEARCH_CONTEXT* _native;

    private VmmSearch() { }

    /// <summary>
    /// Get the result of the search: Blocking / wait until finish.
    /// </summary>
    /// <remarks>
    /// Automatically calls <see cref="Start"/> if the search has not already been started.
    /// </remarks>
    /// <returns></returns>
    public SearchContext Result => GetResult();

    /// <summary>
    /// Event fired when the search is completed.
    /// </summary>
    /// <remarks>
    /// Will be fired on a thread pool thread immediately before the <see cref="GetResult"/> and <see cref="GetResultAsync"/> methods return.
    /// If you never call <see cref="Start"/>, this event will never be fired.
    /// </remarks>
    public event EventHandler<VmmSearch> Completed;
    private void OnCompleted()
    {
        _managed.IsCompleted = true;
        Completed?.Invoke(this, this);
    }

    internal VmmSearch(Vmm vmm, uint pid, ulong addr_min = 0, ulong addr_max = ulong.MaxValue, uint cMaxResult = 0, uint readFlags = 0)
    {
        unsafe
        {
            if (cMaxResult == 0)
            {
                cMaxResult = 0x10000;
            }

            _vmm = vmm;
            _pid = pid;
            _managed.AddrMin = addr_min;
            _managed.AddrMax = addr_max;
            _native = (Vmmi.VMMDLL_MEM_SEARCH_CONTEXT*)NativeMemory.Alloc((nuint)Unsafe.SizeOf<Vmmi.VMMDLL_MEM_SEARCH_CONTEXT>() + 8);
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
    }

    /// <summary>
    /// Poll the search for results. Non-blocking.
    /// </summary>
    /// <remarks>
    /// Automatically calls <see cref="Start"/> if the search has not already been started.
    /// </remarks>
    /// <returns><see cref="SearchContext"/> with the current status.</returns>
    public unsafe SearchContext Poll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_worker is null)
            Start();

        _managed.AddrCurrent = _native->vaCurrent;
        _managed.AddrMin = _native->vaMin;
        _managed.AddrMax = _native->vaMax;
        _managed.TotalReadBytes = _native->cbReadTotal;
        return _managed;
    }

    /// <summary>
    /// Synchronous get result. Blocking / wait until finish.
    /// </summary>
    /// <remarks>
    /// Automatically calls <see cref="Start"/> if the search has not already been started.
    /// </remarks>
    /// <returns><see cref="SearchContext"/> with the search results.</returns>
    public SearchContext GetResult()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_worker is null)
            Start();
        return _worker!.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronous get result. Async / wait until finish.
    /// </summary>
    /// <remarks>
    /// Automatically calls <see cref="Start"/> if the search has not already been started.
    /// </remarks>
    /// <returns><see cref="SearchContext"/> with the search results.</returns>
    public async Task<SearchContext> GetResultAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_worker is null)
            Start();
        return await _worker!;
    }

    /// <summary>
    /// ToString override.
    /// </summary>
    public override string ToString()
    {
        if (_disposed)
        {
            return "VmmSearch:Disposed";
        }
        return (_worker?.Status) switch
        {
            null => "VmmSearch:NotStarted",
            TaskStatus.RanToCompletion => "VmmSearch:Completed",
            TaskStatus.Canceled => "VmmSearch:Aborted",
            TaskStatus.Created => "VmmSearch:NotStarted",
            TaskStatus.WaitingForActivation => "VmmSearch:NotStarted",
            TaskStatus.WaitingToRun => "VmmSearch:NotStarted",
            TaskStatus.Running => "VmmSearch:Running",
            TaskStatus.WaitingForChildrenToComplete => "VmmSearch:Running",
            TaskStatus.Faulted => "VmmSearch:Exception",
            _ => "VmmSearch:Unknown",
        };
    }

    /// <summary>
    /// Add an entry to the search. Must be done before search is started, or no effect will take place.
    /// </summary>
    /// <param name="search">Search filter (max 32 bytes). Excess will be truncated.</param>
    /// <param name="skipmask">Skip mask (max 32 bytes). Excess will be truncated.</param>
    /// <param name="align">Alignment</param>
    /// <returns>TRUE if added OK otherwise FALSE.</returns>
    public unsafe void AddEntry(byte[] search, byte[] skipmask = null, uint align = 1)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        const int maxLength = 32;
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        _worker ??= Task.Run(Worker);
    }

    private unsafe SearchContext Worker()
    {
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_searches.Count == 0)
            {
                _managed.IsCompletedSuccess = false;
                return _managed;
            }

            var searches = _searches.ToArray();
            var hSearches = GCHandle.Alloc(searches, GCHandleType.Pinned);
            try
            {
                _native->cSearch = (uint)searches.Length;
                _native->search = hSearches.AddrOfPinnedObject();
                bool fResult = Vmmi.VMMDLL_MemSearch(_vmm, _pid, _native, IntPtr.Zero, IntPtr.Zero);
                ObjectDisposedException.ThrowIf(_disposed, this);
                _managed.IsCompletedSuccess = fResult && _native->fAbortRequested == 0;
                return _managed;
            }
            finally
            {
                hSearches.Free();
            }
        }
        finally
        {
            OnCompleted();
        }
    }

    private bool SearchResultCallback(Vmmi.VMMDLL_MEM_SEARCH_CONTEXT ctx, ulong va, uint iSearch)
    {
        var e = new SearchResult
        {
            Address = va,
            SearchTermId = iSearch
        };
        _managed._results.Add(e);
        return _managed._results.Count < ctx.cMaxResult;
    }

    /// <summary>
    /// Aborts the search if it is still running and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private unsafe void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, true) == false)
        {
            if (disposing)
            {
                Completed = null;
            }
            if (_worker is null || _worker.IsCompleted)
            {
                NativeMemory.Free(_native);
                _native = null;
            }
            else // Still running
            {
                _ = Task.Run(() => // Ensure Cleanup in the background
                {
                    try
                    {
                        _native->fAbortRequested = 1;
                        _worker.Wait();
                    }
                    finally
                    {
                        NativeMemory.Free(_native);
                        _native = null;
                    }
                });
            }
        }
    }

    ~VmmSearch() => Dispose(disposing: false);

    /// <summary>
    /// Struct with info about the current search results.
    /// </summary>
    public sealed class SearchContext
    {
        internal SearchContext() { }

        /// <summary>
        /// If <see cref="IsCompleted"/> is <see langword="true"/> this indicates if the search was completed.
        /// </summary>
        public bool IsCompleted { get; internal set; }

        /// <summary>
        /// If <see cref="IsCompletedSuccess"/> is <see langword="true"/> this indicates if the search had at least one search item and completed without any errors.
        /// </summary>
        public bool IsCompletedSuccess { get; internal set; }

        /// <summary>
        /// Address to start searching from - default 0.
        /// </summary>
        public ulong AddrMin { get; internal set; }

        /// <summary>
        /// Address to stop searching at - default MAXUINT64.
        /// </summary>
        public ulong AddrMax { get; internal set; }

        /// <summary>
        /// Current address being searched in search thread.
        /// </summary>
        public ulong AddrCurrent { get; internal set; }

        /// <summary>
        /// Number of bytes that have been procssed in search.
        /// </summary>
        public ulong TotalReadBytes { get; internal set; }

        internal readonly ConcurrentBag<SearchResult> _results = new();
        /// <summary>
        /// The search results.
        /// </summary>
        public IReadOnlyCollection<SearchResult> Results => _results;
    }

    /// <summary>
    /// Struct with info about a single search result. Address, search term id.
    /// </summary>
    public readonly struct SearchResult
    {
        public readonly ulong Address { get; init; }
        public readonly ulong SearchTermId { get; init; }
    }
}