// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Norr.Diagnostics.Abstractions.Logging;
using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Alerting;
using Norr.PerformanceMonitor.Configuration;
using Norr.PerformanceMonitor.Core.Metrics;
using Norr.PerformanceMonitor.Core.Runtime;
using Norr.PerformanceMonitor.Sampling;
using Norr.PerformanceMonitor.Telemetry;
using Norr.PerformanceMonitor.Telemetry.Core;

namespace Norr.PerformanceMonitor.Core;

/// <summary>
/// High-level performance monitor that measures operation duration, allocations, and CPU usage,
/// exports metrics, and emits alerts according to configured thresholds.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it measures:</b>
/// </para>
/// <list type="bullet">
///   <item><description><b>Duration</b> (wall-clock ms)</description></item>
///   <item><description><b>Allocated bytes</b> on the current thread</description></item>
///   <item><description><b>CPU time</b> (ms) using either per-thread CPU time (preferred) or an approximate process-wide
///   measurement normalized by the number of concurrently active scopes</description></item>
///   <item><description><b>CPU % of elapsed</b> and<b> CPU % normalized to logical cores</b> (optional)</description></item>
///   <item><description><b>CPU cycles</b> on Windows, when available</description></item>
/// </list>
/// <para>
/// <b>Export &amp; alert:</b> Metrics are pushed to registered <see cref="IMetricExporter"/>s. Threshold
/// breaches are sent to registered <see cref="IAlertSink"/>s.
/// </para>
/// <para>
/// <b>Sampling &amp; duplicate suppression:</b> Emission is gated by <see cref="ISampler"/>. A duplicate
/// guard (<see cref="IDuplicateGuard"/>) suppresses repeated emissions of identical metric keys in a time window.
/// </para>
/// <para>
/// <b>Tags:</b> Each metric is annotated with scrubbed tags. Ambient tags from <see cref="TagContext"/> are merged
/// (inner‑wins) and scrubbed by <see cref="TagScrubber"/>. Built-in tags include:
/// <c>norr.name</c>, <c>norr.category</c>, <c>norr.cpu.mode</c>, optional <c>norr.cpu.approx</c>, and optional <c>norr.thread.id</c>.
/// </para>
/// <para>
/// <b>Thread safety:</b> The type is safe to use concurrently. Scopes are independent per call to <see cref="Begin(string)"/>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp"><![CDATA[
/// var monitor = provider.GetRequiredService<IMonitor>();
/// using var scope = monitor.Begin("HTTP GET /users");
/// // do work...
/// // scope.Dispose() happens automatically at end of using
/// ]]></code>
/// </example>
/// <seealso cref="IMonitor"/>
/// <seealso cref="IMetricExporter"/>
/// <seealso cref="IAlertSink"/>
public sealed class Monitor : IMonitor
{
    private static int _activeScopes;

    private static readonly ActivitySource _activitySource = new("Norr.PerformanceMonitor");

    private readonly Meter _meter = new("Norr.PerformanceMonitor", "1.0.0");
    private readonly Histogram<double> _duration;
    private readonly Histogram<long> _alloc;
    private readonly Histogram<double> _cpuMs;
    private readonly Histogram<double> _cpuPct;
    private readonly Histogram<double> _cpuPctNorm;

    // NOTE: Histogram<ulong> desteklenmiyor. long kullanıyoruz.
    private readonly Histogram<long> _cpuCycles;

    private readonly ISampler _sampler;
    private readonly IDuplicateGuard _dupGuard;
    private readonly IReadOnlyList<IMetricExporter> _exporters;
    private readonly IReadOnlyList<IAlertSink> _alerts;
    private readonly PerformanceOptions _opts;
    private readonly AlertOptions _alertOpts;
    private readonly CpuOptions _cpuOpts;
    private readonly MetricsOptions _metricsOpts;
    private readonly IThreadCpuTimeProvider _cpuProvider;
    private readonly ILogger<Monitor>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Monitor"/> class.
    /// </summary>
    /// <param name="opts">Application performance configuration.</param>
    /// <param name="exporters">Metric exporters that receive emitted metrics.</param>
    /// <param name="alerts">Alert sinks that receive threshold breach notifications.</param>
    /// <param name="sampler">
    /// Optional sampler controlling whether a named operation should be measured.
    /// If <see langword="null"/>, a <see cref="SmartSampler"/> is created from <paramref name="opts"/>.
    /// </param>
    /// <param name="dupGuard">
    /// Optional duplicate emission guard. If <see langword="null"/>, a <see cref="ConcurrentBloomDuplicateGuard"/>
    /// is created from <paramref name="opts"/>.
    /// </param>
    /// <param name="cpuProvider">
    /// Optional CPU provider. If <see langword="null"/>, a <see cref="ThreadCpuTimeProvider"/> is used.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="opts"/>, <paramref name="exporters"/>, or <paramref name="alerts"/> is <see langword="null"/>.
    /// </exception>
    public Monitor(
        IOptions<PerformanceOptions> opts,
        IEnumerable<IMetricExporter> exporters,
        IEnumerable<IAlertSink> alerts,
        ISampler? sampler = null,
        IDuplicateGuard? dupGuard = null,
        IThreadCpuTimeProvider? cpuProvider = null,
        ILogger<Monitor>? logger = null)
    {
        if (opts is null)
            throw new ArgumentNullException(nameof(opts));
        if (exporters is null)
            throw new ArgumentNullException(nameof(exporters));
        if (alerts is null)
            throw new ArgumentNullException(nameof(alerts));

        _logger = logger;

        _opts = opts.Value;
        _alertOpts = _opts.Alerts;
        _cpuOpts = _opts.Cpu;
        _metricsOpts = _opts.Metrics;
        _exporters = exporters.ToList();
        _alerts = alerts.ToList();

        _sampler = sampler ?? new SmartSampler(_opts.Sampling);
        _dupGuard = dupGuard ?? new ConcurrentBloomDuplicateGuard(_opts.DuplicateGuard);
        _cpuProvider = cpuProvider ?? new ThreadCpuTimeProvider();

        _duration = _meter.CreateHistogram<double>("method.duration.ms", unit: "ms", description: "Wall-clock duration");
        _alloc = _meter.CreateHistogram<long>("method.alloc.bytes", unit: "By");
        _cpuMs = _meter.CreateHistogram<double>("method.cpu.ms", unit: "ms", description: "CPU time (user+kernel)");
        _cpuPct = _meter.CreateHistogram<double>("method.cpu.pct", unit: "percent", description: "CPU% of elapsed (single-core scale)");
        _cpuPctNorm = _meter.CreateHistogram<double>("method.cpu.pct_norm", unit: "percent", description: "CPU% normalized to logical cores");

        // *** FIX: ulong -> long  (CreateHistogram<long>)
        _cpuCycles = _meter.CreateHistogram<long>("method.cpu.cycles", unit: "cycles", description: "CPU cycles (Windows only; do not convert to time)");
    }

    /// <summary>
    /// Begins a performance measurement scope for the specified operation <paramref name="name"/>.
    /// </summary>
    /// <param name="name">A stable, low-cardinality operation name (e.g., <c>"HTTP GET /users/{id}"</c>).</param>
    /// <returns>
    /// An <see cref="IPerformanceScope"/> that records metrics on dispose. If sampling is disabled or the name
    /// is configured to be ignored, a no-op scope is returned.
    /// </returns>
    /// <remarks>
    /// Use with <c>using</c> to ensure timely disposal and emission:
    /// <code language="csharp"><![CDATA[
    /// using var scope = monitor.Begin("MyOperation");
    /// // work...
    /// ]]></code>
    /// </remarks>
    public IPerformanceScope Begin(string name)
    {
        if (!_sampler.ShouldSample(name) || _opts.IgnoredNames.Contains(name))
            return NoopScope.Instance;

        _logger?.PM().StartMonitoring(name);
        return new Scope(this, name);
    }

    private void Emit(string name, MetricKind kind, double value, DateTime ts)
    {
        if (!_dupGuard.ShouldEmit($"{name}:{kind}", ts))
            return;

        var metric = new Metric(name, kind, value, ts);
        foreach (var exp in _exporters)
        {
            try
            {
                exp.Export(metric);
            }
            catch (Exception ex) { Console.WriteLine($"[Norr.Perf] Exporter error: {ex.Message}"); }
        }
    }

    private sealed class Scope : IPerformanceScope
    {
        private readonly Monitor _pm;
        private readonly IReadOnlyList<IAlertSink> _alerts;
        private readonly AlertOptions _alertOpts;
        private readonly CpuOptions _cpuOpts;
        private readonly MetricsOptions _metricsOpts;
        private readonly IThreadCpuTimeProvider _cpuProvider;

        private readonly string _name;
        private readonly string _category;
        private readonly Activity? _act;

        private readonly long _startAlloc;
        private readonly TimeSpan _startCpuThread;
        private readonly TimeSpan _startCpuProcess;
        private readonly bool _threadCpuEnabled;
        private readonly int _startActive;
        private readonly ILogger<Monitor>? _logger;

        internal Scope(Monitor pm, string name)
        {
            _pm = pm;
            _alerts = pm._alerts;
            _alertOpts = pm._alertOpts;
            _cpuOpts = pm._cpuOpts;
            _metricsOpts = pm._metricsOpts;
            _cpuProvider = pm._cpuProvider;
            _name = name;
            _category = Categorize(name);

            _logger = pm._logger;

            _act = _activitySource.StartActivity(name, ActivityKind.Internal);
            _startAlloc = GC.GetAllocatedBytesForCurrentThread();
            _startActive = Interlocked.Increment(ref _activeScopes);

            if (_cpuOpts.Mode == CpuMeasureMode.ThreadTime && _cpuProvider.IsSupported)
            {
                _threadCpuEnabled = true;
                _startCpuThread = _cpuProvider.GetCurrentThreadCpuTime();
            }
            else if (_cpuOpts.Mode == CpuMeasureMode.ProcessApproximate)
            {
                _startCpuProcess = Process.GetCurrentProcess().TotalProcessorTime;
            }
        }

        public void Dispose()
        {
            try
            {
                _act?.Stop();

                var now = DateTime.UtcNow;
                var elapsed = (_act?.Duration.TotalMilliseconds) ?? 0.0;
                var alloc = GC.GetAllocatedBytesForCurrentThread() - _startAlloc;

                double cpuMs = 0.0;
                string cpuModeTag = "disabled";
                bool approx = false;

                if (_cpuOpts.Mode == CpuMeasureMode.ThreadTime && _threadCpuEnabled)
                {
                    var end = _cpuProvider.GetCurrentThreadCpuTime();
                    var dt = end - _startCpuThread;
                    if (dt.Ticks < 0)
                        dt = TimeSpan.Zero;
                    cpuMs = dt.TotalMilliseconds;
                    cpuModeTag = "thread";
                }
                else if (_cpuOpts.Mode == CpuMeasureMode.ProcessApproximate)
                {
                    var end = Process.GetCurrentProcess().TotalProcessorTime;
                    var dt = end - _startCpuProcess;
                    if (dt.Ticks < 0)
                        dt = TimeSpan.Zero;

                    var endActive = Volatile.Read(ref _activeScopes);
                    int concurrency = Math.Max(_startActive, endActive);
                    cpuMs = concurrency <= 1 ? dt.TotalMilliseconds : dt.TotalMilliseconds / concurrency;
                    approx = concurrency > 1;

                    if (cpuMs > elapsed)
                        cpuMs = elapsed;
                    cpuModeTag = "process_approx";
                }

                var tags = BuildTags(_pm._metricsOpts, _name, _category, cpuModeTag, approx);

                _pm._duration.Record(elapsed, tags);
                _pm._alloc.Record(alloc, tags);

                if (cpuModeTag != "disabled")
                {
                    _pm._cpuMs.Record(cpuMs, tags);

                    if (_pm._cpuOpts.RecordPercentOfElapsed && elapsed > 0)
                    {
                        var pct = 100.0 * (cpuMs / elapsed);
                        _pm._cpuPct.Record(pct, tags);
                    }

                    if (_pm._cpuOpts.RecordPercentNormalizedToCores && elapsed > 0)
                    {
                        var cores = Math.Max(1, Environment.ProcessorCount);
                        var pctn = 100.0 * (cpuMs / (elapsed * cores));
                        _pm._cpuPctNorm.Record(pctn, tags);
                    }

                    if (_pm._cpuOpts.RecordCycles && _cpuProvider.TryGetThreadCycleCount(out var cyclesU64))
                    {
                        long cycles = cyclesU64 > long.MaxValue ? long.MaxValue : (long)cyclesU64;
                        if (cycles < 0)
                            cycles = 0;
                        _pm._cpuCycles.Record(cycles, tags);
                    }
                }

                _pm.Emit(_name, MetricKind.DurationMs, elapsed, now);
                _pm.Emit(_name, MetricKind.AllocBytes, alloc, now);
                if (cpuModeTag != "disabled")
                    _pm.Emit(_name, MetricKind.CpuMs, cpuMs, now);

                PerfCoreCounters.Record(elapsed, cpuMs, alloc);

                _ = CheckAlertAsync(MetricKind.DurationMs, elapsed, _alertOpts.DurationMs);
                if (cpuModeTag != "disabled")
                    _ = CheckAlertAsync(MetricKind.CpuMs, cpuMs, _alertOpts.CpuMs);
                _ = CheckAlertAsync(MetricKind.AllocBytes, alloc, _alertOpts.AllocBytes);
            }
            finally
            {
                Interlocked.Decrement(ref _activeScopes);
            }
        }


        private static TagList BuildTags(MetricsOptions opt, string name, string category, string cpuMode, bool approx)
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
            tl.Add("norr.cpu.mode", cpuMode);
            if (approx)
                tl.Add("norr.cpu.approx", true);

            if (opt.IncludeThreadId)
                tl.Add("norr.thread.id", Environment.CurrentManagedThreadId);

            return tl;
        }

        private static string Categorize(string name)
        {
            if (name.StartsWith("HTTP ", StringComparison.Ordinal))
                return "http";
            if (name.StartsWith("MediatR ", StringComparison.Ordinal))
                return "mediatr";
            if (name.StartsWith("Consumer ", StringComparison.Ordinal))
                return "messaging";
            if (name.StartsWith("BGService ", StringComparison.Ordinal))
                return "background";
            return "custom";
        }

        private Task CheckAlertAsync(MetricKind kind, double value, double? threshold)
        {
            if (threshold is null || value <= threshold.Value)
                return Task.CompletedTask;

            var alert = new PerfAlert(_name, kind, value, threshold.Value);
            return Task.WhenAll(_alerts.Select(sink =>
                sink.SendAsync(alert).ContinueWith(t =>
                {
                    if (t.Exception is not null)
                    {
                        _logger?.PM().SendFailure(1, t.Exception.InnerException ?? t.Exception);
                        Console.WriteLine($"Alert sink err: {t.Exception.InnerException?.Message}");
                    }
                })));
        }
    }

    private sealed class NoopScope : IPerformanceScope
    {
        public static readonly NoopScope Instance = new();
        public void Dispose()
        {
        }
    }
}
