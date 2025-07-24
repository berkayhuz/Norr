namespace Norr.PerformanceMonitor.Configuration;

/// <summary>
/// Root configuration object for the performance-monitoring library.  
/// Typically supplied via <c>IOptions&lt;PerformanceOptions&gt;</c> and further
/// customised in <c>AddPerformanceMonitoring()</c>.
/// </summary>
public sealed class PerformanceOptions
{
    /// <summary>
    /// Controls <b>how often</b> an operation is measured.  
    /// By default every call is sampled; reduce <see cref="SamplingOptions.Probability"/>
    /// for high-throughput services to lower overhead.
    /// </summary>
    public SamplingOptions Sampling { get; init; } = new();

    /// <summary>
    /// Bitmask selecting which <see cref="IMetricExporter"/> implementations
    /// are active. The default is <see cref="ExporterFlags.Console"/>.
    /// </summary>
    public ExporterFlags Exporters { get; set; } = ExporterFlags.Console;

    /// <summary>
    /// Names of operations (e.g.&nbsp;<c>"HealthCheck"</c>, <c>"HTTP GET /ping"</c>)
    /// that should be <em>excluded</em> from measurement entirely.
    /// Case-insensitive.
    /// </summary>
    public IReadOnlySet<string> IgnoredNames
    {
        get; init;
    }
        = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Thresholds and webhook settings that determine when an alert is emitted.
    /// </summary>
    public AlertOptions Alerts { get; init; } = new();

    /// <summary>
    /// Settings for the Bloom-filter–based duplicate guard that prevents
    /// repetitive metrics from flooding exporters or alert channels.
    /// </summary>
    public DuplicateGuardOptions DuplicateGuard { get; init; } = new();
}
