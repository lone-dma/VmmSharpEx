// Original Credit to lone-dma

using System.Text;

namespace VmmSharpEx.Scatter
{
    public sealed class ScatterReadStringEntry : IScatterEntry
    {
        private readonly Encoding _encoding;
        private string _result;
        /// <summary>
        /// Result for this read. Be sure to check <see cref="IsFailed"/>
        /// </summary>
        internal string Result => _result;
        /// <summary>
        /// Virtual Address to read from.
        /// </summary>
        public ulong Address { get; }
        /// <summary>
        /// Count of bytes to read.
        /// </summary>
        public int CB { get; }
        /// <summary>
        /// True if this read has failed, otherwise False.
        /// </summary>
        public bool IsFailed { get; set; }

        internal ScatterReadStringEntry(ulong address, int cb, Encoding encoding) 
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cb, 0, nameof(cb));
            ArgumentNullException.ThrowIfNull(encoding, nameof(encoding));
            Address = address;
            CB = cb;
            _encoding = encoding;
        }

        /// <summary>
        /// Parse the memory buffer and set the result value.
        /// Only called internally via API.
        /// </summary>
        /// <param name="hScatter">Scatter read handle.</param>
        public void SetResult(LeechCore.LcScatterHandle hScatter)
        {
            try
            {
                var bytes = new byte[CB];
                if (!IScatterEntry.ProcessData<byte>(hScatter, Address, CB, bytes))
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
    }
}
