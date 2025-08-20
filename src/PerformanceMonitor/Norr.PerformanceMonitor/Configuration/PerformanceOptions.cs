// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 



namespace Norr.PerformanceMonitor.Configuration;

/// <summary>
/// Root configuration object for the performance monitoring library.
/// Typically supplied via <c>IOptions&lt;PerformanceOptions&gt;</c> and further
/// customized in <c>AddPerformanceMonitoring()</c>.
/// </summary>
/// <remarks>
/// <para>
/// Use this type to enable/disable exporters, control sampling behavior,
/// configure alerting thresholds and destinations, and tune metric tagging
/// and scrubbing policies.
/// </para>
/// <para>
/// <b>Thread safety:</b> This object is intended to be configured at application
/// startup and treated as immutable thereafter.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp"><![CDATA[
/// services.Configure<PerformanceOptions>(o =>
/// {
///     o.Sampling = new SamplingOptions { Probability = 0.25 }; // 25% sampling
///     o.Exporters = ExporterFlags.Console | ExporterFlags.Prometheus;
///     o.IgnoredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
///     {
///         "HealthCheck",
///         "HTTP GET /ping"
///     };
///     o.Alerts = new AlertOptions { DurationMs = 500, CpuMs = 300 };
///     o.DuplicateGuard = new DuplicateGuardOptions { BitCount = 1 << 20 };
///     o.Cpu = new CpuOptions { Enabled = true };
///     o.Metrics = new MetricsOptions { IncludeThreadId = false };
///     o.Resource = new ResourceOptions { ServiceName = "OrderService" };
/// });
/// ]]></code>
/// </example>
public sealed class PerformanceOptions
{
    /// <summary>
    /// Controls how often an operation is measured.
    /// </summary>
    /// <remarks>
    /// By default every call is sampled; reduce <see cref="SamplingOptions.Probability"/>
    /// for high-throughput services to lower overhead.
    /// </remarks>
    public SamplingOptions Sampling { get; init; } = new();

    /// <summary>
    /// Bitmask selecting which exporter integrations are active.
    /// </summary>
    /// <remarks>
    /// See <see cref="ExporterFlags"/> for available integrations.
    /// The default is <see cref="ExporterFlags.Console"/>.
    /// </remarks>
    public ExporterFlags Exporters { get; set; } = ExporterFlags.Console;

    /// <summary>
    /// Names of operations (for example, <c>HealthCheck</c> or <c>HTTP GET /ping</c>)
    /// that should be excluded from measurement entirely.
    /// </summary>
    /// <value>
    /// A case-insensitive set of logical operation names.
    /// </value>
    public IReadOnlySet<string> IgnoredNames
    {
        get; init;
    }
        = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Thresholds and webhook settings that determine when an alert is emitted.
    /// </summary>
    /// <seealso cref="AlertOptions"/>
    public AlertOptions Alerts { get; init; } = new();

    /// <summary>
    /// Settings for the Bloom-filter–based duplicate guard that prevents
    /// repetitive metrics from flooding exporters or alert channels.
    /// </summary>
    /// <seealso cref="DuplicateGuardOptions"/>
    public DuplicateGuardOptions DuplicateGuard { get; init; } = new();

    /// <summary>
    /// CPU measurement options.
    /// </summary>
    /// <remarks>
    /// CPU timing availability may vary by platform and sandbox restrictions.
    /// </remarks>
    public CpuOptions Cpu { get; init; } = new();

    /// <summary>
    /// OpenTelemetry metric tagging, temporality, and scrubbing options.
    /// </summary>
    /// <seealso cref="MetricsOptions"/>
    public MetricsOptions Metrics { get; init; } = new();

    /// <summary>
    /// OpenTelemetry resource attributes such as <c>service.name</c>,
    /// <c>service.version</c>, and <c>deployment.environment</c>.
    /// </summary>
    /// <seealso cref="ResourceOptions"/>
    public ResourceOptions Resource { get; init; } = new();
}
