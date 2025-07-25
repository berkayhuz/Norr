﻿namespace Norr.PerformanceMonitor.Configuration;

/// <summary>
/// Bit-flag mask that enables or disables metric exporters at runtime.
/// Combine values with the “|” operator, e.g.
/// <code>
/// o.Exporters = ExporterFlags.Console | ExporterFlags.Prometheus;
/// </code>
/// </summary>
[Flags]
public enum ExporterFlags
{
    /// <summary>No exporter enabled.</summary>
    None = 0,

    /// <summary>
    /// Writes metrics to <c>Console.WriteLine</c>; handy for local debugging /
    /// CI logs.
    /// </summary>
    Console = 1 << 0,

    /// <summary>
    /// Stores metrics in an in-memory buffer.  
    /// Primarily used by unit-tests to assert that a metric was recorded.
    /// </summary>
    InMemory = 1 << 1,

    /// <summary>
    /// Exposes metrics via <see href="https://prometheus.io/">Prometheus</see>
    /// using the OpenTelemetry <c>AddPrometheusExporter()</c> pipeline.
    /// </summary>
    Prometheus = 1 << 2,

    /// <summary>
    /// Sends metrics to any OTLP-compatible backend (e.g.&nbsp;Grafana
    /// Tempo / Loki, Jaeger, Honeycomb) via the OpenTelemetry
    /// <c>AddOtlpExporter()</c> integration.
    /// </summary>
    Otlp = 1 << 3
}
