// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Norr.PerformanceMonitor.Exporters.InMemory;

/// <summary>
/// Emits exporter‑scoped metrics (counters and gauges) for the in‑memory exporter,
/// including enqueue/drop totals, live queue depth, and a rolling drop rate.
/// </summary>
/// <remarks>
/// <para>
/// Instruments created:
/// </para>
/// <list type="bullet">
///   <item><c>norr.exporter.enqueued.total</c> (<see cref="Counter{T}"/>): total accepted items.</item>
///  <item><c>norr.exporter.dropped.total</c> (<see cref = "Counter{T}" />): total dropped items.</item>
///  <item><c>norr.exporter.queue.depth</c>(<see cref = "ObservableGauge{T}" />): current queue depth.</item>
///  <item><c>norr.exporter.drop.rate_1m</c>(<see cref = "ObservableGauge{T}" />): rolling drop rate over the last minute(items/second).</item>
/// </list>
/// <para>
/// <b>Thread safety:</b> All counters are safe for concurrent use. The rolling drop window is updated
/// on each drop and read by the observable gauge when the meter is collected.
/// </para>
/// </remarks>
internal sealed class ExporterMetrics : IDisposable
{
    // Static meter shared by exporter metrics.
    private static readonly Meter _meter = new("Norr.PerformanceMonitor.Exporter", "1.0.0");

    private readonly string _name;
    private readonly Func<int> _getDepth;

    private readonly Counter<long> _enqueued;
    private readonly Counter<long> _dropped;

    // Tracks drops in a 60-second rolling window for the rate gauge.
    private readonly RollingWindowCounter _dropWindow;

    private readonly ObservableGauge<long> _depthGauge;
    private readonly ObservableGauge<double> _dropRateGauge;

    /// <summary>
    /// Initializes a new instance of <see cref="ExporterMetrics"/>.
    /// </summary>
    /// <param name="name">
    /// Logical exporter name used as a metric dimension (tag). If null or whitespace, defaults to <c>"default"</c>.
    /// </param>
    /// <param name="getDepth">
    /// Delegate that returns the current queue depth. Invoked by the <c>queue.depth</c> observable gauge.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="getDepth"/> is <see langword="null"/>.</exception>
    public ExporterMetrics(string name, Func<int> getDepth)
    {
        _name = string.IsNullOrWhiteSpace(name) ? "default" : name;
        _getDepth = getDepth ?? throw new ArgumentNullException(nameof(getDepth));

        _enqueued = _meter.CreateCounter<long>(
            name: "norr.exporter.enqueued.total",
            unit: "items",
            description: "Total items accepted by the in-memory exporter.");

        _dropped = _meter.CreateCounter<long>(
            name: "norr.exporter.dropped.total",
            unit: "items",
            description: "Total items dropped by the in-memory exporter.");

        _dropWindow = new RollingWindowCounter(windowSeconds: 60);

        // Observable queue depth (items)
        _depthGauge = _meter.CreateObservableGauge<long>(
            name: "norr.exporter.queue.depth",
            observeValue: () =>
                new Measurement<long>(
                    _getDepth(),
                    new KeyValuePair<string, object?>("exporter", _name)),
            unit: "items",
            description: "Current queue depth of the in-memory exporter.");

        // Observable drop rate (items/second) over the last 60s
        _dropRateGauge = _meter.CreateObservableGauge<double>(
            name: "norr.exporter.drop.rate_1m",
            observeValue: () =>
                new Measurement<double>(
                    _dropWindow.RatePerSecond(),
                    new KeyValuePair<string, object?>("exporter", _name)),
            unit: "items/s",
            description: "Drop rate over the last 1 minute (per second).");
    }

    /// <summary>
    /// Records that <paramref name="count"/> items were successfully enqueued.
    /// </summary>
    /// <param name="count">Number of items to add. Defaults to <c>1</c>.</param>
    public void OnEnqueued(long count = 1)
    {
        var tags = GetTags();
        _enqueued.Add(count, in tags);
    }

    /// <summary>
    /// Records that <paramref name="count"/> items were dropped and updates the rolling drop window.
    /// </summary>
    /// <param name="count">Number of items to add. Defaults to <c>1</c>.</param>
    public void OnDropped(long count = 1)
    {
        var tags = GetTags();
        _dropped.Add(count, in tags);

        // Update rolling window for each dropped item (small counts expected per call).
        for (int i = 0; i < count; i++)
        {
            _dropWindow.Add(1);
        }
    }

    /// <summary>
    /// Builds the tag list used for all exporter metrics (currently only the <c>exporter</c> dimension).
    /// </summary>
    private TagList GetTags()
    {
        var tags = new TagList { { "exporter", _name } };
        return tags;
    }

    /// <summary>
    /// Disposes resources held by this instance.
    /// </summary>
    /// <remarks>
    /// Observable instruments live for the lifetime of the <see cref="Meter"/>; nothing to dispose currently.
    /// This method exists as a future extension point for additional resources.
    /// </remarks>
    public void Dispose()
    {
        // No managed/unmanaged resources to release at the moment.
    }
}
