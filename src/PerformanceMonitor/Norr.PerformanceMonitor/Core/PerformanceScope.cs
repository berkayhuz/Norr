using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Norr.PerformanceMonitor.Core;

/// <summary>
/// Simple, standalone measurement scope that publishes duration, CPU-time and
/// GC allocations directly to OpenTelemetry histograms.  
/// Can be used when you only need raw metrics without the sampling / alerting
/// pipeline provided by <see cref="Monitor"/>.
/// </summary>
/// <remarks>
/// Typical usage:
/// <code>
/// using var _ = new PerformanceScope("SynchronousJob");
/// DoWork();
/// </code>
/// Histograms are pre-created once per process; therefore construction and
/// disposal are allocation-free (except for <see cref="Activity"/> bookkeeping).
/// </remarks>
public sealed class PerformanceScope : IDisposable
{
    // ---- static histograms -------------------------------------------------------------------

    private static readonly Meter _meter = new("Norr.PerformanceMonitor");

    private static readonly Histogram<double> _duration =
        _meter.CreateHistogram<double>("method.duration.ms");

    private static readonly Histogram<long> _alloc =
        _meter.CreateHistogram<long>("method.alloc.bytes");

    private static readonly Histogram<double> _cpu =
        _meter.CreateHistogram<double>("method.cpu.ms");

    // ---- instance state ----------------------------------------------------------------------

    private readonly Activity _activity;
    private readonly long _startAlloc;
    private readonly TimeSpan _startCpu;

    /// <summary>
    /// Begins measuring the current code block.
    /// </summary>
    /// <param name="name">Logical operation name (appears in tracing tools).</param>
    public PerformanceScope(string name)
    {
        _activity = new Activity(name).Start();
        _startAlloc = GC.GetAllocatedBytesForCurrentThread();
        _startCpu = Process.GetCurrentProcess().TotalProcessorTime;
    }

    /// <summary>
    /// Completes the measurement and records the captured metrics.
    /// </summary>
    public void Dispose()
    {
        _activity.Stop();

        var elapsed = _activity.Duration.TotalMilliseconds;
        var alloc = GC.GetAllocatedBytesForCurrentThread() - _startAlloc;
        var cpuMs = (Process.GetCurrentProcess().TotalProcessorTime - _startCpu).TotalMilliseconds;

        _duration.Record(elapsed);
        _alloc.Record(alloc);
        _cpu.Record(cpuMs);
    }
}
