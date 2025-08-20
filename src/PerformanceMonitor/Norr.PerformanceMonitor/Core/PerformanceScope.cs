// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Diagnostics;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;

using Norr.PerformanceMonitor.Configuration;
using Norr.PerformanceMonitor.Core.Runtime;
using Norr.PerformanceMonitor.Telemetry;
using Norr.Diagnostics.Abstractions.Logging;

namespace Norr.PerformanceMonitor.Core;

/// <summary>
/// Measures the performance of a single named operation, recording duration, allocation size, and CPU usage
/// into global histograms. This is a low-overhead alternative to <see cref="Monitor"/> when you only need raw metrics.
/// </summary>
/// <remarks>
/// <para>
/// The scope records the following metrics on <see cref="Dispose"/>:
/// </para>
/// <list type="bullet">
///   <item><description>Wall-clock duration in milliseconds (<c>method.duration.ms</c>)</description></item>
///   <item><description>Allocated bytes on the current thread (<c>method.alloc.bytes</c>)</description></item>
///   <item><description>CPU time (ms) via <see cref="CpuMeasureMode.ThreadTime"/> or an approximate process-wide measurement
/// normalized by concurrency (<c>method.cpu.ms</c>)</description></item>
///   <item><description>Optional CPU % of elapsed (<c>method.cpu.pct</c>) and CPU % normalized to cores (<c>method.cpu.pct_norm</c>)</description></item>
///   <item><description>Optional CPU cycles on Windows (<c>method.cpu.cycles</c>)</description></item>
/// </list>
/// <para>
/// Global tags from <see cref="MetricsOptions.GlobalTags"/> and ambient tags from <see cref="TagContext"/> are merged
/// (inner-wins), scrubbed via <see cref="TagScrubber"/>, and applied to all recorded metrics.
/// </para>
/// <para><b>Thread safety:</b> This type is not thread-safe. Create a new instance per measured operation.</para>
/// </remarks>
/// <example>
/// <code language="csharp"><![CDATA[
/// using var scope = new PerformanceScope("DB Query", CpuMeasureMode.ThreadTime);
/// ExecuteDatabaseQuery();
/// // Dispose() automatically records metrics here
/// ]]></code>
/// </example>
public sealed class PerformanceScope : IDisposable
{
    private static readonly Meter _meter = new("Norr.PerformanceMonitor", "1.0.0");
    private static readonly ActivitySource _activitySource = new("Norr.PerformanceMonitor");

    private static readonly Histogram<double> _duration = _meter.CreateHistogram<double>("method.duration.ms", "ms");
    private static readonly Histogram<long> _alloc = _meter.CreateHistogram<long>("method.alloc.bytes", "By");
    private static readonly Histogram<double> _cpuMs = _meter.CreateHistogram<double>("method.cpu.ms", "ms");
    private static readonly Histogram<double> _cpuPct = _meter.CreateHistogram<double>("method.cpu.pct", "percent");
    private static readonly Histogram<double> _cpuPctN = _meter.CreateHistogram<double>("method.cpu.pct_norm", "percent");
    private static readonly Histogram<ulong> _cycles = _meter.CreateHistogram<ulong>("method.cpu.cycles", "cycles");

    private static readonly ThreadCpuTimeProvider _cpuProvider = new();

    private readonly Activity? _activity;
    private readonly long _startAlloc;
    private readonly string _name;

    private readonly CpuMeasureMode _mode;
    private readonly bool _recordPct;
    private readonly bool _recordPctNorm;
    private readonly bool _recordCycles;

    private readonly TimeSpan _startCpuThread;
    private readonly TimeSpan _startCpuProcess;

    private readonly MetricsOptions _metrics;

    private readonly ILogger<PerformanceScope>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceScope"/> class.
    /// Uses optional CPU % and cycles recording settings.
    /// </summary>
    /// <param name="name">Stable, low-cardinality operation name (e.g., <c>"HTTP GET /users"</c>).</param>
    /// <param name="mode">The CPU measurement mode to use.</param>
    /// <param name="recordPercent">Whether to record CPU % of elapsed time.</param>
    /// <param name="recordPercentNormalizedToCores">Whether to record CPU % normalized to logical core count.</param>
    /// <param name="recordCycles">Whether to record CPU cycles (Windows only).</param>
    public PerformanceScope(
        string name,
        CpuMeasureMode mode = CpuMeasureMode.ThreadTime,
        bool recordPercent = true,
        bool recordPercentNormalizedToCores = false,
        bool recordCycles = false)
        : this(name, mode, recordPercent, recordPercentNormalizedToCores, recordCycles, metrics: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceScope"/> class with explicit <see cref="MetricsOptions"/>.
    /// </summary>
    /// <param name="name">Stable, low-cardinality operation name.</param>
    /// <param name="mode">The CPU measurement mode to use.</param>
    /// <param name="recordPercent">Whether to record CPU % of elapsed time.</param>
    /// <param name="recordPercentNormalizedToCores">Whether to record CPU % normalized to logical core count.</param>
    /// <param name="recordCycles">Whether to record CPU cycles (Windows only).</param>
    /// <param name="metrics">Optional metrics options controlling global tags, scrubbing, and thread ID tagging.</param>
    public PerformanceScope(
        string name,
        CpuMeasureMode mode,
        bool recordPercent,
        bool recordPercentNormalizedToCores,
        bool recordCycles,
        MetricsOptions? metrics,
        ILogger<PerformanceScope>? logger = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _mode = mode;
        _recordPct = recordPercent;
        _recordPctNorm = recordPercentNormalizedToCores;
        _recordCycles = recordCycles;
        _metrics = metrics ?? new MetricsOptions();

        _activity = _activitySource.StartActivity(name, ActivityKind.Internal);
        _startAlloc = GC.GetAllocatedBytesForCurrentThread();

        if (mode == CpuMeasureMode.ThreadTime && _cpuProvider.IsSupported)
        {
            _startCpuThread = _cpuProvider.GetCurrentThreadCpuTime();
        }
        else if (mode == CpuMeasureMode.ProcessApproximate)
        {
            _startCpuProcess = Process.GetCurrentProcess().TotalProcessorTime;
        }

        _logger = logger;
    }

    /// <summary>
    /// Stops timing and records all configured metrics to their respective histograms.
    /// </summary>
    /// <remarks>
    /// Always call <see cref="Dispose"/> once per scope, ideally via a <c>using</c> statement, to ensure
    /// metrics are recorded even if the operation throws an exception.
    /// </remarks>
    public void Dispose()
    {
        _activity?.Stop();

        var elapsed = (_activity?.Duration.TotalMilliseconds) ?? 0.0;
        var alloc = GC.GetAllocatedBytesForCurrentThread() - _startAlloc;

        var tags = BuildTags(_metrics, _name, category: "custom");

        _duration.Record(elapsed, tags);
        _alloc.Record(alloc, tags);

        string cpuMode = "disabled";
        double cpuMs = 0;

        if (_mode == CpuMeasureMode.ThreadTime && _cpuProvider.IsSupported)
        {
            var cpu = _cpuProvider.GetCurrentThreadCpuTime() - _startCpuThread;
            if (cpu.Ticks < 0)
            {
                _logger?.PM().Error("CPU time anomaly: negative ticks.");
                cpu = TimeSpan.Zero;
            }
            cpuMs = cpu.TotalMilliseconds;
            cpuMode = "thread";
        }
        else if (_mode == CpuMeasureMode.ProcessApproximate)
        {
            var cpu = Process.GetCurrentProcess().TotalProcessorTime - _startCpuProcess;
            if (cpu.Ticks < 0)
            {
                _logger?.PM().Error("CPU time anomaly: negative ticks.");
                cpu = TimeSpan.Zero;
            }
            cpuMs = Math.Min(cpu.TotalMilliseconds, elapsed);
            cpuMode = "process_approx";
        }

        tags.Add("norr.cpu.mode", cpuMode);

        if (cpuMode != "disabled")
        {
            _cpuMs.Record(cpuMs, tags);

            if (_recordPct && elapsed > 0)
                _cpuPct.Record(100.0 * (cpuMs / elapsed), tags);

            if (_recordPctNorm && elapsed > 0)
                _cpuPctN.Record(100.0 * (cpuMs / (elapsed * Math.Max(1, Environment.ProcessorCount))), tags);

            if (_recordCycles && _cpuProvider.TryGetThreadCycleCount(out var cyc))
                _cycles.Record(cyc, tags);
        }
    }

    private static TagList BuildTags(MetricsOptions opt, string name, string category)
    {
        var tl = new TagList();

        if (opt.GlobalTags is { Count: > 0 })
        {
            foreach (var kv in opt.GlobalTags)
                tl.Add(kv.Key, kv.Value);
        }

        TagContext.CopyTo(ref tl);

        TagScrubber.Apply(ref tl, opt.Scrub);

        tl.Add("norr.name", name);
        tl.Add("norr.category", category);

        if (opt.IncludeThreadId)
            tl.Add("norr.thread.id", Environment.CurrentManagedThreadId);

        return tl;
    }
}
