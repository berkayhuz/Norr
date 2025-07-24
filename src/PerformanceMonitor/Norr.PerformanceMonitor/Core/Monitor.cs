using System.Diagnostics;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.Options;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Alerting;
using Norr.PerformanceMonitor.Configuration;
using Norr.PerformanceMonitor.Core.Metrics;
using Norr.PerformanceMonitor.Sampling;

namespace Norr.PerformanceMonitor.Core;

/// <summary>
/// Central orchestrator that measures an operation’s execution time, CPU usage
/// and memory allocations, applies sampling / duplicate-guard logic and finally
/// dispatches the resulting <see cref="Metric"/> instances to the configured
/// exporters and alert sinks.
/// </summary>
public sealed class Monitor : IMonitor
{
    // ---- OpenTelemetry histograms -------------------------------------------------------------

    private readonly Meter _meter = new("Norr.PerformanceMonitor");
    private readonly Histogram<double> _duration;
    private readonly Histogram<long> _alloc;
    private readonly Histogram<double> _cpu;

    // ---- Dependencies ------------------------------------------------------------------------

    private readonly ISampler _sampler;
    private readonly IDuplicateGuard _dupGuard;
    private readonly IReadOnlyList<IMetricExporter> _exporters;
    private readonly IReadOnlyList<IAlertSink> _alerts;
    private readonly PerformanceOptions _opts;
    private readonly AlertOptions _alertOpts;

    // ------------------------------------------------------------------------------------------
    #region ctor

    /// <summary>
    /// Creates a <see cref="Monitor"/> instance. Normally resolved by DI via
    /// <c>AddPerformanceMonitoring()</c>.
    /// </summary>
    /// <param name="opts">Application-wide monitoring options.</param>
    /// <param name="exporters">The metric exporters to forward data to.</param>
    /// <param name="alerts">Alert sinks that will receive threshold breaches.</param>
    /// <param name="sampler">
    /// Optional override for the default <see cref="ProbabilitySampler"/>.
    /// </param>
    /// <param name="dupGuard">
    /// Optional override for the default <see cref="BloomDuplicateGuard"/>.
    /// </param>
    public Monitor(
        IOptions<PerformanceOptions> opts,
        IEnumerable<IMetricExporter> exporters,
        IEnumerable<IAlertSink> alerts,
        ISampler? sampler = null,
        IDuplicateGuard? dupGuard = null)
    {
        _opts = opts.Value;
        _alertOpts = _opts.Alerts;
        _exporters = exporters.ToList();
        _alerts = alerts.ToList();
        _sampler = sampler ?? new ProbabilitySampler(_opts.Sampling);
        _dupGuard = dupGuard ?? new BloomDuplicateGuard(_opts.DuplicateGuard);

        _duration = _meter.CreateHistogram<double>("method.duration.ms");
        _alloc = _meter.CreateHistogram<long>("method.alloc.bytes");
        _cpu = _meter.CreateHistogram<double>("method.cpu.ms");
    }

    #endregion
    // ------------------------------------------------------------------------------------------

    /// <inheritdoc />
    public IPerformanceScope Begin(string name)
    {
        if (!_sampler.ShouldSample(name) ||
            _opts.IgnoredNames.Contains(name))
            return NoopScope.Instance;

        return new Scope(this, name);
    }

    // ---- Metric dispatch with duplicate guard -------------------------------------------------

    private void Emit(string name, MetricKind kind, double value, DateTime ts)
    {
        if (!_dupGuard.ShouldEmit($"{name}:{kind}", ts))
            return;

        var metric = new Metric(name, kind, value, ts);
        foreach (var exp in _exporters)
            exp.Export(metric);
    }

    // ------------------------------------------------------------------------------------------
    #region inner Scope

    /// <summary>
    /// Disposable object returned by <see cref="Begin"/>; measures the lifetime
    /// of a single operation.
    /// </summary>
    private sealed class Scope : IPerformanceScope
    {
        private readonly Monitor _pm;
        private readonly IReadOnlyList<IAlertSink> _alerts;
        private readonly AlertOptions _alertOpts;
        private readonly string _name;
        private readonly Activity _act;
        private readonly long _startAlloc;
        private readonly TimeSpan _startCpu;

        internal Scope(Monitor pm, string name)
        {
            _pm = pm;
            _alerts = pm._alerts;
            _alertOpts = pm._alertOpts;
            _name = name;

            _act = new Activity(name).Start();
            _startAlloc = GC.GetAllocatedBytesForCurrentThread();
            _startCpu = Process.GetCurrentProcess().TotalProcessorTime;
        }

        /// <summary>
        /// Completes the measurement, records histograms and triggers alerts
        /// (if thresholds were exceeded).
        /// </summary>
        public void Dispose()
        {
            _act.Stop(); // ensure Activity.Duration is populated

            var now = DateTime.UtcNow;
            var elapsed = _act.Duration.TotalMilliseconds;
            var cpuMs = (Process.GetCurrentProcess().TotalProcessorTime - _startCpu).TotalMilliseconds;
            var alloc = GC.GetAllocatedBytesForCurrentThread() - _startAlloc;

            // OpenTelemetry histograms
            _pm._duration.Record(elapsed);
            _pm._cpu.Record(cpuMs);
            _pm._alloc.Record(alloc);

            // Custom exporters
            _pm.Emit(_name, MetricKind.DurationMs, elapsed, now);
            _pm.Emit(_name, MetricKind.CpuMs, cpuMs, now);
            _pm.Emit(_name, MetricKind.AllocBytes, alloc, now);

            // Threshold-based alerts
            _ = CheckAlertAsync(MetricKind.DurationMs, elapsed, _alertOpts.DurationMs);
            _ = CheckAlertAsync(MetricKind.CpuMs, cpuMs, _alertOpts.CpuMs);
            _ = CheckAlertAsync(MetricKind.AllocBytes, alloc, _alertOpts.AllocBytes);
        }

        // -- helpers ---------------------------------------------------------------------------

        private Task CheckAlertAsync(MetricKind kind, double value, double? threshold)
        {
            if (threshold is null || value <= threshold.Value)
                return Task.CompletedTask;

            var alert = new PerfAlert(_name, kind, value, threshold.Value);

            // Fire-and-forget; swallow any sink exceptions.
            return Task.WhenAll(_alerts.Select(sink =>
                sink.SendAsync(alert).ContinueWith(t =>
                {
                    if (t.Exception is not null)
                        Console.WriteLine($"Alert sink err: {t.Exception.InnerException?.Message}");
                })));
        }
    }

    #endregion
    // ------------------------------------------------------------------------------------------

    private sealed class NoopScope : IPerformanceScope
    {
        public static readonly NoopScope Instance = new();
        public void Dispose()
        { /* intentionally blank */
        }
    }
}
