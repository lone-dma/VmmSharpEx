/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using Collections.Pooled;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using VmmSharpEx.Extensions;
using VmmSharpEx.Internal;
using VmmSharpEx.Options;

namespace VmmSharpEx.Scatter;

/// <summary>
/// The <see cref="VmmScatterSlim"/> class is used to ease the reading of memory in bulk using this managed VmmSharpEx implementation by Lone.
/// This implementation is mostly managed, except for a native call to perform the mem read operation (using <see cref="Vmmi.VMMDLL_MemReadScatter(nint, uint, nint, uint, VmmFlags)"/>).
/// Implementation follows <see href="https://github.com/ufrisk/MemProcFS/blob/master/vmm/vmmdll_scatter.c"/> as closely as possible.
/// </summary>
/// <remarks>
/// Known issue: VMMDLL_MemReadScatter can cause audio crackling/static on some target systems while performing mem reads. See: <see href="https://github.com/ufrisk/MemProcFS/issues/410"/>
/// </remarks>
public sealed class VmmScatterSlim : IScatter, IScatter<VmmScatterSlim>, IDisposable
{
    #region Fields / Ctors

    private const int SCATTER_MAX_SIZE_SINGLE = 0x40000000;
    private const ulong SCATTER_MAX_SIZE_TOTAL = 0x40000000000;
    private readonly Lock _sync = new();
    private readonly PooledDictionary<ulong, LeechCore.MEM_SCATTER> _mems = new();
    private readonly Vmm _vmm;
    private readonly uint _pid;
    private readonly VmmFlags _flags;
    private readonly bool _isKernel;
    private readonly bool _isUser;
    private IntPtr _scatter;
    private bool _disposed;

    /// <summary>
    /// Event is fired upon completion of <see cref="Execute"/>. Exceptions are handled/ignored.
    /// </summary>
    public event EventHandler<VmmScatterSlim>? Completed;
    private void OnCompleted()
    {
        foreach (var callback in Completed?.GetInvocationList() ?? Enumerable.Empty<Delegate>())
        {
            try
            {
                ((EventHandler<VmmScatterSlim>)callback).Invoke(this, this);
            }
            catch { }
        }
    }

    private VmmScatterSlim() { throw new NotImplementedException(); }

    public VmmScatterSlim(Vmm vmm, uint pid, VmmFlags flags = VmmFlags.NONE)
    {
        _vmm = vmm;
        _pid = pid;
        _flags = flags;
        bool isPhysical = pid == Vmm.PID_PHYSICALMEMORY;
        _isKernel = !isPhysical && (pid & Vmm.PID_PROCESS_WITH_KERNELMEMORY) != 0;
        _isUser = !isPhysical && !_isKernel;
    }

    static VmmScatterSlim IScatter<VmmScatterSlim>.Create(Vmm vmm, uint pid, VmmFlags flags)
    {
        return new VmmScatterSlim(vmm, pid, flags);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Prepare to read memory of a certain size.
    /// </summary>
    /// <remarks>
    /// Can be used with any Read* method as long as the size matches.
    /// For example this would be used with <see cref="ReadString(ulong, int, Encoding)"/>, after calling <see cref="Execute"/>.
    /// </remarks>
    /// <param name="address">Address of the memory to be read.</param>
    /// <param name="cb">Count of bytes to be read.</param>
    /// <returns><see langword="true"/> if successful, otherwise <see langword="false"/>.</returns>
    public bool PrepareRead(ulong address, int cb)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if ((_isKernel && !address.IsValidKernelVA()) ||
            (_isUser && !address.IsValidUserVA()) ||
            cb <= 0 ||
            cb >= SCATTER_MAX_SIZE_SINGLE ||
            ((ulong)_mems.Count << 12) + (uint)cb > SCATTER_MAX_SIZE_TOTAL ||
            unchecked(address + (ulong)cb) < address)
        {
            return false;
        }
        ulong pageCount = VmmUtilities.ADDRESS_AND_SIZE_TO_SPAN_PAGES(address, (uint)cb);
        ulong pageBase = VmmUtilities.PAGE_ALIGN(address);
        bool fForcePageRead = (_flags & VmmFlags.SCATTER_FORCE_PAGEREAD) != 0;
        lock (_sync)
        {
            for (ulong p = 0; p < pageCount; p++)
            {
                ulong pageAddr = checked(pageBase + (p << 12));
                ulong vaMem = pageAddr;
                uint cbMem = 0x1000;

                if ((pageCount == 1) && (cb <= 0x400) && !fForcePageRead)
                {
                    // single-page small read -> optimize MEM for small read.
                    // NB! buffer allocation still remains 0x1000 even if not all is used for now.
                    cbMem = ((uint)cb + 7u + (uint)(address & 7u)) & ~0x7u;
                    vaMem = address & ~0x7ul;
                    if ((vaMem & 0xffful) + cbMem > 0x1000u)
                    {
                        vaMem = (vaMem & ~0xffful) + 0x1000u - cbMem;
                    }
                }
                _mems.AddOrUpdate(
                    pageAddr,
                    // add: no existing entry
                    _ => new LeechCore.MEM_SCATTER()
                    {
                        qwA = vaMem,
                        cb = cbMem
                    },
                    // update: entry already exists
                    (_, existing) =>
                    {
                        // If an entry already exists for this page, ensure we upgrade to full-page read.
                        // This preserves correctness when multiple overlapping prepares hit the same page.
                        if (existing.cb != 0x1000)
                            return new LeechCore.MEM_SCATTER()
                            {
                                qwA = pageAddr,
                                cb = 0x1000u
                            };

                        return existing;
                    }
                );
            }
        }
        return true;
    }

    /// <summary>
    /// Prepare to read memory from an array of a certain struct.
    /// </summary>
    /// <remarks>
    /// Corresponds with the <see cref="ReadArray{T}(ulong, int)"/>, <see cref="ReadPooled{T}(ulong, int)"/>, or <see cref="ReadSpan{T}(ulong, Span{T})"/> methods, that should be called after <see cref="Execute"/>.
    /// </remarks>
    /// <typeparam name="T">The <see langword="unmanaged"/> struct type for this operation.</typeparam>
    /// <param name="address">Address of the array to be read.</param>
    /// <param name="count">Number of array elements to be read.</param>
    /// <returns><see langword="true"/> if successful, otherwise <see langword="false"/>.</returns>
    public unsafe bool PrepareReadArray<T>(ulong address, int count)
        where T : unmanaged
    {
        if (count <= 0)
            return false;
        int cb = checked(sizeof(T) * count);
        return PrepareRead(address, cb);
    }

    /// <summary>
    /// Prepare to read memory of a certain struct.
    /// </summary>
    /// <remarks>
    /// Corresponds with the <see cref="ReadValue{T}(ulong, out T)"/> method, that should be called after <see cref="Execute"/>.
    /// </remarks>
    /// <typeparam name="T">The <see langword="unmanaged"/> struct type for this operation.</typeparam>
    /// <param name="address">Address of the memory to be read.</param>
    /// <returns><see langword="true"/> if successful, otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool PrepareReadValue<T>(ulong address)
        where T : unmanaged, allows ref struct
    {
        return PrepareRead(address, sizeof(T));
    }

    /// <summary>
    /// Prepare to read memory of a Windows x64 pointer type.
    /// </summary>
    /// <remarks>
    /// Corresponds with the <see cref="ReadPtr(ulong, out VmmPointer)"/> method, that should be called after <see cref="Execute"/>.
    /// </remarks>
    /// <param name="address">Address of the memory to be read.</param>
    /// <returns><see langword="true"/> if successful, otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool PrepareReadPtr(ulong address)
    {
        return PrepareRead(address, sizeof(VmmPointer));
    }

    /// <summary>
    /// Execute any prepared read operations.
    /// Can be called multiple times to re-execute the same prepared operations.
    /// </summary>
    /// <remarks>
    /// If there are no prepared operations, this method is a no-op.
    /// </remarks>
    /// <exception cref="VmmException"></exception>
    public void Execute()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_mems.Count == 0)
                return; // no-op
            FreeScatter();
            _scatter = MemReadScatterInternal();
        }
        OnCompleted();
    }

    /// <summary>
    /// Read memory from an address into a byte array.
    /// </summary>
    /// <remarks>
    /// This should be called after <see cref="Execute"/>.
    /// NOTE: This method incurs a heap allocation for the returned byte array. For high-performance use other read methods instead.
    /// </remarks>
    /// <param name="address">Address to read from.</param>
    /// <param name="cb">Count of bytes to be read.</param>
    /// <returns>A byte array with the read memory, otherwise <see langword="null"/>.</returns>
    public byte[]? Read(ulong address, int cb)
    {
        if (cb <= 0)
            return null;
        var array = new byte[cb];
        if (!ReadSpanInternal(address, array))
            return null;
        return array;
    }

    /// <summary>
    /// Read memory from an address to a pointer of a buffer that can accept <paramref name="cb"/> bytes.
    /// </summary>
    /// <param name="address">Address to read from.</param>
    /// <param name="cb">Count of bytes to be read.</param>
    /// <param name="pb">Pointer to buffer to receive read. You must make sure the buffer is pinned/fixed.</param>
    /// <returns><see langword="true"/> if successful, otherwise <see langword="false"/>.</returns>
    public unsafe bool Read(ulong address, int cb, void* pb)
    {
        if (cb <= 0)
            return false;
        var dest = new Span<byte>(pb, cb);
        return ReadSpanInternal(address, dest);
    }

    /// <summary>
    /// Read memory from an address to a pointer of a buffer that can accept <paramref name="cb"/> bytes.
    /// </summary>
    /// <param name="address">Address to read from.</param>
    /// <param name="cb">Count of bytes to be read.</param>
    /// <param name="pb">Pointer to buffer to receive read. You must make sure the buffer is pinned/fixed.</param>
    /// <returns><see langword="true"/> if successful, otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool Read(ulong address, int cb, IntPtr pb)
    {
        return Read(address, cb, pb.ToPointer());
    }

    /// <summary>
    /// Read memory from an address into a struct type.
    /// </summary>
    /// <remarks>
    /// This should be called after <see cref="Execute"/>.
    /// </remarks>
    /// <typeparam name="T">The <see langword="unmanaged"/> struct type for this operation.</typeparam>
    /// <param name="address">Address to read from.</param>
    /// <param name="result">Field in which the span <typeparamref name="T"/> is populated. If the read fails this will be <see langword="default"/>.</param>
    /// <returns><see langword="true"/> if successful, otherwise <see langword="false"/>.</returns>
    public unsafe bool ReadValue<T>(ulong address, out T result)
        where T : unmanaged, allows ref struct
    {
        result = default;
        fixed (void* pResult = &result)
        {
            var dest = new Span<byte>(pResult, sizeof(T));
            return ReadSpanInternal(address, dest);
        }
    }

    /// <summary>
    /// Read memory from an address into a Windows x64 pointer type.
    /// </summary>
    /// <remarks>
    /// This should be called after <see cref="Execute"/>.
    /// </remarks>
    /// <param name="address">Address to read from.</param>
    /// <param name="result">Field in which the span <see cref="VmmPointer"/> is populated. If the read fails this will be <see langword="default"/>.</param>
    /// <returns><see langword="true"/> if successful, otherwise <see langword="false"/>.</returns>
    public bool ReadPtr(ulong address, out VmmPointer result)
    {
        if (!ReadValue(address, out result) ||
            result == 0 ||
            (_isKernel && !result.IsValidKernelVA) ||
            (_isUser && !result.IsValidUserVA))
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// Read memory from an address into an array of a certain type.
    /// </summary>
    /// <remarks>
    /// NOTE: This method incurs a heap allocation for the returned byte array. For high-performance use other read methods instead. This should be called after <see cref="Execute"/>.
    /// </remarks>
    /// <typeparam name="T">The <see langword="unmanaged"/> struct type for this operation.</typeparam>
    /// <param name="address">Address to read from.</param>
    /// <param name="count">The number of array elements to read.</param>
    /// <returns>An array on success; otherwise <see langword="null"/>.</returns>
    public T[]? ReadArray<T>(ulong address, int count)
        where T : unmanaged
    {
        if (count <= 0)
            return null;
        var array = new T[count];
        if (!ReadSpanInternal(address, array))
            return null;
        return array;
    }

    /// <summary>
    /// Read memory from an address into a pooled array of a certain type.
    /// </summary>
    /// <remarks>
    /// This should be called after <see cref="Execute"/>.
    /// </remarks>
    /// <typeparam name="T">The <see langword="unmanaged"/> struct type for this operation.</typeparam>
    /// <param name="address">Address to read from.</param>
    /// <param name="count">The number of array elements to read.</param>
    /// <returns><see cref="IMemoryOwner{T}"/> lease, or <see langword="null"/> if failed. Be sure to call <see cref="IDisposable.Dispose()"/> when done.</returns>
    public IMemoryOwner<T>? ReadPooled<T>(ulong address, int count)
        where T : unmanaged
    {
        if (count <= 0)
            return null;
        var pooled = new PooledMemory<T>(count);
        if (!ReadSpanInternal(address, pooled.Span))
        {
            pooled.Dispose();
            return null;
        }
        return pooled;
    }

    /// <summary>
    /// Read memory from an address into a Span of a certain type.
    /// </summary>
    /// <remarks>
    /// This should be called after <see cref="Execute"/>.
    /// </remarks>
    /// <typeparam name="T">The <see langword="unmanaged"/> struct type for this operation.</typeparam>
    /// <param name="address">Address to read from.</param>
    /// <param name="span">The span to read into.</param>
    /// <returns><see langword="true"/> if successful, otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadSpan<T>(ulong address, Span<T> span)
        where T : unmanaged
    {
        return ReadSpanInternal(address, span);
    }

    /// <summary>
    /// Read memory from an address into a managed string.
    /// </summary>
    /// <remarks>
    /// This should be called after <see cref="Execute"/>.
    /// </remarks>
    /// <param name="address">Address to read from.</param>
    /// <param name="cb">Count of bytes to read. Keep in mind some string encodings are 2-4 bytes per character.</param>
    /// <param name="encoding">String Encoding for this read.</param>
    /// <returns>C# Managed <see cref="System.String"/>. Otherwise, <see langword="null"/> if failed.</returns>
    public string? ReadString(ulong address, int cb, Encoding encoding)
    {
        if (cb <= 0)
            return null;
        ArgumentNullException.ThrowIfNull(encoding, nameof(encoding));
        byte[]? rentedBytes = null;
        char[]? rentedChars = null;
        try
        {
            Span<byte> bytesSource = cb <= 256 ?
                stackalloc byte[cb] : (rentedBytes = ArrayPool<byte>.Shared.Rent(cb));
            var bytes = bytesSource.Slice(0, cb); // Rented Pool can have more than cb
            if (!ReadSpan(address, bytes))
            {
                return null;
            }

            int charCount = encoding.GetCharCount(bytes);
            Span<char> charsSource = charCount <= 128 ?
                stackalloc char[charCount] : (rentedChars = ArrayPool<char>.Shared.Rent(charCount));
            var chars = charsSource.Slice(0, charCount);
            encoding.GetChars(bytes, chars);
            int nt = chars.IndexOf('\0');
            return nt != -1 ?
                chars.Slice(0, nt).ToString() : chars.ToString(); // Only one string allocation
        }
        finally
        {
            if (rentedBytes is not null)
                ArrayPool<byte>.Shared.Return(rentedBytes);
            if (rentedChars is not null)
                ArrayPool<char>.Shared.Return(rentedChars);
        }
    }

    /// <summary>
    /// Resets the prepared read operations, and results buffer.
    /// </summary>
    public void Reset()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _mems.Clear();
            FreeScatter();
        }
    }

    #endregion

    #region Internal

    private unsafe IntPtr MemReadScatterInternal()
    {
        int cMems = _mems.Count;
        if (!Lci.LcAllocScatter1((uint)cMems, out var pppMEMs) || pppMEMs == IntPtr.Zero)
        {
            throw new VmmException("LcAllocScatter1 FAIL");
        }
        try
        {
            var ppMEMs = (LeechCore.MEM_SCATTER_NATIVE**)pppMEMs.ToPointer();
            int i = 0;
            foreach (var mem in _mems.Values)
            {
                var pMEM = ppMEMs[i++];
                if (pMEM is null)
                    continue;
                pMEM->qwA = mem.qwA;
                pMEM->cb = mem.cb;
            }

            _ = Vmmi.VMMDLL_MemReadScatter(_vmm, _pid, pppMEMs, (uint)cMems, _flags);

            for (i = 0; i < cMems; i++)
            {
                var pMEM = ppMEMs[i];
                if (pMEM is null)
                    continue;
                if (pMEM->f)
                {
                    _mems[VmmUtilities.PAGE_ALIGN(pMEM->qwA)] = new(
                        qwA: pMEM->qwA,
                        cb: pMEM->cb,
                        f: true,
                        pb: pMEM->pb);
                }
            }

            return pppMEMs; // caller is responsible for freeing
        }
        catch
        {
            Lci.LcMemFree(pppMEMs);
            throw;
        }
    }

    /// <summary>
    /// Process the Scatter Read bytes into the span.
    /// </summary>
    /// <typeparam name="T">Span type</typeparam>
    /// <param name="addr">Address of read.</param>
    /// <param name="span">Result buffer</param>
    /// <returns><see langword="true"/> if successful, otherwise <see langword="false"/>.</returns>
    private bool ReadSpanInternal<T>(ulong addr, Span<T> span)
        where T : unmanaged
    {
        lock (_sync)
            checked
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (span.IsEmpty)
                    return false;

                var spanBytes = MemoryMarshal.AsBytes(span);
                int cbTotal = spanBytes.Length;
                ulong numPages = VmmUtilities.ADDRESS_AND_SIZE_TO_SPAN_PAGES(addr, (uint)cbTotal);
                ulong basePageAddr = VmmUtilities.PAGE_ALIGN(addr);

                int pageOffset = (int)VmmUtilities.BYTE_OFFSET(addr);
                int cb = Math.Min(cbTotal, 0x1000 - pageOffset);
                int cbRead = 0;

                for (ulong p = 0; p < numPages; p++)
                {
                    ulong pageAddr = basePageAddr + (p << 12);
                    if (!_mems.TryGetValue(pageAddr, out var mem) || !mem.f)
                        return false;

                    var data = mem.Data;

                    if (p == 0 && data.Length != 0x1000) // Tiny mem
                    {
                        pageOffset = (int)(addr - mem.qwA);
                        // Validate the read falls within the tiny MEM range
                        if (addr < mem.qwA || addr + (ulong)cb > mem.qwA + (ulong)data.Length)
                            return false;
                    }

                    data
                        .Slice(pageOffset, cb)
                        .CopyTo(spanBytes.Slice(cbRead, cb));

                    cbRead += cb;
                    cb = Math.Clamp(cbTotal - cbRead, 0, 0x1000);
                    pageOffset = 0; // Next page (if any) starts at 0x0
                }

                return cbRead == cbTotal;
            }
    }

    /// <summary>
    /// <see cref="object.ToString"/> override.
    /// </summary>
    /// <remarks>
    /// Prints the state of the <see cref="VmmScatterSlim"/> object.
    /// </remarks>
    public override string ToString()
    {
        if (_disposed)
        {
            return "VmmScatter:NotValid";
        }

        if (_pid == Vmm.PID_PHYSICALMEMORY)
        {
            return "VmmScatter:physical";
        }

        return $"VmmScatter:virtual:{_pid}";
    }

    private void FreeScatter()
    {
        if (_scatter != IntPtr.Zero)
        {
            Lci.LcMemFree(_scatter);
            _scatter = IntPtr.Zero;
        }
    }

    ~VmmScatterSlim() => Dispose(disposing: false);

    public void Dispose()
    {
        lock (_sync)
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    private void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, true) == false)
        {
            if (disposing)
            {
                Completed = null;
                _mems.Dispose();
            }
            FreeScatter();
        }
    }

    #endregion
}