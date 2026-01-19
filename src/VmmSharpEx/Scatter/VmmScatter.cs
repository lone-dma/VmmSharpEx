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

using Collections.Pooled;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using VmmSharpEx.Internal;
using VmmSharpEx.Options;

namespace VmmSharpEx.Scatter;

/// <summary>
/// The <see cref="VmmScatter"/> class is used to ease the reading and writing of memory in bulk using the VMM Scatter API.
/// </summary>
/// <remarks>
/// This API has been enhanced in VmmSharpEx over the original VmmSharp implementation.
/// </remarks>
public sealed class VmmScatter : IDisposable
{
    #region Base Functionality

    private readonly Lock _sync = new();
    private readonly Dictionary<ScatterReadKey, ScatterReadBuffer> _preparedReads = new();
    private readonly Vmm _vmm;
    private uint _pid;
    private VmmFlags _flags;
    private IntPtr _handle;
    private bool _isPrepared;

    /// <summary>
    /// <see langword="true"/> if the VmmScatter handle has at least one operation prepared, otherwise <see langword="false"/>.
    /// </summary>
    public bool IsPrepared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _isPrepared;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private set
        {
            if (value is true)
            {
                _isPrepared = true;
            }
        }
    }

    /// <summary>
    /// True if the VmmScatter handle has been disposed, otherwise false.
    /// </summary>
    public bool Disposed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _handle == IntPtr.Zero;
    }

    /// <summary>
    /// Event is fired upon completion of <see cref="Execute"/>. Exceptions are handled/ignored.
    /// </summary>
    public event EventHandler<VmmScatter>? Completed;
    private void InvokeCompleted(Delegate[]? callbacks)
    {
        if (callbacks is null)
            return;
        foreach (var callback in callbacks)
        {
            try
            {
                ((EventHandler<VmmScatter>)callback).Invoke(this, this);
            }
            catch { }
        }
    }

    private VmmScatter() { throw new NotImplementedException(); }

    internal VmmScatter(Vmm vmm, uint pid, VmmFlags flags = VmmFlags.NONE)
    {
        _vmm = vmm;
        _pid = pid;
        _flags = flags;
        _handle = Create(vmm, pid, flags);
    }

    private static IntPtr Create(Vmm vmm, uint pid, VmmFlags flags)
    {
        var hS = Vmmi.VMMDLL_Scatter_Initialize(vmm, pid, flags);
        if (hS == IntPtr.Zero)
        {
            throw new VmmException("Failed to create VmmScatter handle!");
        }

        return hS;
    }

    ~VmmScatter() => Dispose(disposing: false);

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private unsafe void Dispose(bool disposing)
    {
        lock (_sync)
        {
            if (_handle != IntPtr.Zero)
            {
                if (disposing)
                {
                    Completed = null;
                }

                // Free all native allocations
                FreeNativeAllocations();

                Vmmi.VMMDLL_Scatter_CloseHandle(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }

    private unsafe void FreeNativeAllocations()
    {
        foreach (var entry in _preparedReads.Values)
        {
            entry.Free();
        }
        _preparedReads.Clear();
    }

    /// <summary>
    /// <see cref="object.ToString"/> override.
    /// </summary>
    /// <remarks>
    /// Prints the state of the <see cref="VmmScatter"/> object.
    /// </remarks>
    public override string ToString()
    {
        if (Disposed)
        {
            return "VmmScatter:Disposed";
        }

        if (_pid == Vmm.PID_PHYSICALMEMORY)
        {
            return "VmmScatter:physical";
        }

        return $"VmmScatter:virtual:{_pid}";
    }

    #endregion

    #region Memory Read/Write

    /// <summary>
    /// Prepare to read memory of a certain size.
    /// </summary>
    /// <remarks>
    /// Can be used with any Read* method as long as the size matches.
    /// For example this would be used with <see cref="ReadString(ulong, int, Encoding)"/>, after calling <see cref="Execute"/>.
    /// </remarks>
    /// <param name="address">Address of the memory to be read.</param>
    /// <param name="cb">Count of bytes to be read.</param>
    /// <returns><see langword="true"/> if the operation is successful, otherwise <see langword="false"/>.</returns>
    public unsafe bool PrepareRead(ulong address, uint cb)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);

            var key = new ScatterReadKey(address, cb);
            if (_preparedReads.ContainsKey(key))
            {
                // Already prepared
                return true;
            }
            // Allocate native memory for this read
            var entry = new ScatterReadBuffer(cb);

            if (Vmmi.VMMDLL_Scatter_PrepareEx(_handle, address, cb, entry.Buffer, entry.CbReadPtr) &&
                _preparedReads.TryAdd(key, entry))
            {
                IsPrepared = true;
                return IsPrepared;
            }
            entry.Free();
            return false;
        }
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
    /// <returns><see langword="true"/> if the operation is successful, otherwise <see langword="false"/>.</returns>
    public unsafe bool PrepareReadArray<T>(ulong address, int count)
        where T : unmanaged
    {
        uint cb = checked((uint)sizeof(T) * (uint)count);
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
    /// <returns><see langword="true"/> if the operation is successful, otherwise <see langword="false"/>.</returns>
    public unsafe bool PrepareReadValue<T>(ulong address)
        where T : unmanaged, allows ref struct
    {
        uint cb = (uint)sizeof(T);
        return PrepareRead(address, cb);
    }

    /// <summary>
    /// Prepare to read memory of a Windows x64 pointer type.
    /// </summary>
    /// <remarks>
    /// Corresponds with the <see cref="ReadPtr(ulong, out VmmPointer)"/> method, that should be called after <see cref="Execute"/>.
    /// </remarks>
    /// <param name="address">Address of the memory to be read.</param>
    /// <returns><see langword="true"/> if the operation is successful, otherwise <see langword="false"/>.</returns>
    public unsafe bool PrepareReadPtr(ulong address)
    {
        uint cb = (uint)sizeof(VmmPointer);
        return PrepareRead(address, cb);
    }

    /// <summary>
    /// Prepare to write a span of <see langword="unmanaged"/> struct type <typeparamref name="T"/> to memory.
    /// </summary>
    /// <remarks>
    /// Must call <see cref="Execute"/> for this write to be committed.
    /// </remarks>
    /// <typeparam name="T">The <see langword="unmanaged"/> struct type for this operation.</typeparam>
    /// <param name="address">The address that will be written to.</param>
    /// <param name="data">The data that will be written.</param>
    /// <returns><see langword="true"/> if the operation is successful, otherwise <see langword="false"/>.</returns>
    public unsafe bool PrepareWriteSpan<T>(ulong address, Span<T> data)
        where T : unmanaged
    {
        lock (_sync)
        {
            _vmm.ThrowIfMemWritesDisabled();
            uint cb = checked((uint)sizeof(T) * (uint)data.Length);
            fixed (T* pb = data)
            {
                IsPrepared = Vmmi.VMMDLL_Scatter_PrepareWrite(_handle, address, (byte*)pb, cb);
                return IsPrepared;
            }
        }
    }

    /// <summary>
    /// Prepare to write an <see langword="unmanaged"/> struct of type <typeparamref name="T"/> to memory.
    /// </summary>
    /// <remarks>
    /// Must call <see cref="Execute"/> for this write to be committed.
    /// </remarks>
    /// <typeparam name="T">The <see langword="unmanaged"/> struct type for this operation.</typeparam>
    /// <param name="address">The address that will be written to.</param>
    /// <param name="value">The value that will be written.</param>
    /// <returns><see langword="true"/> if the operation is successful, otherwise <see langword="false"/>.</returns>
    public unsafe bool PrepareWriteValue<T>(ulong address, T value)
        where T : unmanaged, allows ref struct
    {
        lock (_sync)
        {
            _vmm.ThrowIfMemWritesDisabled();
            uint cb = (uint)sizeof(T);
            IsPrepared = Vmmi.VMMDLL_Scatter_PrepareWrite(_handle, address, (byte*)&value, cb);
            return IsPrepared;
        }
    }

    /// <summary>
    /// Execute any prepared read, and/or write operations.
    /// </summary>
    /// <remarks>
    /// If there are no prepared operations, this method is a no-op.
    /// Uses ExecuteRead for read operations (PrepareEx buffers are filled directly by native code).
    /// </remarks>
    /// <exception cref="VmmException"></exception>
    /// <exception cref="ObjectDisposedException">Thrown if the scatter handle has been disposed.</exception>
    public void Execute()
    {
        try
        {
            Delegate[]? callbacks;
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(Disposed, this);

                if (!IsPrepared)
                    return;

                if (!Vmmi.VMMDLL_Scatter_Execute(_handle))
                    throw new VmmException("Scatter Operation Failed");
                callbacks = Completed?.GetInvocationList();
            }
            InvokeCompleted(callbacks);
        }
        finally
        {
            GC.KeepAlive(this);
        }
    }

    /// <summary>
    /// Read memory from an address into a byte array.
    /// </summary>
    /// <remarks>
    /// This should be called after <see cref="Execute"/>.
    /// NOTE: This method incurs a heap allocation for the returned byte array. For high-performance use other read methods instead.
    /// Data is copied from pre-filled native buffer (PrepareEx pattern).
    /// </remarks>
    /// <param name="address">Address to read from.</param>
    /// <param name="cb">Count of bytes to be read.</param>
    /// <param name="cbRead">Count of bytes actually read.</param>
    /// <returns>A byte array with the read memory, otherwise <see langword="null"/>. Be sure to also check <paramref name="cbRead"/>.</returns>
    public unsafe byte[]? Read(ulong address, uint cb, out uint cbRead)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            var key = new ScatterReadKey(address, cb);
            if (_preparedReads.TryGetValue(key, out var prep))
            {
                cbRead = prep.CbRead;
                if (cbRead == 0)
                    return null;

                var arr = new byte[cb];
                new ReadOnlySpan<byte>(prep.Buffer, checked((int)Math.Min(cb, cbRead))).CopyTo(arr);
                return arr;
            }

            cbRead = 0;
            return null;
        }
    }

    /// <summary>
    /// Read memory from an address to a pointer of a buffer that can accept <paramref name="cb"/> bytes.
    /// </summary>
    /// <param name="address">Address to read from.</param>
    /// <param name="cb">Count of bytes to be read.</param>
    /// <param name="pb">Pointer to buffer to receive read. You must make sure the buffer is pinned/fixed.</param>
    /// <param name="cbRead">Count of bytes actually read.</param>
    /// <returns>TRUE if successful, otherwise FALSE. Be sure to also check <paramref name="cbRead"/>.</returns>
    public unsafe bool Read(ulong address, uint cb, void* pb, out uint cbRead)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            var key = new ScatterReadKey(address, cb);
            if (_preparedReads.TryGetValue(key, out var prep))
            {
                cbRead = prep.CbRead;
                if (cbRead == 0)
                    return false;

                int copyLen = checked((int)Math.Min(cb, cbRead));
                new ReadOnlySpan<byte>(prep.Buffer, copyLen).CopyTo(new Span<byte>(pb, copyLen));
                return true;
            }

            cbRead = 0;
            return false;
        }
    }

    /// <summary>
    /// Read memory from an address to a pointer of a buffer that can accept <paramref name="cb"/> bytes.
    /// </summary>
    /// <param name="address">Address to read from.</param>
    /// <param name="cb">Count of bytes to be read.</param>
    /// <param name="pb">Pointer to buffer to receive read. You must make sure the buffer is pinned/fixed.</param>
    /// <param name="cbRead">Count of bytes actually read.</param>
    /// <returns>TRUE if successful, otherwise FALSE. Be sure to also check <paramref name="cbRead"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool Read(ulong address, uint cb, IntPtr pb, out uint cbRead) =>
        Read(address, cb, pb.ToPointer(), out cbRead);

    /// <summary>
    /// Read memory from an address into a struct type.
    /// </summary>
    /// <remarks>
    /// This should be called after <see cref="Execute"/>.
    /// Data is copied from pre-filled native buffer (PrepareEx pattern).
    /// </remarks>
    /// <typeparam name="T">The <see langword="unmanaged"/> struct type for this operation.</typeparam>
    /// <param name="address">Address to read from.</param>
    /// <param name="result">Field in which the result <typeparamref name="T"/> is populated. If the read fails this will be <see langword="default"/>.</param>
    /// <returns><see langword="true"/> if the operation is successful, otherwise <see langword="false"/>.</returns>
    public unsafe bool ReadValue<T>(ulong address, out T result)
        where T : unmanaged, allows ref struct
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            uint cb = (uint)sizeof(T);
            result = default;

            var key = new ScatterReadKey(address, cb);
            if (_preparedReads.TryGetValue(key, out var prep))
            {
                if (prep.CbRead != cb)
                    return false;

                result = *(T*)prep.Buffer;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Read memory from an address into a Windows x64 pointer type.
    /// </summary>
    /// <remarks>
    /// This should be called after <see cref="Execute"/>.
    /// </remarks>
    /// <param name="address">Address to read from.</param>
    /// <param name="result">Field in which the result <see cref="VmmPointer"/> is populated. If the read fails this will be <see langword="default"/>.</param>
    /// <returns><see langword="true"/> if the operation is successful, otherwise <see langword="false"/>.</returns>
    public bool ReadPtr(ulong address, out VmmPointer result)
    {
        if (ReadValue<VmmPointer>(address, out result) && result.IsValidVA)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Read memory from an address into an array of a certain type.
    /// </summary>
    /// <remarks>
    /// NOTE: This method incurs a heap allocation for the returned byte array. For high-performance use other read methods instead. This should be called after <see cref="Execute"/>.
    /// Data is copied from pre-filled native buffer (PrepareEx pattern).
    /// </remarks>
    /// <typeparam name="T">The <see langword="unmanaged"/> struct type for this operation.</typeparam>
    /// <param name="address">Address to read from.</param>
    /// <param name="count">The number of array elements to read.</param>
    /// <returns>An array on success; otherwise <see langword="null"/>.</returns>
    public unsafe T[]? ReadArray<T>(ulong address, int count)
        where T : unmanaged
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            uint cb = checked((uint)sizeof(T) * (uint)count);

            var key = new ScatterReadKey(address, cb);
            if (_preparedReads.TryGetValue(key, out var prep))
            {
                if (prep.CbRead != cb)
                    return null;

                var array = new T[count];
                new ReadOnlySpan<byte>(prep.Buffer, checked((int)cb)).CopyTo(MemoryMarshal.AsBytes(array.AsSpan()));
                return array;
            }

            return null;
        }
    }

    /// <summary>
    /// Read memory from an address into a pooled array of a certain type.
    /// </summary>
    /// <remarks>
    /// This should be called after <see cref="Execute"/>.
    /// Data is copied from pre-filled native buffer (PrepareEx pattern).
    /// </remarks>
    /// <typeparam name="T">The <see langword="unmanaged"/> struct type for this operation.</typeparam>
    /// <param name="address">Address to read from.</param>
    /// <param name="count">The number of array elements to read.</param>
    /// <returns><see cref="IMemoryOwner{T}"/> lease, or <see langword="null"/> if failed. Be sure to call <see cref="IDisposable.Dispose()"/> when done.</returns>
    public unsafe IMemoryOwner<T>? ReadPooled<T>(ulong address, int count)
        where T : unmanaged
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            uint cb = checked((uint)sizeof(T) * (uint)count);

            var key = new ScatterReadKey(address, cb);
            if (_preparedReads.TryGetValue(key, out var prep))
            {
                if (prep.CbRead != cb)
                    return null;

                var data = new PooledMemory<T>(count);
                new ReadOnlySpan<byte>(prep.Buffer, checked((int)cb)).CopyTo(MemoryMarshal.AsBytes(data.Span));
                return data;
            }

            return null;
        }
    }

    /// <summary>
    /// Read memory from an address into a Span of a certain type.
    /// </summary>
    /// <remarks>
    /// This should be called after <see cref="Execute"/>.
    /// Data is copied from pre-filled native buffer (PrepareEx pattern).
    /// </remarks>
    /// <typeparam name="T">The <see langword="unmanaged"/> struct type for this operation.</typeparam>
    /// <param name="address">Address to read from.</param>
    /// <param name="span">The span to read into.</param>
    /// <returns><see langword="true"/> if the operation is successful, otherwise <see langword="false"/>.</returns>
    public unsafe bool ReadSpan<T>(ulong address, Span<T> span)
        where T : unmanaged
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            uint cb = checked((uint)sizeof(T) * (uint)span.Length);

            var key = new ScatterReadKey(address, cb);
            if (_preparedReads.TryGetValue(key, out var prep))
            {
                if (prep.CbRead != cb)
                    return false;

                new ReadOnlySpan<byte>(prep.Buffer, checked((int)cb)).CopyTo(MemoryMarshal.AsBytes(span));
                return true;
            }

            return false;
        }
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
    /// Clear the <see cref="VmmScatter"/> object to allow for new operations.
    /// Also clears any previously set <see cref="Completed"/> event handlers.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: Using <see cref="Clear(VmmFlags?, uint?)"/> and reusing a handle does not offer much (if any) performance benefit over creating a new handle.
    /// Be sure to profile and compare performance before using this in performance critical code.
    /// </remarks>
    /// <param name="flags">[Optional] Flags to be set for new operations, otherwise uses existing flags.</param>
    /// <param name="pid">[Optional] PID to be set for new operations, otherwise uses existing PID.</param>
    /// <exception cref="VmmException"></exception>
    public void Clear(VmmFlags? flags = null, uint? pid = null)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            if (flags is VmmFlags f)
                _flags = f;
            if (pid is uint p)
                _pid = p;
            _isPrepared = default;
            Completed = default;

            // Free all native allocations from previous operations
            FreeNativeAllocations();

            if (!Vmmi.VMMDLL_Scatter_Clear(_handle, _pid, _flags))
                throw new VmmException("Failed to clear VmmScatter Handle.");
        }
    }

    #endregion

    #region Types

    // Key for prepared read lookup - uses Address+Size for equality
    private readonly struct ScatterReadKey : IEquatable<ScatterReadKey>
    {
        public readonly ulong Address;
        public readonly uint Size;

        public ScatterReadKey(ulong address, uint size)
        {
            Address = address;
            Size = size;
        }

        public bool Equals(ScatterReadKey other) => Address == other.Address && Size == other.Size;
        public override bool Equals(object? obj) => obj is ScatterReadKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Address, Size);
        public static bool operator ==(ScatterReadKey left, ScatterReadKey right) => left.Equals(right);
        public static bool operator !=(ScatterReadKey left, ScatterReadKey right) => !left.Equals(right);
    }

    // Immutable buffer holder for prepared reads
    private readonly unsafe struct ScatterReadBuffer
    {
        public readonly byte* Buffer;
        public readonly uint* CbReadPtr;

        public ScatterReadBuffer(uint cb)
        {
            ArgumentOutOfRangeException.ThrowIfZero(cb, nameof(cb));
            Buffer = (byte*)NativeMemory.Alloc(cb);
            CbReadPtr = (uint*)NativeMemory.AllocZeroed(sizeof(uint));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free()
        {
            NativeMemory.Free(Buffer);
            NativeMemory.Free(CbReadPtr);
        }

        public uint CbRead => *CbReadPtr;
    }

    #endregion
}