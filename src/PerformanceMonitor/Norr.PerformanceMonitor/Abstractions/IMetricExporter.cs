// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using Norr.PerformanceMonitor.Core.Metrics;

namespace Norr.PerformanceMonitor.Abstractions;

/// <summary>
/// Sends <see cref="Metric"/> instances to an external system (console, Prometheus,
/// OTLP, …​) or retains them in-memory for testing.
/// </summary>
public interface IMetricExporter
{
    /// <summary>
    /// Publishes a single <paramref name="metric"/>.  
    /// Implementations should be **non-blocking** and throw no exceptions to avoid
    /// disrupting the caller; if an error occurs, log and return.
    /// </summary>
    /// <param name="metric">The metric payload (name, kind, value, timestamp).</param>
    void Export(in Metric metric);
}
