using System.Timers;
using Timer = System.Timers.Timer;

namespace VmmSharpEx.Refresh;

/// <summary>
///     Internal wrapper for the VMM refresher utilizing a System.Timers.Timer to periodically refresh.
/// </summary>
internal sealed class VmmRefresher : IDisposable
{
    private readonly Vmm _instance;
    private readonly RefreshOptions _option;
    private readonly Timer _timer;
    private bool _disposed;

    /// <summary>
    ///     Ctor for the VmmRefresher.
    /// </summary>
    /// <param name="instance">Parent Vmm instance.</param>
    /// <param name="option">Option to invoke refresh upon.</param>
    /// <param name="interval">Timespan interval in which to refresh. Minimum resolution ~10-15ms.</param>
    public VmmRefresher(Vmm instance, RefreshOptions option, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(instance, nameof(instance));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero, nameof(interval));
        _instance = instance;
        _option = option;
        _timer = new Timer(interval)
        {
            AutoReset = true
        };
        _timer.Elapsed += Interval_Elapsed;
        _timer.Start();
    }

    public void Dispose()
    {
        if (!Interlocked.Exchange(ref _disposed, true))
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }

    private void Interval_Elapsed(object sender, ElapsedEventArgs e)
    {
        if (!_instance.ConfigSet((ulong)_option, 1))
        {
            _instance.Log($"WARNING: {_option} Auto Refresh Failed!", Vmm.LogLevel.Warning);
        }
    }
}