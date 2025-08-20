// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 



namespace Norr.PerformanceMonitor.Configuration;

/// <summary>
/// Defines the strategy used to decide whether a given operation should be measured.
/// </summary>
public enum SamplerMode
{
    /// <summary>
    /// Thread-safe random sampling using <see cref="Random.Shared"/>.
    /// </summary>
    Random = 0,

    /// <summary>
    /// Deterministic sampling: <c>hash64(name, seed) / 2^64 &lt;= p</c>.
    /// </summary>
    /// <remarks>
    /// Using the same <see cref="SamplingOptions.Seed"/> value and <c>name</c>
    /// on all nodes yields the same sampling decision. This is useful when
    /// you want consistent inclusion/exclusion of specific operations across
    /// a distributed system.
    /// </remarks>
    Deterministic = 1
}

/// <summary>
/// Configuration controlling whether an individual call is measured (sampled)
/// or ignored to reduce overhead.
/// </summary>
/// <remarks>
/// <para>
/// Sampling is essential for high-throughput systems where measuring every
/// request would be too costly. It can be configured to be random or deterministic
/// based on operation name.
/// </para>
/// <para>
/// Deterministic mode ensures that the same operation name produces the same
/// sampling decision across all nodes, provided they share the same
/// <see cref="Seed"/>.
/// </para>
/// </remarks>
public sealed class SamplingOptions
{
    /// <summary>
    /// Base sampling probability in the range <c>0.0</c> to <c>1.0</c>.
    /// </summary>
    /// <value>
    /// <c>1.0</c> means sample every call; <c>0.0</c> means sample none.
    /// </value>
    public double Probability { get; init; } = 1.0;

    /// <summary>
    /// Seed value used in <see cref="SamplerMode.Deterministic"/> mode.
    /// </summary>
    /// <value>
    /// Using the same seed and operation name across all nodes ensures the
    /// same sampling decision is made. In <see cref="SamplerMode.Random"/> mode,
    /// this can optionally be used as the random number generator's initial seed.
    /// </value>
    public ulong? Seed
    {
        get; init;
    }

    /// <summary>
    /// The sampling mode to apply.
    /// </summary>
    /// <value>
    /// Defaults to <see cref="SamplerMode.Deterministic"/>.
    /// </value>
    public SamplerMode Mode { get; init; } = SamplerMode.Deterministic;

    /// <summary>
    /// Maximum number of samples to accept per second, or <see langword="null"/> for no limit.
    /// </summary>
    /// <remarks>
    /// Implemented using a token-bucket algorithm, which allows bursts.
    /// </remarks>
    public double? MaxSamplesPerSecond
    {
        get; init;
    }

    /// <summary>
    /// Capacity of the token bucket (burst size).
    /// </summary>
    /// <value>
    /// Defaults to 100. Only used when <see cref="MaxSamplesPerSecond"/> is set.
    /// </value>
    public int RateLimiterBurst { get; init; } = 100;

    /// <summary>
    /// Overrides for sampling probability based on operation name.
    /// </summary>
    /// <remarks>
    /// The keys must match the operation name exactly (case-insensitive).
    /// Example:
    /// <code language="csharp">
    /// new Dictionary&lt;string, double&gt;
    /// {
    ///     ["HTTP GET /health"] = 0.0,
    ///     ["OrderService.PlaceOrder"] = 1.0
    /// }
    /// </code>
    /// </remarks>
    public Dictionary<string, double> NameProbabilities
    {
        get; init;
    }
        = new(StringComparer.OrdinalIgnoreCase);
}
