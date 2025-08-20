using VmmSharpEx;
using VmmSharpEx.Options;
using VmmSharpEx.Refresh;

internal sealed class VmmRefresher : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public VmmRefresher(Vmm instance, RefreshOption option, TimeSpan interval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero, nameof(interval));
        _ = RunAsync(instance, option, interval, _cts.Token);
    }

    private static async Task RunAsync(Vmm instance, RefreshOption option, TimeSpan interval, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            if (!instance.ConfigSet((VmmOption)option, 1))
                instance.Log($"WARNING: {option} Auto Refresh Failed!", Vmm.LogLevel.Warning);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, true) == false)
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}