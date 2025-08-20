// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Core;
using Norr.PerformanceMonitor.Core.Metrics;
using Norr.PerformanceMonitor.Core.Metrics.Aggregation;

namespace Norr.PerformanceMonitor.Exporters
{
    /// <summary>
    /// An <see cref="IMetricExporter"/> implementation that feeds metrics into the
    /// in-process <see cref="AggregationRegistry"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exporter enables Prometheus-style text exposition without requiring an
    /// external metrics backend. Metrics are aggregated in memory using either
    /// histograms (for duration metrics) or summaries (for other numeric metrics).
    /// </para>
    /// <para>
    /// <b>Thread safety:</b> This type is thread-safe. Observations can be exported
    /// from multiple threads concurrently.
    /// </para>
    /// <para>
    /// <b>Use cases:</b>
    /// <list type="bullet">
    ///   <item>Development environments without a Prometheus server.</item>
    ///   <item>Lightweight deployments where metrics must be scraped locally.</item>
    ///   <item>Integration tests verifying aggregated metric output.</item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class AggregationExporter : IMetricExporter
    {
        /// <summary>
        /// Exports a single <see cref="Metric"/> observation into the in-process aggregation registry.
        /// </summary>
        /// <param name="metric">
        /// The metric to export. Passed by <see langword="in"/> reference to avoid unnecessary struct copying.
        /// </param>
        /// <remarks>
        /// <para>
        /// The exporter determines the <see cref="AggregatorKind"/> based on the metric's
        /// <see cref="MetricKind"/>:
        /// </para>
        /// <list type="bullet">
        ///   <item><see cref="MetricKind.DurationMs"/> → <see cref="AggregatorKind.Histogram"/></item>
        ///   <item>Any other kind → <see cref="AggregatorKind.Summary"/></item>
        /// </list>
        /// The metric's value is then observed in the corresponding aggregator.
        /// </remarks>
        public void Export(in Metric metric)
        {
            var kind = metric.Kind == MetricKind.DurationMs
                ? AggregatorKind.Histogram
                : AggregatorKind.Summary;

            var agg = AggregationRegistry.GetOrCreate(metric.Name + ":" + metric.Kind, kind);
            agg.Observe(metric.Value);
        }
    }
}
