// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Norr.PerformanceMonitor.OpenTelemetry;

/// <summary>
/// Disposable scope that starts an <see cref="Activity"/> and, on dispose, records:
/// <list type="bullet">
///   <item><description>Operation duration to a histogram (seconds)</description></item>
///   <item><description>Operation count to a counter</description></item>
///   <item><description>Stops the <see cref="Activity"/></description></item>
/// </list>
/// </summary>
public sealed class OtelPerformanceScope : IDisposable
{
    private readonly Activity? _activity;
    private readonly Histogram<double>? _duration;
    private readonly Counter<long>? _counter;
    private readonly long _startTicks;
    private readonly double _tickSeconds;

    private bool _disposed;

    internal OtelPerformanceScope(
        Activity? activity,
        Histogram<double>? durationHistogram,
        Counter<long>? counter)
    {
        _activity = activity;
        _duration = durationHistogram;
        _counter = counter;
        _startTicks = Stopwatch.GetTimestamp();
        _tickSeconds = 1.0 / Stopwatch.Frequency;
    }

    /// <summary>
    /// Records the payload size (in bytes) as both an <see cref="Activity"/> tag
    /// (<c>norr.payload.bytes</c>) and, if provided, to a payload-size histogram.
    /// </summary>
    /// <param name="bytes">Payload size in bytes.</param>
    /// <param name="payloadHistogram">
    /// Optional histogram for payload sizes (bytes). If supplied, <paramref name="bytes"/> is recorded to it.
    /// </param>
    public void RecordPayloadBytes(long bytes, Histogram<long>? payloadHistogram = null)
    {
        if (_activity is { } a)
        {
            a.SetTag(ActivityTags.PayloadBytes, bytes);
        }

        payloadHistogram?.Record(bytes);
    }

    /// <summary>
    /// Completes the scope:
    /// computes elapsed time, records the duration and operation count,
    /// and stops the associated <see cref="Activity"/> if it was started.
    /// Safe to call multiple times; subsequent calls are no-ops.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        var elapsedSeconds = (Stopwatch.GetTimestamp() - _startTicks) * _tickSeconds;

        _duration?.Record(elapsedSeconds);
        _counter?.Add(1);

        _activity?.Stop();

        // No unmanaged resources; this is just for conventional completeness.
        System.GC.SuppressFinalize(this);
    }
}
