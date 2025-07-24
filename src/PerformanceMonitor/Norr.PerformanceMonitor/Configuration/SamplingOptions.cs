namespace Norr.PerformanceMonitor.Configuration;

/// <summary>
/// Settings that control <i>how frequently</i> the library samples an operation.
/// Applied by implementations of <see cref="PerformanceMonitor.Sampling.ISampler"/>,
/// most notably <c>ProbabilitySampler</c>.
/// </summary>
public sealed class SamplingOptions
{
    /// <summary>
    /// Probability (0 – 1) that an individual operation will be measured.  
    /// <list type="bullet">
    ///   <item><description><c>1.0</c>  →  measure every call (default)</description></item>
    ///   <item><description><c>0.1</c>  →  ~10 % of calls</description></item>
    ///   <item><description><c>0.0</c>  →  sampling disabled</description></item>
    /// </list>
    /// </summary>
    public double Probability { get; init; } = 1.0;

    /// <summary>
    /// Optional 64-bit seed used for <b>deterministic</b> sampling.  
    /// When the same <paramref name="Seed"/> and operation name are supplied,
    /// the sampler will reach the same decision across different nodes/instances,
    /// ensuring consistent aggregation in distributed systems.
    /// </summary>
    public ulong? Seed
    {
        get; init;
    }
}
