/*  
 *  VmmSharpEx by Lone (Lone DMA)
 *  Copyright (C) 2025 AGPL-3.0
*/

using Collections.Pooled;
using VmmSharpEx.Options;

namespace VmmSharpEx.Scatter
{
    /// <summary>
    /// Convenience mapping API that allows for multiple 'rounds' of <see cref="VmmScatterSlim"/> operations to be executed in sequence.
    /// </summary>
    public sealed class VmmScatterMap<T> : IDisposable
        where T : IScatter<T>
    {
        private readonly Lock _sync = new();
        private readonly PooledList<IScatter> _rounds = new(capacity: 16);
        private readonly Vmm _vmm;
        private readonly uint _pid;
        private bool _disposed;

        /// <summary>
        /// Event is fired upon completion of <see cref="Execute"/>.
        /// </summary>
        public event EventHandler? Completed;
        private void OnCompleted() => Completed?.Invoke(this, EventArgs.Empty);

        private VmmScatterMap() { throw new NotImplementedException(); }

        public VmmScatterMap(Vmm vmm, uint pid)
        {
            _vmm = vmm;
            _pid = pid;
        }

        /// <summary>
        /// Add a new scatter round to this map. Rounds will be executed in the order they are added when <see cref="Execute"/> is called.
        /// </summary>
        /// <param name="flags">Vmm Flag Options for this operation.</param>
        /// <returns>Object reference to the added <see cref="VmmScatterSlim"/> instance.</returns>
        /// <exception cref="VmmException"></exception>
        public T AddRound(VmmFlags flags = VmmFlags.NONE)
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                var round = T.Create(_vmm, _pid, flags);
                _rounds.Add(round);
                return round;
            }
        }

        /// <summary>
        /// Executes all of the contained Scatter Read 'Rounds' that have been added via <see cref="AddRound(VmmFlags)"/>.
        /// </summary>
        /// <remarks>
        /// If no rounds have been added, this method is a no-op.
        /// </remarks>
        /// <exception cref="VmmException"></exception>
        public void Execute()
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_rounds.Count == 0)
                    return;
                foreach (var round in _rounds)
                {
                    round.Execute();
                }
            }
            OnCompleted();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, true) == false)
            {
                Completed = null;
                lock (_sync)
                {
                    foreach (var round in _rounds)
                    {
                        round.Dispose();
                    }
                    _rounds.Dispose();
                }
            }
        }
    }
}
