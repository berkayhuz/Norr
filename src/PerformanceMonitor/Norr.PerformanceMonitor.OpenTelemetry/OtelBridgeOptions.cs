// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

namespace Norr.PerformanceMonitor.OpenTelemetry;

/// <summary>
/// Configuration options for the OpenTelemetry bridge in Norr Performance Monitor.
/// Allows customizing service metadata, ActivitySource and Meter settings,
/// instrumentation names, tracing/metrics enablement, and global tags.
/// </summary>
public sealed class OtelBridgeOptions
{
    /// <summary>Service name for Resource. Defaults to assembly name.</summary>
    public string? ServiceName
    {
        get; set;
    }

    /// <summary>Service version for Resource. Defaults to assembly version.</summary>
    public string? ServiceVersion
    {
        get; set;
    }

    /// <summary>ActivitySource name. Defaults to ServiceName or 'Norr.PerformanceMonitor'.</summary>
    public string? ActivitySourceName
    {
        get; set;
    }

    /// <summary>Meter name. Defaults to ServiceName or 'Norr.PerformanceMonitor'.</summary>
    public string? MeterName
    {
        get; set;
    }

    /// <summary>Enable Tracing pipeline (ActivitySource).</summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>Enable Metrics pipeline (Meter).</summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>Default histogram instrument name for operation durations (seconds).</summary>
    public string DurationHistogramName { get; set; } = "norr.operation.duration";

    /// <summary>Default counter instrument name for operation counts.</summary>
    public string OperationCounterName { get; set; } = "norr.operation.count";

    /// <summary>Default histogram instrument name for payload sizes (bytes).</summary>
    public string? PayloadBytesHistogramName { get; set; } = "norr.payload.size";

    /// <summary>When true, record exceptions on Activity and set Status=Error.</summary>
    public bool RecordExceptions { get; set; } = true;

    /// <summary>Global attribute tags attached to all Activities/Metrics.</summary>
    public IDictionary<string, object?> GlobalAttributes
    {
        get; set;
    } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Sampling ratio [0..1]. 1=always on. Only applies to tracing.</summary>
    public double TraceSamplingRatio { get; set; } = 0.1;

    /// <summary>
    /// Enable Prometheus scraping endpoint (ASP.NET Core). 
    /// If true, you must map the scraping endpoint in the host.
    /// </summary>
    public bool EnablePrometheusScrapingEndpoint { get; set; } = false;
}
