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
using System.Text;
using VmmSharpEx.Extensions;
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
    private readonly Vmm _vmm;
    private readonly uint _pid;
    private readonly VmmFlags _flags;
    private readonly bool _isKernel;
    private readonly bool _isUser;
    private IntPtr _handle;
    private bool _disposed;

    private volatile bool _isPrepared;
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
    /// Event is fired upon completion of <see cref="Execute"/>. Exceptions are handled/ignored.
    /// </summary>
    public event EventHandler<VmmScatter>? Completed;
    private void OnCompleted()
    {
        foreach (var callback in Completed?.GetInvocationList() ?? Enumerable.Empty<Delegate>())
        {
            try
            {
                ((EventHandler<VmmScatter>)callback).Invoke(this, this);
            }
            catch { }
        }
    }

    private VmmScatter() { throw new NotImplementedException(); }

    public VmmScatter(Vmm vmm, uint pid, VmmFlags flags = VmmFlags.NONE)
    {
        _vmm = vmm;
        _pid = pid;
        _flags = flags;
        bool isPhysical = pid == Vmm.PID_PHYSICALMEMORY;
        _isKernel = !isPhysical && (pid & Vmm.PID_PROCESS_WITH_KERNELMEMORY) != 0;
        _isUser = !isPhysical && !_isKernel;
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

    private void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, true) == false)
        {
            if (disposing)
            {
                Completed = null;
            }
            // No managed locks since a finalizer could be running.
            Vmmi.VMMDLL_Scatter_CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }

    /// <summary>
    /// <see cref="object.ToString"/> override.
    /// </summary>
    /// <remarks>
    /// Prints the state of the <see cref="VmmScatter"/> object.
    /// </remarks>
    public override string ToString()
    {
        if (_disposed)
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
    public bool PrepareRead(ulong address, int cb)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if ((_isKernel && !address.IsValidKernelVA()) ||
            (_isUser && !address.IsValidUserVA()) ||
            cb <= 0)
        {
            return false;
        }
        bool ret;
        lock (_sync)
        {
            IsPrepared = ret = Vmmi.VMMDLL_Scatter_Prepare(_handle, address, checked((uint)cb));
        }
        return ret;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool PrepareReadArray<T>(ulong address, int count)
        where T : unmanaged
    {
        if (count <= 0)
            return false;
        return PrepareRead(address, checked(sizeof(T) * count));
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
    /// <returns><see langword="true"/> if the operation is successful, otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool PrepareReadPtr(ulong address)
    {
        return PrepareRead(address, sizeof(VmmPointer));
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        _vmm.ThrowIfMemWritesDisabled();
        if (data.IsEmpty)
            return false;
        int cb = checked(sizeof(T) * data.Length);
        bool ret;
        lock (_sync)
        {
            fixed (T* pb = data)
            {
                IsPrepared = ret = Vmmi.VMMDLL_Scatter_PrepareWrite(_handle, address, (byte*)pb, (uint)cb);
                return ret;
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        _vmm.ThrowIfMemWritesDisabled();
        bool ret;
        lock (_sync)
        {
            IsPrepared = ret = Vmmi.VMMDLL_Scatter_PrepareWrite(_handle, address, (byte*)&value, (uint)sizeof(T));
        }
        return ret;
    }

    /// <summary>
    /// Execute any prepared read, and/or write operations.
    /// </summary>
    /// <remarks>
    /// If there are no prepared operations, this method is a no-op.
    /// </remarks>
    /// <exception cref="VmmException"></exception>
    public void Execute()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsPrepared) // VMMDLL_Scatter_Execute will return FALSE if no operations are prepared, this may be expected behavior so we don't want to throw here.
        {
            lock (_sync)
            {
                if (!Vmmi.VMMDLL_Scatter_Execute(_handle))
                    throw new VmmException("Scatter Operation Failed");
            }
            OnCompleted();
        }
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
    /// <param name="cbRead">Count of bytes actually read.</param>
    /// <returns>A byte array with the read memory, otherwise <see langword="null"/>. Be sure to also check <paramref name="cbRead"/>.</returns>
    public unsafe byte[]? Read(ulong address, int cb, out uint cbRead)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (cb <= 0)
        {
            cbRead = 0;
            return null;
        }
        var arr = new byte[cb];
        lock (_sync)
        {
            fixed (byte* pb = arr)
            {
                if (!Vmmi.VMMDLL_Scatter_Read(_handle, address, (uint)cb, pb, out cbRead))
                {
                    return null;
                }
            }
        }
        return arr;
    }

    /// <summary>
    /// Read memory from an address to a pointer of a buffer that can accept <paramref name="cb"/> bytes.
    /// </summary>
    /// <param name="address">Address to read from.</param>
    /// <param name="cb">Count of bytes to be read.</param>
    /// <param name="pb">Pointer to buffer to receive read. You must make sure the buffer is pinned/fixed.</param>
    /// <param name="cbRead">Count of bytes actually read.</param>
    /// <returns><see langword="true"/> if the operation is successful, otherwise <see langword="false"/>. Be sure to also check <paramref name="cbRead"/>.</returns>
    public unsafe bool Read(ulong address, int cb, void* pb, out uint cbRead)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (cb <= 0)
        {
            cbRead = 0;
            return false;
        }
        lock (_sync)
        {
            return Vmmi.VMMDLL_Scatter_Read(_handle, address, (uint)cb, (byte*)pb, out cbRead);
        }
    }

    /// <summary>
    /// Read memory from an address to a pointer of a buffer that can accept <paramref name="cb"/> bytes.
    /// </summary>
    /// <param name="address">Address to read from.</param>
    /// <param name="cb">Count of bytes to be read.</param>
    /// <param name="pb">Pointer to buffer to receive read. You must make sure the buffer is pinned/fixed.</param>
    /// <param name="cbRead">Count of bytes actually read.</param>
    /// <returns><see langword="true"/> if the operation is successful, otherwise <see langword="false"/>. Be sure to also check <paramref name="cbRead"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool Read(ulong address, int cb, IntPtr pb, out uint cbRead) =>
        Read(address, cb, pb.ToPointer(), out cbRead);

    /// <summary>
    /// Read memory from an address into a struct type.
    /// </summary>
    /// <remarks>
    /// This should be called after <see cref="Execute"/>.
    /// </remarks>
    /// <typeparam name="T">The <see langword="unmanaged"/> struct type for this operation.</typeparam>
    /// <param name="address">Address to read from.</param>
    /// <param name="result">Field in which the result <typeparamref name="T"/> is populated. If the read fails this will be <see langword="default"/>.</param>
    /// <returns><see langword="true"/> if the operation is successful, otherwise <see langword="false"/>.</returns>
    public unsafe bool ReadValue<T>(ulong address, out T result)
        where T : unmanaged, allows ref struct
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        result = default;
        uint cb = (uint)sizeof(T);
        lock (_sync)
        {
            fixed (T* pb = &result)
            {
                if (!Vmmi.VMMDLL_Scatter_Read(_handle, address, cb, (byte*)pb, out var cbRead) || cbRead != cb)
                {
                    return false;
                }
            }
        }
        return true;
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
    public unsafe T[]? ReadArray<T>(ulong address, int count)
        where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (count <= 0)
        {
            return null;
        }
        int cb = checked(sizeof(T) * count);
        var array = new T[count];
        lock (_sync)
        {
            fixed (T* pb = array)
            {
                if (!Vmmi.VMMDLL_Scatter_Read(_handle, address, (uint)cb, (byte*)pb, out var cbRead) || cbRead != cb)
                {
                    return null;
                }
            }
        }
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
    public unsafe IMemoryOwner<T>? ReadPooled<T>(ulong address, int count)
        where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (count <= 0)
        {
            return null;
        }
        int cb = checked(sizeof(T) * count);
        var data = new PooledMemory<T>(count);
        lock (_sync)
        {
            fixed (T* pb = data.Span)
            {
                if (!Vmmi.VMMDLL_Scatter_Read(_handle, address, (uint)cb, (byte*)pb, out var cbRead) || cbRead != cb)
                {
                    data.Dispose();
                    return null;
                }
            }
        }
        return data;
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
    /// <returns><see langword="true"/> if the operation is successful, otherwise <see langword="false"/>.</returns>
    public unsafe bool ReadSpan<T>(ulong address, Span<T> span)
        where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (span.IsEmpty)
        {
            return false;
        }
        int cb = checked(sizeof(T) * span.Length);
        lock (_sync)
        {
            fixed (T* pb = span)
            {
                return Vmmi.VMMDLL_Scatter_Read(_handle, address, (uint)cb, (byte*)pb, out var cbRead) && cbRead == cb;
            }
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (cb <= 0)
        {
            return null;
        }
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
    /// IMPORTANT: Using <see cref="Clear"/> and reusing a handle does not offer much (if any) performance benefit over creating a new handle.
    /// Be sure to profile and compare performance before using this in performance critical code.
    /// </remarks>
    /// <exception cref="VmmException"></exception>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _isPrepared = default;
        Completed = default;
        lock (_sync)
        {
            if (!Vmmi.VMMDLL_Scatter_Clear(_handle, _pid, _flags))
                throw new VmmException("Failed to clear VmmScatter Handle.");
        }
    }

    #endregion
}