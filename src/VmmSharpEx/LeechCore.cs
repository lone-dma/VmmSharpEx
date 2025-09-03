using Collections.Pooled;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VmmSharpEx.Internal;
using VmmSharpEx.Options;

namespace VmmSharpEx;

/// <summary>
/// LeechCore public API
/// </summary>
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
    /// Create a new inherited LeechCore instance from a given Vmm instance.
    /// </summary>
    /// <param name="vmm"></param>
    /// <exception cref="VmmException"></exception>
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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public static implicit operator IntPtr(LeechCore x)
    {
        return x?._h ?? IntPtr.Zero;
    }

    /// <summary>
    /// ToString() override.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return _h == IntPtr.Zero ? "LeechCore:NULL" : $"LeechCore:{_h.ToString("X")}";
    }

    /// <summary>
    /// Factory method creating a new LeechCore object taking a LC_CONFIG structure
    /// containing the configuration and optionally return a LC_CONFIG_ERRORINFO
    /// structure containing any error.
    /// Use this when you wish to gain greater control of creating LeechCore objects.
    /// </summary>
    /// <param name="pLcCreateConfig"></param>
    /// <param name="configErrorInfo"></param>
    /// <returns>Initialized LeechCore Instance.</returns>
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
            if (_parent is null)
            {
                Lci.LcClose(h);
            }
        }
    }

    //---------------------------------------------------------------------
    // LEECHCORE: GENERAL FUNCTIONALITY BELOW:
    //---------------------------------------------------------------------

    /// <summary>
    /// Read physcial memory into a nullable struct value <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">Value Type.</typeparam>
    /// <param name="pa">Physical address to read.</param>
    /// <param name="result">Result of the memory read.</param>
    /// <returns>TRUE if successful, otherwise FALSE.</returns>
    public unsafe bool ReadValue<T>(ulong pa, out T result)
        where T : unmanaged, allows ref struct
    {
        var cb = (uint)sizeof(T);
        result = default;
        fixed (void* pb = &result)
        {
            return Lci.LcRead(_h, pa, cb, (byte*)pb);
        }
    }

    /// <summary>
    /// Read physical memory into an array of type <typeparamref name="T" />.
    /// WARNING: This incurs a heap allocation for the array. Recommend using <see cref="ReadPooledArray{T}(ulong, int)"/> instead.
    /// </summary>
    /// <typeparam name="T">Value Type.</typeparam>
    /// <param name="pa">Physical address to read.</param>
    /// <param name="count">Number of elements to read.</param>
    /// <returns>Managed Array of type <typeparamref name="T" />. Null if read failed.</returns>
    public unsafe T[] ReadArray<T>(ulong pa, int count)
        where T : unmanaged
    {
        var cb = (uint)(sizeof(T) * count);
        var data = new T[count];
        fixed (T* pb = data)
        {
            if (!Lci.LcRead(_h, pa, cb, (byte*)pb))
                return null;
        }
        return data;
    }

    /// <summary>
    /// Read physical memory into a pooled array of type <typeparamref name="T" />.
    /// NOTE: You must dispose the returned <see cref="IMemoryOwner{T}"/> when finished with it.
    /// </summary>
    /// <typeparam name="T">Value Type.</typeparam>
    /// <param name="pa">Physical address to read.</param>
    /// <param name="count">Number of elements to read.</param>
    /// <returns><see cref="IMemoryOwner{T}"/> lease, NULL if failed.</returns>
    public unsafe IMemoryOwner<T> ReadPooledArray<T>(ulong pa, int count)
        where T : unmanaged
    {
        var owner = new PooledArray<T>(count);
        var cb = (uint)(sizeof(T) * count);
        fixed (T* pb = owner.Memory.Span)
        {
            if (!Lci.LcRead(_h, pa, cb, (byte*)pb))
            {
                owner.Dispose();
                return null;
            }
        }
        return owner;
    }

    /// <summary>
    /// Read memory into a Span of <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">Value Type</typeparam>
    /// <param name="pa">Memory address to read from.</param>
    /// <param name="span">Span to receive the memory read.</param>
    /// <returns>True if successful, otherwise False.</returns>
    public unsafe bool ReadSpan<T>(ulong pa, Span<T> span)
        where T : unmanaged
    {
        var cb = (uint)(sizeof(T) * span.Length);
        fixed (T* pb = span)
        {
            return Lci.LcRead(_h, pa, cb, (byte*)pb);
        }
    }

    /// <summary>
    /// Write memory from a Span of <typeparamref name="T" /> to a specified memory address.
    /// </summary>
    /// <typeparam name="T">Value Type</typeparam>
    /// <param name="pa">Memory address to write to.</param>
    /// <param name="span">Span to write from.</param>
    /// <returns>True if successful, otherwise False.</returns>
    public unsafe bool WriteSpan<T>(ulong pa, Span<T> span)
        where T : unmanaged
    {
        _parent?.ThrowIfMemWritesDisabled();
        var cb = (uint)(sizeof(T) * span.Length);
        fixed (T* pb = span)
        {
            return Lci.LcWrite(_h, pa, cb, (byte*)pb);
        }
    }

    /// <summary>
    /// Read physical memory into unmanaged memory.
    /// </summary>
    /// <param name="pa">Physical address to read.</param>
    /// <param name="pb">Pointer to buffer to read into.</param>
    /// <param name="cb">Counte of bytes to read.</param>
    /// <returns>True if read successful, otherwise False.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool Read(ulong pa, IntPtr pb, uint cb)
    {
        return Read(pa, pb.ToPointer(), cb);
    }

    /// <summary>
    /// Read physical memory into unmanaged memory.
    /// </summary>
    /// <param name="pa">Physical address to read.</param>
    /// <param name="pb">Pointer to buffer to read into.</param>
    /// <param name="cb">Counte of bytes to read.</param>
    /// <returns>True if read successful, otherwise False.</returns>
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
    /// Does not copy the read memory to a managed byte buffer, but instead allows direct access to the native memory via a
    /// Span view.
    /// </summary>
    /// <param name="pas">Array of page-aligned Physical Memory Addresses.</param>
    /// <returns>SCATTER_HANDLE</returns>
    /// <exception cref="VmmException"></exception>
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
        var results = new PooledDictionary<ulong, ScatterData>(capacity: pas.Length);
        Lci.LcReadScatter(_h, (uint)pas.Length, pppMEMs);
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
    /// Write a single struct <typeparamref name="T" /> into physical memory.
    /// </summary>
    /// <typeparam name="T">Value Type.</typeparam>
    /// <param name="pa">Physical address to write</param>
    /// <param name="value"><typeparamref name="T" /> value to write.</param>
    /// <returns>
    /// True if write successful, otherwise False. The write is best-effort and may fail. It's recommended to verify
    /// the write with a subsequent read.
    /// </returns>
    public unsafe bool WriteValue<T>(ulong pa, T value)
        where T : unmanaged, allows ref struct
    {
        _parent?.ThrowIfMemWritesDisabled();
        var cb = (uint)sizeof(T);
        return Lci.LcWrite(_h, pa, cb, (byte*)&value);
    }

    /// <summary>
    /// Write a managed <typeparamref name="T" /> array into physical memory.
    /// </summary>
    /// <typeparam name="T">Value Type.</typeparam>
    /// <param name="pa">Physical address to write</param>
    /// <param name="data">Managed <typeparamref name="T" /> array to write.</param>
    /// <returns>
    /// True if write successful, otherwise False. The write is best-effort and may fail. It's recommended to verify
    /// the write with a subsequent read.
    /// </returns>
    public unsafe bool WriteArray<T>(ulong pa, T[] data)
        where T : unmanaged
    {
        _parent?.ThrowIfMemWritesDisabled();
        var cb = (uint)sizeof(T) * (uint)data.Length;
        fixed (T* pb = data)
        {
            return Lci.LcWrite(_h, pa, cb, (byte*)pb);
        }
    }

    /// <summary>
    /// Write from unmanaged memory into physical memory.
    /// </summary>
    /// <param name="pa">Physical address to write</param>
    /// <param name="pb">Pointer to buffer to write from.</param>
    /// <param name="cb">Count of bytes to write.</param>
    /// <returns>
    /// True if write successful, otherwise False. The write is best-effort and may fail. It's recommended to verify
    /// the write with a subsequent read.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool Write(ulong pa, IntPtr pb, uint cb)
    {
        return Write(pa, pb.ToPointer(), cb);
    }

    /// <summary>
    /// Write from unmanaged memory into physical memory.
    /// </summary>
    /// <param name="pa">Physical address to write</param>
    /// <param name="pb">Pointer to buffer to write from.</param>
    /// <param name="cb">Count of bytes to write.</param>
    /// <returns>
    /// True if write successful, otherwise False. The write is best-effort and may fail. It's recommended to verify
    /// the write with a subsequent read.
    /// </returns>
    public unsafe bool Write(ulong pa, void* pb, uint cb)
    {
        _parent?.ThrowIfMemWritesDisabled();
        return Lci.LcWrite(_h, pa, cb, (byte*)pb);
    }

    /// <summary>
    /// Retrieve a LeechCore option value.
    /// </summary>
    /// <param name="fOption">Parameter LeechCore.LC_OPT_*</param>
    /// <returns>The option value retrieved. NULL on fail.</returns>
    public ulong? GetOption(LcOption fOption)
    {
        if (!Lci.GetOption(_h, fOption, out var pqwValue))
        {
            return null;
        }

        return pqwValue;
    }

    /// <summary>
    /// Set a LeechCore option value.
    /// </summary>
    /// <param name="fOption">Parameter LeechCore.LC_OPT_*</param>
    /// <param name="qwValue">The option value to set.</param>
    /// <returns></returns>
    public bool SetOption(LcOption fOption, ulong qwValue)
    {
        return Lci.SetOption(_h, fOption, qwValue);
    }

    /// <summary>
    /// Send a command to LeechCore.
    /// </summary>
    /// <param name="fOption">Parameter LeechCore.LC_CMD_*</param>
    /// <param name="dataIn">The data to set (or null).</param>
    /// <param name="dataOut">The data retrieved.</param>
    /// <returns></returns>
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
            var src = new ReadOnlySpan<byte>(pbDataOut.ToPointer(), (int)cbDataOut);
            src.CopyTo(dataOut);
            Lci.LcMemFree(pbDataOut);
        }

        return true;
    }

    /// <summary>
    /// Wraps native memory from a Scatter Read.
    /// Calls LcMemFree on disposal.
    /// </summary>
    public sealed class LcScatterHandle : IDisposable
    {
        private readonly PooledDictionary<ulong, ScatterData> _results;
        private IntPtr _mems;

        public LcScatterHandle(PooledDictionary<ulong, ScatterData> results, IntPtr mems)
        {
            _results = results;
            _mems = mems;
        }

        /// <summary>
        /// Scatter Read Results. Only successful reads are contained in this Dictionary. If a read failed, it will not be
        /// present.
        /// KEY: Page-aligned Memory Address.
        /// VALUE: SCATTER_PAGE containing the page data.
        /// </summary>
        public IReadOnlyDictionary<ulong, ScatterData> Results => _results;

        #region IDisposable

        /// <summary>
        /// Calls LcMemFree on native memory resources.
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
    /// Encapsulates native data from a scatter read entry.
    /// </summary>
    public readonly struct ScatterData
    {
        private readonly IntPtr _pb;
        private readonly int _cb;

        public ScatterData(IntPtr pb, uint cb)
        {
            _pb = pb;
            _cb = (int)cb;
        }

        /// <summary>
        /// Page for this scatter read entry.
        /// WARNING: Do not access this memory after the parent scope is disposed/freed!
        /// </summary>
        public readonly unsafe ReadOnlySpan<byte> Data =>
            new(_pb.ToPointer(), _cb);
    }

    #region Constants/Types

    //---------------------------------------------------------------------
    // LEECHCORE: CORE FUNCTIONALITY BELOW:
    //---------------------------------------------------------------------

    public const uint LC_CONFIG_VERSION = 0xc0fd0002;
    public const uint LC_CONFIG_ERRORINFO_VERSION = 0xc0fe0002;

    /// <summary>
    /// From tdMEM_SCATTER in leechcore.h
    /// Designed to be blittable for direct pointer access.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct LcMemScatter
    {
        /// <summary>
        /// MEM_SCATTER_VERSION
        /// </summary>
        private readonly uint version;
        private readonly int _f; // WIN32 BOOL
        /// <summary>
        /// TRUE = success data in pb, FALSE = fail or not yet read.
        /// </summary>
        public readonly bool f => _f != 0;
        /// <summary>
        /// address of memory to read
        /// </summary>
        public ulong qwA;
        /// <summary>
        /// buffer to hold memory contents
        /// </summary>
        public readonly IntPtr pb;
        /// <summary>
        /// size of buffer to hold memory contents.
        /// </summary>
        public readonly uint cb;
        /// <summary>
        /// internal stack pointer
        /// </summary>
        private readonly uint iStack;
        /// <summary>
        /// internal stack
        /// </summary>
        private unsafe fixed ulong vStack[12];

        /// <summary>
        /// Contains the read data from the <see cref="pb"/> buffer.
        /// DANGER: Do not access this memory after the memory is freed via <see cref="Lci.LcMemFree(nint)"/>!
        /// </summary>
        public readonly unsafe ReadOnlySpan<byte> Data =>
            new ReadOnlySpan<byte>(pb.ToPointer(), (int)cb);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct LCConfig
    {
        public uint dwVersion;
        public uint dwPrintfVerbosity;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDevice;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szRemote;

        public IntPtr pfn_printf_opt;
        public ulong paMax;
        public bool fVolatile;
        public bool fWritable;
        public bool fRemote;
        public bool fRemoteDisableCompress;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDeviceName;
    }

    public struct LCConfigErrorInfo
    {
        public bool fValid;
        public bool fUserInputRequest;
        public string strUserText;
    }

    #endregion
}