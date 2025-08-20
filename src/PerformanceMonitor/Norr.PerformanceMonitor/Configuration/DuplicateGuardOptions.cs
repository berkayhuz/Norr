// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 



namespace Norr.PerformanceMonitor.Configuration;

/// <summary>
/// Configuration for the Bloom‑filter–based <see cref="Norr.PerformanceMonitor.Abstractions.IDuplicateGuard"/>
/// that suppresses repetitive metrics to avoid log / alert spam.
/// </summary>
/// <remarks>
/// <para>
/// A Bloom filter is a probabilistic data structure: it can yield false positives
/// (i.e., report that an item was seen before when it actually was not), but it does
/// not yield false negatives. Larger filters and more hash functions reduce the false
/// positive probability at the cost of memory and CPU.
/// </para>
/// <para>
/// These options are intended to tune a duplicate guard that decides whether a metric
/// name should be emitted again within a cooldown window. See
/// <see cref="Norr.PerformanceMonitor.Abstractions.IDuplicateGuard.ShouldEmit(string, DateTime)"/>
/// for the decision contract.
/// </para>
/// </remarks>
public sealed class DuplicateGuardOptions
{
    /// <summary>
    /// Size of the Bloom filter in <b>bits</b>.
    /// Must be a power of two (<c>2^n</c>).
    /// Example: <c>1 &lt;&lt; 20</c> → 1 048 576 bits ≈ 128 KB.
    /// Larger values reduce the probability of false positives at the cost of memory.
    /// </summary>
    /// <remarks>
    /// Many implementations use a bit‑mask for fast modulo; choosing a power of two
    /// enables this optimization.
    /// </remarks>
    public int BitCount { get; set; } = 1 << 20;

    /// <summary>
    /// The cooldown window. If the same metric name is encountered within this period,
    /// it will be suppressed (i.e., <see cref="Norr.PerformanceMonitor.Abstractions.IDuplicateGuard.ShouldEmit(string, DateTime)"/>
    /// returns <see langword="false"/>).
    /// </summary>
    /// <remarks>
    /// Pick a value that balances noise reduction and visibility. Short windows reduce
    /// spam but allow more frequent repeats; long windows may hide legitimate bursts.
    /// </remarks>
    public TimeSpan CoolDown { get; set; } = TimeSpan.FromSeconds(10);
}
