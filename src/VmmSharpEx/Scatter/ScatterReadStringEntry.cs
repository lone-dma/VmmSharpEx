/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using Microsoft.Extensions.ObjectPool;
using System.Buffers;
using System.Text;
using VmmSharpEx.Pools;

namespace VmmSharpEx.Scatter
{
    internal sealed class ScatterReadStringEntry : IScatterEntry
    {
        private static readonly ObjectPool<ScatterReadStringEntry> _pool = VmmPoolManager.ObjectPoolProvider
            .Create<ScatterReadStringEntry>();

        private Encoding _encoding;
        internal string _result;
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
            byte[] rentedBytes = null;
            char[] rentedChars = null;
            try
            {
                Span<byte> bytesSource = CB <= 256 ? 
                    stackalloc byte[CB] : (rentedBytes = ArrayPool<byte>.Shared.Rent(CB));
                var data = bytesSource.Slice(0, CB); // Rented Pool can have more than cb

                if (!IScatterEntry.ProcessData<byte>(hScatter, Address, data))
                {
                    IsFailed = true;
                }
                else
                {
                    int charCount = _encoding.GetCharCount(data);
                    Span<char> charsSource = charCount <= 128 ? 
                        stackalloc char[charCount] : (rentedChars = ArrayPool<char>.Shared.Rent(charCount));
                    var chars = charsSource.Slice(0, charCount);
                    _encoding.GetChars(data, chars);
                    int nt = chars.IndexOf('\0');
                    _result = nt != -1 ?
                        chars.Slice(0, nt).ToString() : chars.ToString(); // Only one string allocation
                }
            }
            catch
            {
                IsFailed = true;
            }
            finally
            {
                if (rentedBytes is not null)
                    ArrayPool<byte>.Shared.Return(rentedBytes);
                if (rentedChars is not null)
                    ArrayPool<char>.Shared.Return(rentedChars);
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
