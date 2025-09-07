using Collections.Pooled;
using Microsoft.Extensions.ObjectPool;

namespace VmmSharpEx.Scatter
{
    /// <summary>
    /// Read multiple virtual addresses from a Windows x64 process using a custom scatter read implementation.
    /// Results are populated in the IScatterEntry objects, and can be used to chain reads.
    /// NOTE: This API is NOT thread safe, you must keep operations synchronous.
    /// </summary>
    public sealed class ScatterReadMap : IDisposable
    {
        /// <summary>
        /// Maximum read size in bytes for any single entry.
        /// DEFAULT: No Limit (<see cref="int.MaxValue"/>)
        /// </summary>
        public static int MaxReadSize { get; set; } = int.MaxValue; // Buffer Spans/Arrays are limited to int.MaxValue anyway.

        private readonly Vmm _vmm;
        private readonly uint _pid;
        private readonly PooledList<ScatterReadRound> _rounds = new(capacity: 12);
        private bool _disposed;

        /// <summary>
        /// Event is fired after the completion of all reads/rounds.
        /// </summary>
        public event EventHandler Completed;
        private void OnCompleted() => Completed?.Invoke(this, EventArgs.Empty);

        /// <summary>
        /// Read multiple virtual addresses from a Windows x64 process using a custom scatter read implementation.
        /// Results are populated in the IScatterEntry objects, and can be used to chain reads.
        /// NOTE: This API is NOT thread safe, you must keep operations synchronous.
        /// </summary>
        /// <param name="vmm">Vmm instance to read within.</param>
        /// <param name="pid">Process ID (PID) to read within.</param>
        public ScatterReadMap(Vmm vmm, uint pid)
        {
            _vmm = vmm;
            _pid = pid;
        }

        /// <summary>
        /// Executes Scatter Read operation as defined per the map.
        /// </summary>
        public void Execute()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_rounds.Count == 0)
            {
                return;
            }
            foreach (var round in _rounds)
            {
                round.Execute(_vmm, _pid);
            }
            OnCompleted();
        }

        /// <summary>
        /// Add scatter read rounds to the operation. Each round is a successive scatter read, you may need multiple
        /// rounds if you have reads dependent on earlier scatter reads result(s).
        /// </summary>
        /// <returns>ScatterReadRound object.</returns>
        public ScatterReadRound AddRound(bool useCache = true)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var round = ScatterReadRound.Create(useCache);
            _rounds.Add(round);
            return round;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, true) == false)
            {
                foreach (var round in _rounds)
                {
                    round.Return();
                }
                _rounds.Dispose();
            }
        }
    }
}