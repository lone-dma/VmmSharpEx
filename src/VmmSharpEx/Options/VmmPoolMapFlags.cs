namespace VmmSharpEx.Options
{
    /// <summary>
    /// Pool map flags returned by VMMDLL.  
    /// Used to filter pool allocations.
    /// </summary>
    public enum VmmPoolMapFlags : uint
    {
        /// <summary>
        /// Include all pools.
        /// </summary>
        ALL = 0,

        /// <summary>
        /// Include only big pool allocations.
        /// </summary>
        BIG = 1
    }
}
