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
using VmmSharpEx.Internal;
using VmmSharpEx.Options;

namespace VmmSharpEx.Scatter;

/// <summary>
/// The <see cref="VmmScatter"/> class is used to ease the reading and writing of memory in bulk using the official MemProcFS Scatter API via vmm.dll.
/// All operations incur native calls to vmm.dll (using VMMDLL_Scatter_Initialize).
/// </summary>
public sealed class VmmScatter : IScatter, IScatter<VmmScatter>, IDisposable
{
    #region Base Functionality

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
        _handle = Create(vmm, pid, flags);
    }

    static VmmScatter IScatter<VmmScatter>.Create(Vmm vmm, uint pid, VmmFlags flags)
    {
        return new VmmScatter(vmm, pid, flags);
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
        if (Interlocked.Exchange(ref _handle, IntPtr.Zero) is IntPtr h && h != IntPtr.Zero)
        {
            if (disposing)
            {
                Completed = null;
            }
            Vmmi.VMMDLL_Scatter_CloseHandle(h);
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
        if (_handle == IntPtr.Zero)
        {
            return "VmmScatter:NotValid";
        }

        if (_pid == 0xFFFFFFFF)
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
    public bool PrepareRead(ulong address, uint cb)
    {
        bool ret;
        IsPrepared = ret = Vmmi.VMMDLL_Scatter_Prepare(_handle, address, cb);
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
    public unsafe bool PrepareReadArray<T>(ulong address, int count)
        where T : unmanaged
    {
        uint cb = checked((uint)sizeof(T) * (uint)count);
        bool ret;
        IsPrepared = ret = Vmmi.VMMDLL_Scatter_Prepare(_handle, address, cb);
        return ret;
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
        bool ret;
        IsPrepared = ret = Vmmi.VMMDLL_Scatter_Prepare(_handle, address, cb);
        return ret;
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
        bool ret;
        IsPrepared = ret = Vmmi.VMMDLL_Scatter_Prepare(_handle, address, cb);
        return ret;
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
        _vmm.ThrowIfMemWritesDisabled();
        uint cb = checked((uint)sizeof(T) * (uint)data.Length);
        bool ret;
        fixed (T* pb = data)
        {
            IsPrepared = ret = Vmmi.VMMDLL_Scatter_PrepareWrite(_handle, address, (byte*)pb, cb);
            return ret;
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
        _vmm.ThrowIfMemWritesDisabled();
        uint cb = (uint)sizeof(T);
        bool ret;
        IsPrepared = ret = Vmmi.VMMDLL_Scatter_PrepareWrite(_handle, address, (byte*)&value, cb);
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
        if (IsPrepared) // VMMDLL_Scatter_Execute will return FALSE if no operations are prepared, this may be expected behavior so we don't want to throw here.
        {
            if (!Vmmi.VMMDLL_Scatter_Execute(_handle))
                throw new VmmException("Scatter Operation Failed");
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
    public unsafe byte[]? Read(ulong address, uint cb, out uint cbRead)
    {
        var arr = new byte[cb];
        fixed (byte* pb = arr)
        {
            if (!Vmmi.VMMDLL_Scatter_Read(_handle, address, cb, pb, out cbRead))
            {
                return null;
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
    /// <returns>TRUE if successful, otherwise FALSE. Be sure to also check <paramref name="cbRead"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool Read(ulong address, uint cb, void* pb, out uint cbRead)
    {
        return Vmmi.VMMDLL_Scatter_Read(_handle, address, cb, (byte*)pb, out cbRead);
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
    /// </remarks>
    /// <typeparam name="T">The <see langword="unmanaged"/> struct type for this operation.</typeparam>
    /// <param name="address">Address to read from.</param>
    /// <param name="result">Field in which the result <typeparamref name="T"/> is populated. If the read fails this will be <see langword="default"/>.</param>
    /// <returns><see langword="true"/> if the operation is successful, otherwise <see langword="false"/>.</returns>
    public unsafe bool ReadValue<T>(ulong address, out T result)
        where T : unmanaged, allows ref struct
    {
        uint cb = (uint)sizeof(T);
        result = default;
        fixed (T* pb = &result)
        {
            if (!Vmmi.VMMDLL_Scatter_Read(_handle, address, cb, (byte*)pb, out var cbRead) || cbRead != cb)
            {
                return false;
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
    /// </remarks>
    /// <typeparam name="T">The <see langword="unmanaged"/> struct type for this operation.</typeparam>
    /// <param name="address">Address to read from.</param>
    /// <param name="count">The number of array elements to read.</param>
    /// <returns>An array on success; otherwise <see langword="null"/>.</returns>
    public unsafe T[]? ReadArray<T>(ulong address, int count)
        where T : unmanaged
    {
        uint cb = checked((uint)sizeof(T) * (uint)count);
        var array = new T[count];
        fixed (T* pb = array)
        {
            if (!Vmmi.VMMDLL_Scatter_Read(_handle, address, cb, (byte*)pb, out var cbRead) || cbRead != cb)
            {
                return null;
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
        uint cb = checked((uint)sizeof(T) * (uint)count);
        var data = new PooledMemory<T>(count);
        fixed (T* pb = data.Span)
        {
            if (!Vmmi.VMMDLL_Scatter_Read(_handle, address, cb, (byte*)pb, out var cbRead) || cbRead != cb)
            {
                data.Dispose();
                return null;
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
        uint cb = checked((uint)sizeof(T) * (uint)span.Length);
        fixed (T* pb = span)
        {
            return Vmmi.VMMDLL_Scatter_Read(_handle, address, cb, (byte*)pb, out var cbRead) && cbRead == cb;
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
        if (flags is VmmFlags f)
            _flags = f;
        if (pid is uint p)
            _pid = p;
        _isPrepared = default;
        Completed = default;
        if (!Vmmi.VMMDLL_Scatter_Clear(_handle, _pid, _flags))
            throw new VmmException("Failed to clear VmmScatter Handle.");
    }

    #endregion
}