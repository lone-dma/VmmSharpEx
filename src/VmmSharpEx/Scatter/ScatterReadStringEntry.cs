// Original Credit to lone-dma

using Microsoft.Extensions.ObjectPool;
using System.Buffers;
using System.Text;

namespace VmmSharpEx.Scatter
{
    public sealed class ScatterReadStringEntry : IScatterEntry
    {
        /// <summary>
        /// Object Pool for <see cref="ScatterReadStringEntry"/>"/>
        /// </summary>
        internal static ObjectPool<ScatterReadStringEntry> Pool { get; } = 
            new DefaultObjectPoolProvider() { MaximumRetained = int.MaxValue - 1 }
            .Create<ScatterReadStringEntry>();
        private Encoding _encoding;
        private string _result;
        /// <summary>
        /// Result for this read. Be sure to check <see cref="IsFailed"/>
        /// </summary>
        internal string Result => _result;
        /// <summary>
        /// Virtual Address to read from.
        /// </summary>
        public ulong Address { get; private set; }
        /// <summary>
        /// Count of bytes to read.
        /// </summary>
        public int CB { get; private set; }
        /// <summary>
        /// True if this read has failed, otherwise False.
        /// </summary>
        public bool IsFailed { get; set; }

        public ScatterReadStringEntry() { }

        /// <summary>
        /// Parse the memory buffer and set the result value.
        /// Only called internally via API.
        /// </summary>
        /// <param name="hScatter">Scatter read handle.</param>
        public void SetResult(LeechCore.LcScatterHandle hScatter)
        {
            try
            {
                var rent = ArrayPool<byte>.Shared.Rent(CB);
                try
                {
                    if (!IScatterEntry.ProcessData<byte>(hScatter, Address, CB, rent.AsSpan(0, CB)))
                    {
                        IsFailed = true;
                    }
                    else
                    {
                        var str = _encoding.GetString(rent);
                        int nt = str.IndexOf('\0');
                        _result = nt != -1 ?
                            str.Substring(0, nt) : str;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rent);
                }
            }
            catch
            {
                IsFailed = true;
            }
        }

        internal void Configure(ulong address, int cb, Encoding encoding)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cb, 0, nameof(cb));
            ArgumentNullException.ThrowIfNull(encoding, nameof(encoding));
            Address = address;
            CB = cb;
            _encoding = encoding;
        }

        /// <summary>
        /// Internal Only - DO NOT CALL
        /// </summary>
        public void Return()
        {
            Pool.Return(this);
        }

        /// <summary>
        /// Internal Only - DO NOT CALL
        /// </summary>
        public bool TryReset()
        {
            _encoding = default;
            _result = default;
            Address = default;
            CB = default;
            IsFailed = default;
            return true;
        }
    }
}
