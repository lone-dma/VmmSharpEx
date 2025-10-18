﻿/*  
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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VmmSharpEx.Internal;
using VmmSharpEx.Options;

namespace VmmSharpEx;

/// <summary>
/// High-level managed wrapper over the native LeechCore API.
/// </summary>
/// <remarks>
/// This class wraps a native <c>LC_CONTEXT</c> handle created by LeechCore and exposes common read/write and control
/// operations against physical memory devices. It is typically acquired from <see cref="Vmm"/> via
/// <see cref="Vmm.LeechCore"/> when MemProcFS has been initialized with a LeechCore-backed device.
/// Native counterparts are defined in <c>leechcore.h</c> and implemented in <c>leechcore.dll</c>.
/// </remarks>
public sealed class LeechCore : IDisposable
{
    private readonly Vmm _parent;
    private IntPtr _h;

    private LeechCore() { }

    private LeechCore(IntPtr hLC)
    {
        _h = hLC;
    }

    /// <summary>
    /// Create a new inherited <see cref="LeechCore"/> instance from a given <see cref="Vmm"/> instance.
    /// </summary>
    /// <param name="vmm">The owning <see cref="Vmm"/> instance the LC context should be bound to.</param>
    /// <exception cref="VmmException">Thrown if the native LeechCore handle cannot be retrieved or duplicated.</exception>
    internal LeechCore(Vmm vmm)
    {
        if (vmm.ConfigGet(VmmOption.CORE_LEECHCORE_HANDLE) is not ulong pqwValue)
        {
            throw new VmmException("LeechCore: failed retrieving handle from Vmm.");
        }

        var strDevice = string.Format("existing://0x{0:X}", pqwValue);
        var cfg = new LCConfig
        {
            dwVersion = LC_CONFIG_VERSION,
            szDevice = strDevice
        };
        var hLC = Lci.LcCreate(ref cfg);
        if (hLC == IntPtr.Zero)
        {
            throw new VmmException("LeechCore: failed to create object.");
        }

        _h = hLC;
        _parent = vmm;
    }

    /// <summary>
    /// Releases native resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Implicitly convert a <see cref="LeechCore"/> instance to its native LC handle.
    /// </summary>
    /// <param name="x">Instance to convert.</param>
    /// <returns>The native LC handle or <see cref="IntPtr.Zero"/> if <paramref name="x"/> is <see langword="null"/>.</returns>
    public static implicit operator IntPtr(LeechCore x)
    {
        return x?._h ?? IntPtr.Zero;
    }

    /// <summary>
    /// Returns a string representation of this instance including the native handle value.
    /// </summary>
    public override string ToString()
    {
        return _h == IntPtr.Zero ? "LeechCore:NULL" : $"LeechCore:{_h.ToString("X")}";
    }

    /// <summary>
    /// Factory that creates a new <see cref="LeechCore"/> object from a native <see cref="LCConfig"/>.
    /// </summary>
    /// <remarks>
    /// This overload provides access to extended create-time error information via
    /// <paramref name="configErrorInfo"/>. See native <c>LcCreateEx</c> in <c>leechcore.h</c>.
    /// </remarks>
    /// <param name="pLcCreateConfig">The LC configuration to use.</param>
    /// <param name="configErrorInfo">Receives extended create-time error information, if available.</param>
    /// <returns>An initialized <see cref="LeechCore"/> instance on success; otherwise <see langword="null"/>.</returns>
    public static LeechCore Create(ref LCConfig pLcCreateConfig, out LCConfigErrorInfo configErrorInfo)
    {
        var cbERROR_INFO = Marshal.SizeOf<Lci.LC_CONFIG_ERRORINFO>();
        var hLC = Lci.LcCreateEx(ref pLcCreateConfig, out var pLcErrorInfo);
        configErrorInfo = new LCConfigErrorInfo
        {
            strUserText = ""
        };
        if (pLcErrorInfo != IntPtr.Zero && hLC != IntPtr.Zero)
        {
            return new LeechCore(hLC);
        }

        if (hLC != IntPtr.Zero)
        {
            Lci.LcClose(hLC);
        }

        if (pLcErrorInfo != IntPtr.Zero)
        {
            var e = Marshal.PtrToStructure<Lci.LC_CONFIG_ERRORINFO>(pLcErrorInfo);
            if (e.dwVersion == LC_CONFIG_ERRORINFO_VERSION)
            {
                configErrorInfo.fValid = true;
                configErrorInfo.fUserInputRequest = e.fUserInputRequest;
                if (e.cwszUserText > 0)
                {
                    configErrorInfo.strUserText = Marshal.PtrToStringUni((IntPtr)(pLcErrorInfo.ToInt64() + cbERROR_INFO));
                }
            }

            Lci.LcMemFree(pLcErrorInfo);
        }

        return null;
    }

    ~LeechCore()
    {
        Dispose(false);
    }

    private void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _h, IntPtr.Zero) is IntPtr h && h != IntPtr.Zero)
        {
            Lci.LcClose(h);
        }
    }

    //---------------------------------------------------------------------
    // LEECHCORE: GENERAL FUNCTIONALITY BELOW:
    //---------------------------------------------------------------------

    /// <summary>
    /// Read physical memory into a value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">An unmanaged value or <see langword="ref struct"/>.</typeparam>
    /// <param name="pa">Physical address to read.</param>
    /// <param name="result">Receives the value read from memory on success.</param>
    /// <returns><see langword="true"/> if successful; otherwise <see langword="false"/>.</returns>
    /// <seealso cref="Lci.LcRead(IntPtr, ulong, uint, byte*)"/>
    public unsafe bool ReadValue<T>(ulong pa, out T result)
        where T : unmanaged, allows ref struct
    {
        uint cb = (uint)sizeof(T);
		result = default;
        fixed (void* pb = &result)
        {
            return Lci.LcRead(_h, pa, cb, (byte*)pb);
        }
    }

    /// <summary>
    /// Read physical memory into a pooled array of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">An unmanaged value type.</typeparam>
    /// <param name="pa">Physical address to read.</param>
    /// <param name="count">Number of elements to read.</param>
    /// <returns>A <see cref="PooledMemory{T}"/> lease on success; otherwise <see langword="null"/>.</returns>
    public unsafe PooledMemory<T> ReadArray<T>(ulong pa, int count)
        where T : unmanaged
    {
        var arr = new PooledMemory<T>(count);
        uint cb = checked((uint)sizeof(T) * (uint)count);
        fixed (T* pb = arr.Span)
        {
            if (!Lci.LcRead(_h, pa, cb, (byte*)pb))
            {
                arr.Dispose();
                return null;
            }
        }
        return arr;
    }

    /// <summary>
    /// Read physical memory into a <see cref="Span{T}"/>.
    /// </summary>
    /// <typeparam name="T">An unmanaged value type.</typeparam>
    /// <param name="pa">Physical address to read.</param>
    /// <param name="span">Destination span to receive the data.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    public unsafe bool ReadSpan<T>(ulong pa, Span<T> span)
        where T : unmanaged
    {
		uint cb = checked((uint)sizeof(T) * (uint)span.Length);
		fixed (T* pb = span)
        {
            return Lci.LcRead(_h, pa, cb, (byte*)pb);
        }
    }

    /// <summary>
    /// Write a <see cref="Span{T}"/> of unmanaged values to physical memory.
    /// </summary>
    /// <typeparam name="T">An unmanaged value type.</typeparam>
    /// <param name="pa">Physical address to write.</param>
    /// <param name="span">Source span that will be written.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    public unsafe bool WriteSpan<T>(ulong pa, Span<T> span)
        where T : unmanaged
    {
        _parent?.ThrowIfMemWritesDisabled();
		uint cb = checked((uint)sizeof(T) * (uint)span.Length);
		fixed (T* pb = span)
        {
            return Lci.LcWrite(_h, pa, cb, (byte*)pb);
        }
    }

    /// <summary>
    /// Read physical memory into unmanaged memory.
    /// </summary>
    /// <param name="pa">Physical address to read.</param>
    /// <param name="pb">Destination pointer to receive the data.</param>
    /// <param name="cb">Number of bytes to read.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool Read(ulong pa, IntPtr pb, uint cb)
    {
        return Read(pa, pb.ToPointer(), cb);
    }

    /// <summary>
    /// Read physical memory into unmanaged memory.
    /// </summary>
    /// <param name="pa">Physical address to read.</param>
    /// <param name="pb">Destination pointer to receive the data.</param>
    /// <param name="cb">Number of bytes to read.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    public unsafe bool Read(ulong pa, void* pb, uint cb)
    {
        if (!Lci.LcRead(_h, pa, cb, (byte*)pb))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Perform a scatter read of multiple page-sized physical memory ranges.
    /// </summary>
    /// <remarks>
    /// This operation does not copy read pages into managed memory; instead, native buffers are exposed via
    /// <see cref="ScatterData.Data"/>. Ensure the returned <see cref="LcScatterHandle"/> is disposed before those buffers
    /// are accessed by other operations.
    /// </remarks>
    /// <param name="pas">Page-aligned physical memory addresses.</param>
    /// <returns>An <see cref="LcScatterHandle"/> that owns the native buffers.</returns>
    /// <exception cref="VmmException">Thrown if the native scatter allocation fails.</exception>
    public unsafe LcScatterHandle ReadScatter(params Span<ulong> pas)
    {
        if (!Lci.LcAllocScatter1((uint)pas.Length, out var pppMEMs))
        {
            throw new VmmException("LcAllocScatter1 FAIL");
        }

        var ppMEMs = (LcMemScatter**)pppMEMs.ToPointer();
        for (var i = 0; i < pas.Length; i++)
        {
            var pMEM = ppMEMs[i];
            pMEM->qwA = pas[i] & ~(ulong)0xfff;
        }

        Lci.LcReadScatter(_h, (uint)pas.Length, pppMEMs);

        var results = new PooledDictionary<ulong, ScatterData>(capacity: pas.Length);
        for (var i = 0; i < pas.Length; i++)
        {
            var pMEM = ppMEMs[i];
            if (pMEM->f)
            {
                results[pMEM->qwA] = new ScatterData(pMEM->pb, pMEM->cb);
            }
        }

        return new LcScatterHandle(results, pppMEMs);
    }

    /// <summary>
    /// Write a single value of type <typeparamref name="T"/> to physical memory.
    /// </summary>
    /// <typeparam name="T">An unmanaged value or <see langword="ref struct"/>.</typeparam>
    /// <param name="pa">Physical address to write.</param>
    /// <param name="value">The value to write.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    public unsafe bool WriteValue<T>(ulong pa, T value)
        where T : unmanaged, allows ref struct
    {
        _parent?.ThrowIfMemWritesDisabled();
        uint cb = (uint)sizeof(T);
		return Lci.LcWrite(_h, pa, cb, (byte*)&value);
    }

    /// <summary>
    /// Write a managed array of <typeparamref name="T"/> to physical memory.
    /// </summary>
    /// <typeparam name="T">An unmanaged value type.</typeparam>
    /// <param name="pa">Physical address to write.</param>
    /// <param name="data">The managed array to write.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    public unsafe bool WriteArray<T>(ulong pa, T[] data)
        where T : unmanaged
    {
        _parent?.ThrowIfMemWritesDisabled();
		uint cb = checked((uint)sizeof(T) * (uint)data.Length);
		fixed (T* pb = data)
        {
            return Lci.LcWrite(_h, pa, cb, (byte*)pb);
        }
    }

    /// <summary>
    /// Write from unmanaged memory into physical memory.
    /// </summary>
    /// <param name="pa">Physical address to write.</param>
    /// <param name="pb">Source pointer to write from.</param>
    /// <param name="cb">Number of bytes to write.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool Write(ulong pa, IntPtr pb, uint cb)
    {
        return Write(pa, pb.ToPointer(), cb);
    }

    /// <summary>
    /// Write from unmanaged memory into physical memory.
    /// </summary>
    /// <param name="pa">Physical address to write.</param>
    /// <param name="pb">Source pointer to write from.</param>
    /// <param name="cb">Number of bytes to write.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    public unsafe bool Write(ulong pa, void* pb, uint cb)
    {
        _parent?.ThrowIfMemWritesDisabled();
        return Lci.LcWrite(_h, pa, cb, (byte*)pb);
    }

    /// <summary>
    /// Retrieve a LeechCore option value via <see cref="Lci.GetOption(IntPtr, LcOption, out ulong)"/>.
    /// </summary>
    /// <param name="fOption">The <see cref="LcOption"/> to query.</param>
    /// <returns>The option value on success; otherwise <see langword="null"/>.</returns>
    public ulong? GetOption(LcOption fOption)
    {
        if (!Lci.GetOption(_h, fOption, out var pqwValue))
        {
            return null;
        }

        return pqwValue;
    }

    /// <summary>
    /// Set a LeechCore option value via <see cref="Lci.SetOption(IntPtr, LcOption, ulong)"/>.
    /// </summary>
    /// <param name="fOption">The <see cref="LcOption"/> to set.</param>
    /// <param name="qwValue">The value to assign.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    public bool SetOption(LcOption fOption, ulong qwValue)
    {
        return Lci.SetOption(_h, fOption, qwValue);
    }

    /// <summary>
    /// Send a command to LeechCore.
    /// </summary>
    /// <remarks>
    /// See native <c>LcCommand</c> in <c>leechcore.h</c>. The output buffer, if any, is owned by the caller and must be
    /// freed by the wrapper (handled internally).
    /// </remarks>
    /// <param name="fOption">The <see cref="LcCmd"/> to execute.</param>
    /// <param name="dataIn">Optional input data.</param>
    /// <param name="dataOut">Receives any output data returned by the command.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    public unsafe bool ExecuteCommand(LcCmd fOption, byte[] dataIn, out byte[] dataOut)
    {
        uint cbDataOut;
        IntPtr pbDataOut;
        dataOut = null;
        if (dataIn is null)
        {
            if (!Lci.LcCommand(_h, fOption, 0, null, out pbDataOut, out cbDataOut))
            {
                return false;
            }
        }
        else
        {
            fixed (byte* pbDataIn = dataIn)
            {
                if (!Lci.LcCommand(_h, fOption, (uint)dataIn.Length, pbDataIn, out pbDataOut, out cbDataOut))
                {
                    return false;
                }
            }
        }

        dataOut = new byte[cbDataOut];
        if (cbDataOut > 0)
        {
            var src = new ReadOnlySpan<byte>(pbDataOut.ToPointer(), checked((int)cbDataOut));
            src.CopyTo(dataOut);
            Lci.LcMemFree(pbDataOut);
        }

        return true;
    }

    /// <summary>
    /// Wraps native memory returned from a scatter read invocation.
    /// </summary>
    /// <remarks>
    /// The underlying native scatter page buffers are released via <see cref="Lci.LcMemFree(IntPtr)"/> when this handle
    /// is disposed.
    /// </remarks>
    public sealed class LcScatterHandle : IDisposable
    {
        private readonly PooledDictionary<ulong, ScatterData> _results;
        private IntPtr _mems;

        private LcScatterHandle() { }

        internal LcScatterHandle(PooledDictionary<ulong, ScatterData> results, IntPtr mems)
        {
            _results = results;
            _mems = mems;
        }

        /// <summary>
        /// Results of a scatter read.
        /// </summary>
        /// <remarks>
        /// Only successful page reads are present. Keys are page-aligned addresses; values are the corresponding page
        /// buffers.
        /// </remarks>
        public IReadOnlyDictionary<ulong, ScatterData> Results => _results;

        #region IDisposable

        /// <summary>
        /// Dispose and release any native scatter resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _mems, IntPtr.Zero) is IntPtr h && h != IntPtr.Zero)
            {
                if (disposing)
                {
                    _results.Dispose();
                }
                Lci.LcMemFree(h);
            }
        }

        ~LcScatterHandle() => Dispose(false);

        #endregion
    }

    /// <summary>
    /// Encapsulates native page data for a single scatter entry.
    /// </summary>
    public readonly struct ScatterData
    {
        private readonly IntPtr _pb;
        private readonly int _cb;

        internal ScatterData(IntPtr pb, uint cb)
        {
            _pb = pb;
            _cb = checked((int)cb); // fail if cb > int.MaxValue
        }

        /// <summary>
        /// Read-only view over the native page buffer.
        /// </summary>
        /// <remarks>
        /// Do not access this memory after the owning <see cref="LcScatterHandle"/> has been disposed.
        /// </remarks>
        public readonly unsafe ReadOnlySpan<byte> Data =>
            new(_pb.ToPointer(), _cb);
    }

    #region Constants/Types

    //---------------------------------------------------------------------
    // LEECHCORE: CORE FUNCTIONALITY BELOW:
    //---------------------------------------------------------------------

    /// <summary>
    /// Current <see cref="LCConfig"/> structure version used by this wrapper.
    /// </summary>
    public const uint LC_CONFIG_VERSION = 0xc0fd0002;

    /// <summary>
    /// Current <see cref="LCConfigErrorInfo"/> structure version used by this wrapper.
    /// </summary>
    public const uint LC_CONFIG_ERRORINFO_VERSION = 0xc0fe0002;

    /// <summary>
    /// Native scatter descriptor mirroring <c>tdMEM_SCATTER</c> in <c>leechcore.h</c>.
    /// </summary>
    /// <remarks>
    /// This type is laid out for blittable interop. Only the documented public fields are intended for use.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct LcMemScatter
    {
        /// <summary>
        /// MEM_SCATTER_VERSION (internal).
        /// </summary>
        private readonly uint version;
        private readonly int _f; // WIN32 BOOL
        /// <summary>
        /// Indicates whether the entry contains valid data (<see langword="true"/>) or not.
        /// </summary>
        public readonly bool f => _f != 0;
        /// <summary>
        /// Page-aligned address associated with this scatter entry.
        /// </summary>
        public ulong qwA;
        /// <summary>
        /// Pointer to the native buffer holding the page data.
        /// </summary>
        public readonly IntPtr pb;
        /// <summary>
        /// Size of the native page buffer in bytes.
        /// </summary>
        public readonly uint cb;
        /// <summary>
        /// Internal stack pointer (reserved).
        /// </summary>
        private readonly uint iStack;
        /// <summary>
        /// Internal stack storage (reserved).
        /// </summary>
        private unsafe fixed ulong vStack[12];

        /// <summary>
        /// A read-only view over the page contents pointed at by <see cref="pb"/>.
        /// </summary>
        /// <remarks>
        /// DANGER: Do not access this memory after the memory is freed via <see cref="Lci.LcMemFree(IntPtr)"/>.
        /// </remarks>
        public readonly unsafe ReadOnlySpan<byte> Data =>
            new ReadOnlySpan<byte>(pb.ToPointer(), checked((int)cb));
    }

    /// <summary>
    /// Managed representation of native <c>LC_CONFIG</c> used when creating a LeechCore context.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct LCConfig
    {
        /// <summary>
        /// Structure version. Must be set to <see cref="LC_CONFIG_VERSION"/>.
        /// </summary>
        public uint dwVersion;
        /// <summary>
        /// Printf verbosity level.
        /// </summary>
        public uint dwPrintfVerbosity;

        /// <summary>
        /// Device string, e.g. <c>fpga://...</c> or <c>existing://0xHANDLE</c>.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDevice;

        /// <summary>
        /// Remote target string, if applicable.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szRemote;

        /// <summary>
        /// Optional printf callback.
        /// </summary>
        public IntPtr pfn_printf_opt;
        /// <summary>
        /// Maximum physical address to use.
        /// </summary>
        public ulong paMax;
        /// <summary>
        /// If <see langword="true"/>, volatile mode is enabled.
        /// </summary>
        public bool fVolatile;
        /// <summary>
        /// If <see langword="true"/>, writes are allowed.
        /// </summary>
        public bool fWritable;
        /// <summary>
        /// If <see langword="true"/>, operates in remote mode.
        /// </summary>
        public bool fRemote;
        /// <summary>
        /// If <see langword="true"/>, disables compression in remote mode.
        /// </summary>
        public bool fRemoteDisableCompress;

        /// <summary>
        /// Optional device name.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDeviceName;
    }

    /// <summary>
    /// Extended create-time error information corresponding to native <c>LC_CONFIG_ERRORINFO</c>.
    /// </summary>
    public struct LCConfigErrorInfo
    {
        /// <summary>
        /// Indicates whether this structure contains valid data.
        /// </summary>
        public bool fValid;
        /// <summary>
        /// Indicates a user-input request was signalled by the native layer.
        /// </summary>
        public bool fUserInputRequest;
        /// <summary>
        /// Optional user text provided by the native layer.
        /// </summary>
        public string strUserText;
    }

    #endregion
}