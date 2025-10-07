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

using System.Buffers;
using System.Text;
using VmmSharpEx.Internal;
using VmmSharpEx.Options;

namespace VmmSharpEx;

/// <summary>
/// The VmmScatterMemory class is used to ease the reading and writing of memory in bulk using the VMM Scatter API.
/// </summary>
public sealed class VmmScatter : IDisposable
{
    #region Base Functionality

    private readonly Vmm _vmm;
    private readonly uint _pid;
    private IntPtr _h;

    private VmmScatter()
    {
        ;
    }

    internal VmmScatter(Vmm vmm, uint pid, VmmFlags flags = VmmFlags.NONE)
    {
        _vmm = vmm;
        _pid = pid;
        _h = Create(vmm, pid, flags);
    }

    private static IntPtr Create(Vmm vmm, uint pid, VmmFlags flags = VmmFlags.NONE)
    {
        var hS = Vmmi.VMMDLL_Scatter_Initialize(vmm, pid, flags);
        if (hS == IntPtr.Zero)
        {
            throw new VmmException("Failed to create VmmScatter handle!");
        }

        return hS;
    }

    ~VmmScatter()
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
        if (Interlocked.Exchange(ref _h, IntPtr.Zero) is IntPtr h && h != IntPtr.Zero)
        {
            Vmmi.VMMDLL_Scatter_CloseHandle(h);
        }
    }

    /// <summary>
    /// ToString override.
    /// </summary>
    public override string ToString()
    {
        if (_h == IntPtr.Zero)
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
    /// <param name="qwA">Address of the memory to be read.</param>
    /// <param name="cb">Length in bytes of the data to be read.</param>
    /// <returns>true/false.</returns>
    public bool PrepareRead(ulong qwA, uint cb)
    {
        return Vmmi.VMMDLL_Scatter_Prepare(_h, qwA, cb);
    }

    /// <summary>
    /// Prepare to read memory of a certain struct.
    /// </summary>
    /// <typeparam name="T">Struct type to read.</typeparam>
    /// <param name="qwA">Address of the memory to be read.</param>
    /// <returns>true/false.</returns>
    public unsafe bool PrepareReadValue<T>(ulong qwA)
        where T : unmanaged, allows ref struct
    {
        uint cb = (uint)sizeof(T);
        return Vmmi.VMMDLL_Scatter_Prepare(_h, qwA, cb);
    }

    /// <summary>
    /// Prepare to read memory from a contiguous array of a certain struct.
    /// </summary>
    /// <typeparam name="T">Struct type to read.</typeparam>
    /// <param name="qwA">Address of the memory to be read.</param>
    /// <param name="count">Number of elements to be read.</param>
    /// <returns>true/false.</returns>
    public unsafe bool PrepareReadContiguous<T>(ulong qwA, int count)
        where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count, nameof(count));
        uint cb = checked((uint)sizeof(T) * (uint)count);
        return Vmmi.VMMDLL_Scatter_Prepare(_h, qwA, cb);
    }

    /// <summary>
    /// Prepare to write an array of a certain struct to memory.
    /// </summary>
    /// <typeparam name="T">The type of struct to write.</typeparam>
    /// <param name="qwA">The address where to write the data.</param>
    /// <param name="data">The data to write to memory.</param>
    /// <returns>true/false.</returns>
    public unsafe bool PrepareWriteArray<T>(ulong qwA, T[] data)
        where T : unmanaged
    {
        _vmm.ThrowIfMemWritesDisabled();
        uint cb = checked((uint)sizeof(T) * (uint)data.Length);
        fixed (T* pb = data)
        {
            return Vmmi.VMMDLL_Scatter_PrepareWrite(_h, qwA, (byte*)pb, cb);
        }
    }

    /// <summary>
    /// Prepare to write a span of a certain struct to memory.
    /// </summary>
    /// <typeparam name="T">The type of struct to write.</typeparam>
    /// <param name="qwA">The address where to write the data.</param>
    /// <param name="data">The data to write to memory.</param>
    /// <returns>true/false.</returns>
    public unsafe bool PrepareWriteSpan<T>(ulong qwA, Span<T> data)
        where T : unmanaged
    {
        _vmm.ThrowIfMemWritesDisabled();
        uint cb = checked((uint)sizeof(T) * (uint)data.Length);
        fixed (T* pb = data)
        {
            return Vmmi.VMMDLL_Scatter_PrepareWrite(_h, qwA, (byte*)pb, cb);
        }
    }

    /// <summary>
    /// Prepare to write a struct to memory.
    /// </summary>
    /// <typeparam name="T">The type of struct to write.</typeparam>
    /// <param name="qwA">The address where to write the data.</param>
    /// <param name="value">The data to write to memory.</param>
    /// <returns>true/false.</returns>
    public unsafe bool PrepareWriteValue<T>(ulong qwA, T value)
        where T : unmanaged, allows ref struct
    {
        _vmm.ThrowIfMemWritesDisabled();
        uint cb = (uint)sizeof(T);
        return Vmmi.VMMDLL_Scatter_PrepareWrite(_h, qwA, (byte*)&value, cb);
    }

    /// <summary>
    /// Execute any prepared read and/or write operations.
    /// </summary>
    /// <returns>true/false.</returns>
    public bool Execute()
    {
        return Vmmi.VMMDLL_Scatter_Execute(_h);
    }

    /// <summary>
    /// Read memory from an address into a struct type.
    /// </summary>
    /// <typeparam name="T">The type of struct to read.</typeparam>
    /// <param name="qwA">Address to read from.</param>
    /// <param name="result">true/false</param>
    /// <returns>true/false.</returns>
    public unsafe bool ReadValue<T>(ulong qwA, out T result)
        where T : unmanaged, allows ref struct
    {
        uint cb = (uint)sizeof(T);
        result = default;
        fixed (T* pb = &result)
        {
            if (!Vmmi.VMMDLL_Scatter_Read(_h, qwA, cb, (byte*)pb, out var cbRead) || cbRead != cb)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Read memory from an address into an array of a certain type.
    /// </summary>
    /// <typeparam name="T">The type of struct to read.</typeparam>
    /// <param name="qwA">Address to read from.</param>
    /// <param name="count">The number of array items to read.</param>
    /// <returns>Array of objects read. Null on fail.</returns>
    public unsafe T[] ReadArray<T>(ulong qwA, int count)
        where T : unmanaged
    {
        var data = new T[count];
        uint cb = checked((uint)sizeof(T) * (uint)count);
        fixed (T* pb = data)
        {
            if (!Vmmi.VMMDLL_Scatter_Read(_h, qwA, cb, (byte*)pb, out var cbRead) || cbRead != cb)
            {
                return null;
            }
        }
        return data;
    }

    /// <summary>
    /// Read memory from an address into a Span of a certain type.
    /// </summary>
    /// <typeparam name="T">The type of struct to read.</typeparam>
    /// <param name="qwA">Address to read from.</param>
    /// <param name="span">The span to read into.</param>
    /// <returns>TRUE if successful, otherwise FALSE.</returns>
    public unsafe bool ReadSpan<T>(ulong qwA, Span<T> span)
        where T : unmanaged
    {
        uint cb = checked((uint)sizeof(T) * (uint)span.Length);
        fixed (T* pb = span)
        {
            return Vmmi.VMMDLL_Scatter_Read(_h, qwA, cb, (byte*)pb, out var cbRead) && cbRead == cb;
        }
    }

    /// <summary>
    /// Read memory from an address into a managed string.
    /// </summary>
    /// <param name="qwA">Address to read from.</param>
    /// <param name="cb">Number of bytes to read. Keep in mind some string encodings are 2-4 bytes per character.</param>
    /// <param name="encoding">String Encoding for this read.</param>
    /// <returns>C# Managed System.String. Null if failed.</returns>
    public string ReadString(ulong qwA, int cb, Encoding encoding)
    {
        byte[] rentedBytes = null;
        char[] rentedChars = null;
        try
        {
            Span<byte> bytesSource = cb <= 256 ?
                stackalloc byte[cb] : (rentedBytes = ArrayPool<byte>.Shared.Rent(cb));
            var bytes = bytesSource.Slice(0, cb); // Rented Pool can have more than cb
            if (!ReadSpan(qwA, bytes))
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
    /// Clear the VmmScatter object to allow for new operations.
    /// </summary>
    /// <param name="flags"></param>
    /// <returns>true/false.</returns>
    public bool Clear(VmmFlags flags)
    {
        return Vmmi.VMMDLL_Scatter_Clear(_h, _pid, flags);
    }

    #endregion
}