namespace Norr.PerformanceMonitor.Configuration;

/// <summary>
/// Configuration for the Bloom-filter–based <see cref="IDuplicateGuard"/> that
/// suppresses repetitive metrics to avoid log / alert spam.
/// </summary>
public sealed class DuplicateGuardOptions
{
    /// <summary>
    /// Size of the Bloom filter in <b>bits</b>.  
    /// Must be a power of two (<c>2ⁿ</c>).  
    /// Example: <c>1 &lt;&lt; 20</c> → 1 048 576 bits ≈ 128 KB.
    /// Larger values reduce the probability of false positives at the cost of
    /// memory.
    /// </summary>
    public int BitCount { get; set; } = 1 << 20;

    /// <summary>
    /// The cooldown window. If the same metric name is encountered within this
    /// period, it will be suppressed (i.e.&nbsp;<see cref="IDuplicateGuard.ShouldEmit"/>
    /// returns <c>false</c>).
    /// </summary>
    public TimeSpan CoolDown { get; set; } = TimeSpan.FromSeconds(10);
}
