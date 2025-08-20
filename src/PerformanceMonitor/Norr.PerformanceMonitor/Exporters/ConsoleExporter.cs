// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Core.Metrics;

namespace Norr.PerformanceMonitor.Exporters;

/// <summary>
/// Very lightweight exporter that writes each <see cref="Metric"/> to
/// <see cref="System.Console"/> in a human-readable table format.
/// Useful for local debugging and CI pipelines where logs are the primary output.
/// </summary>
public sealed class ConsoleExporter : IMetricExporter
{
    /// <inheritdoc />
    public void Export(in Metric metric)
    {
        Console.WriteLine(
            $"[Norr.PerformanceMonitor] {metric.TimestampUtc:O} | " +
            $"{metric.Name,-40} | {metric.Kind,-12} : {metric.Value,8:N2}");
    }
}
