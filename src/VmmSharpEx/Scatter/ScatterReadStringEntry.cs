// Original Credit to lone-dma

using Microsoft.Extensions.ObjectPool;
using System.Runtime.CompilerServices;
using System.Text;

namespace VmmSharpEx.Scatter
{
    internal sealed class ScatterReadStringEntry : IScatterEntry
    {
        private static readonly ObjectPool<ScatterReadStringEntry> _pool = 
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

        [Obsolete("For internal use only. Construct a ScatterReadMap to begin using this API.")]
        public ScatterReadStringEntry() { }

        /// <summary>
        /// Rent from the Object Pool.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ScatterReadStringEntry Rent() => _pool.Get();

        /// <summary>
        /// Parse the memory buffer and set the result value.
        /// Only called internally via API.
        /// </summary>
        /// <param name="hScatter">Scatter read handle.</param>
        public void SetResult(LeechCore.LcScatterHandle hScatter)
        {
            try
            {
                Span<byte> bytes = CB <= 256 ? stackalloc byte[CB] : new byte[CB];
                if (!IScatterEntry.ProcessData<byte>(hScatter, Address, bytes))
                {
                    IsFailed = true;
                }
                else
                {
                    var str = _encoding.GetString(bytes);
                    int nt = str.IndexOf('\0');
                    _result = nt != -1 ?
                        str.Substring(0, nt) : str;
                }
            }
            catch
            {
                IsFailed = true;
            }
        }

        internal void Configure(ulong address, int cb, Encoding encoding)
        {
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
            _pool.Return(this);
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
