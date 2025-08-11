using System.Runtime.CompilerServices;
using System.Text;
using VmmSharpEx.Internal;

namespace VmmSharpEx
{
    /// <summary>
    /// The VmmScatterMemory class is used to ease the reading and writing of memory in bulk using the VMM Scatter API.
    /// </summary>
    public sealed class VmmScatter : IDisposable
    {
        #region Base Functionality

        private readonly uint _pid;
        private IntPtr _h;

        private VmmScatter()
        {
            ;
        }

        internal VmmScatter(IntPtr hS, uint pid)
        {
            _h = hS;
            _pid = pid;
        }

        ~VmmScatter() => Dispose(disposing: false);

        public void Dispose()
        {
            Dispose(disposing: true);
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
                return "VmmScatterMemory:NotValid";
            }
            else if (_pid == 0xFFFFFFFF)
            {
                return "VmmScatterMemory:physical";
            }
            else
            {
                return $"VmmScatterMemory:virtual:{_pid}";
            }
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
        /// Prepare to read memory from an array/collection of structs.
        /// </summary>
        /// <param name="qwA">Address of the memory to be read.</param>
        /// <param name="count">Number of elements to be read.</param>
        /// <returns>true/false.</returns>
        public unsafe bool PrepareReadCollection<T>(ulong qwA, int count)
            where T : unmanaged
        {
            uint cb = (uint)sizeof(T) * (uint)count;
            return Vmmi.VMMDLL_Scatter_Prepare(_h, qwA, cb);
        }

        /// <summary>
        /// Prepare to write bytes to memory.
        /// </summary>
        /// <param name="qwA">The address where to write the data.</param>
        /// <param name="data">The data to write to memory.</param>
        /// <returns>true/false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool PrepareWrite(ulong qwA, byte[] data) =>
            PrepareWriteArray<byte>(qwA, data);

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
            uint cb = (uint)sizeof(T) * (uint)data.Length;
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
            uint cb = (uint)sizeof(T) * (uint)data.Length;
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
        /// Read memory bytes from an address.
        /// </summary>
        /// <param name="qwA">Address to read from.</param>
        /// <param name="cb">Bytes to read.</param>
        /// <returns>The byte array on success, Null on fail.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte[] Read(ulong qwA, uint cb) =>
            ReadArray<byte>(qwA, cb);

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
            uint cbRead;
            result = default;
            fixed (T* pb = &result)
            {
                if (!Vmmi.VMMDLL_Scatter_Read(_h, qwA, cb, (byte*)pb, out cbRead))
                    return false;
            }
            if (cbRead != cb)
                return false;
            return true;
        }

        /// <summary>
        /// Read memory from an address into an array of a certain type.
        /// </summary>
        /// <typeparam name="T">The type of struct to read.</typeparam>
        /// <param name="qwA">Address to read from.</param>
        /// <param name="count">The number of array items to read.</param>
        /// <returns>Array of objects read. Null on fail.</returns>
        public unsafe T[] ReadArray<T>(ulong qwA, uint count)
            where T : unmanaged
        {
            uint cb = (uint)sizeof(T) * count;
            uint cbRead;
            T[] data = new T[count];
            fixed (T* pb = data)
            {
                if (!Vmmi.VMMDLL_Scatter_Read(_h, qwA, cb, (byte*)pb, out cbRead))
                {
                    return null;
                }
            }
            if (cbRead != cb)
            {
                int partialCount = (int)cbRead / sizeof(T);
                Array.Resize<T>(ref data, partialCount);
            }
            return data;
        }

        /// <summary>
        /// Read memory from an address into a Span of a certain type.
        /// </summary>
        /// <typeparam name="T">The type of struct to read.</typeparam>
        /// <param name="qwA">Address to read from.</param>
        /// <param name="span">The span to read into.</param>
        /// <param name="cbRead">The number of bytes read.</param>
        /// <returns>TRUE if successful, otherwise FALSE.</returns>
        public unsafe bool ReadSpan<T>(ulong qwA, Span<T> span, out uint cbRead)
            where T : unmanaged
        {
            uint cb = (uint)sizeof(T) * (uint)span.Length;
            fixed (T* pb = span)
            {
                return Vmmi.VMMDLL_Scatter_Read(_h, qwA, cb, (byte*)pb, out cbRead);
            }
        }

        /// <summary>
        /// Read memory from an address into a managed string.
        /// </summary>
        /// <param name="encoding">String Encoding for this read.</param>
        /// <param name="qwA">Address to read from.</param>
        /// <param name="cb">Number of bytes to read. Keep in mind some string encodings are 2-4 bytes per character.</param>
        /// <param name="terminateOnNullChar">Terminate the string at the first occurrence of the null character.</param>
        /// <returns>C# Managed System.String. Null if failed.</returns>
        public unsafe string ReadString(Encoding encoding, ulong qwA, uint cb, bool terminateOnNullChar = true)
        {
            byte[] buffer = Read(qwA, cb);
            if (buffer is null)
                return null;
            var result = encoding.GetString(buffer);
            if (terminateOnNullChar)
            {
                int nullIndex = result.IndexOf('\0');
                if (nullIndex != -1)
                    result = result.Substring(0, nullIndex);
            }
            return result;
        }

        /// <summary>
        /// Clear the VmmScatter object to allow for new operations.
        /// </summary>
        /// <param name="flags"></param>
        /// <returns>true/false.</returns>
        public bool Clear(uint flags)
        {
            return Vmmi.VMMDLL_Scatter_Clear(_h, _pid, flags);
        }

#endregion
    }
}
