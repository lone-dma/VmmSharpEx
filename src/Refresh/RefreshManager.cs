using System.Collections.Concurrent;

namespace VmmSharpEx.Refresh;

/// <summary>
///     Controls the registration and management of refreshers for Vmm instances.
/// </summary>
internal static class RefreshManager
{
    private static readonly ConcurrentDictionary<Vmm, ConcurrentDictionary<RefreshOptions, VmmRefresher>> _refreshers = new();

    /// <summary>
    ///     Register a refresher for the given Vmm instance and refresh option.
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="option"></param>
    /// <param name="interval"></param>
    /// <exception cref="VmmException"></exception>
    public static void Register(Vmm instance, RefreshOptions option, TimeSpan interval)
    {
        var dict = _refreshers.GetOrAdd(instance, new ConcurrentDictionary<RefreshOptions, VmmRefresher>());
        if (dict.ContainsKey(option))
            throw new VmmException("Refresher already registered for this option!");
        var refresher = new VmmRefresher(instance, option, interval);
        dict[option] = refresher;
    }

    /// <summary>
    ///     Unregister a refresher for the given Vmm instance and refresh option.
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="option"></param>
    public static void Unregister(Vmm instance, RefreshOptions option)
    {
        if (_refreshers.TryGetValue(instance, out var dict) && dict.TryRemove(option, out var refresher)) refresher.Dispose();
    }

    /// <summary>
    ///     Unregister all refreshers for the given Vmm instance.
    ///     Usually called when the parent Vmm instance is disposed or no longer needed.
    /// </summary>
    /// <param name="instance"></param>
    public static void UnregisterAll(Vmm instance)
    {
        if (_refreshers.TryRemove(instance, out var dict))
            foreach (var refresher in dict.Values)
                refresher.Dispose();
    }
}