// Original Credit to lone-dma

using Microsoft.Extensions.ObjectPool;
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
        internal string Result => _result;
        public ulong Address { get; private set; }
        public int CB { get; private set; }
        public bool IsFailed { get; set; }

        public ScatterReadStringEntry() { }

        internal static ScatterReadStringEntry Create(ulong address, int cb, Encoding encoding)
        {
            ArgumentNullException.ThrowIfNull(encoding, nameof(encoding));
            var rented = _pool.Get();
            rented.Address = address;
            rented.CB = cb;
            rented._encoding = encoding;
            return rented;
        }

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

        public void Return()
        {
            _pool.Return(this);
        }

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
