using VmmSharpEx.Options;

namespace VmmSharpEx.Refresh;

/// <summary>
/// VMM Refresh Options.
/// </summary>
public enum RefreshOptions : ulong
{
    /// <summary>
    /// refresh all caches
    /// </summary>
    All = VmmOption.REFRESH_ALL,

    /// <summary>
    /// refresh memory cache (excl. TLB) [fully]
    /// </summary>
    Memory = VmmOption.REFRESH_FREQ_MEM,

    /// <summary>
    /// refresh memory cache (excl. TLB) [partial 33%/call]
    /// </summary>
    MemoryPartial = VmmOption.REFRESH_FREQ_MEM_PARTIAL,

    /// <summary>
    /// refresh page table (TLB) cache [fully]
    /// </summary>
    Tlb = VmmOption.REFRESH_FREQ_TLB,

    /// <summary>
    /// refresh page table (TLB) cache [partial 33%/call]
    /// </summary>
    TlbPartial = VmmOption.REFRESH_FREQ_TLB_PARTIAL,

    /// <summary>
    /// refresh fast frequency - incl. partial process refresh
    /// </summary>
    Fast = VmmOption.REFRESH_FREQ_FAST,

    /// <summary>
    /// refresh medium frequency - incl. full process refresh
    /// </summary>
    Medium = VmmOption.REFRESH_FREQ_MEDIUM,

    /// <summary>
    /// refresh slow frequency.
    /// </summary>
    Slow = VmmOption.REFRESH_FREQ_SLOW
}