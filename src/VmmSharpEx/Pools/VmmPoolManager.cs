using Microsoft.Extensions.ObjectPool;

namespace VmmSharpEx.Pools
{
    internal static class VmmPoolManager
    {
        /// <summary>
        /// Internal Object Pool Provider for VmmSharpEx.
        /// </summary>
        internal static ObjectPoolProvider ObjectPoolProvider { get; } = new DefaultObjectPoolProvider()
        {
            MaximumRetained = int.MaxValue - 1
        };
    }
}
