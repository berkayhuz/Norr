// -----------------------------------------------------------------------------
//  Copyright (c) Norr
//  Licensed under the MIT license.
// -----------------------------------------------------------------------------

#nullable enable 

using System.Globalization;
using System.Text;

using Norr.PerformanceMonitor.Core.Metrics.Aggregation;
using Norr.PerformanceMonitor.Telemetry.Prometheus;

namespace Norr.PerformanceMonitor.Integrations.AspNetCore;

/// <summary>
/// Minimal, allocation‑friendly Prometheus text exposition (<c>text/plain; version=0.0.4</c>)
/// serializer for metrics aggregated by the Norr Performance Monitor.
/// </summary>
/// <remarks>
/// <para>
/// This serializer writes metric families to a provided <see cref="StringBuilder"/> in the
/// Prometheus <em>text</em> exposition format. It currently supports
/// <b>histograms</b> and <b>summaries</b> produced by the in‑process aggregators:
/// <see cref="HistogramAggregator"/> and <see cref="SummaryAggregator"/>.
/// </para>
/// <para>
/// The serializer is intentionally small and dependency‑free so it can be used in
/// ASP.NET Core middleware, export endpoints, or self‑hosted diagnostics pages.
/// It performs no I/O itself—callers own buffering and response writing.
/// </para>
/// <para><b>Thread‑safety:</b> The type is stateless and thread‑safe. Callers must ensure
/// the provided <see cref="StringBuilder"/> is not shared concurrently.</para>
/// <para><b>Cardinality:</b> Metric names are <em>sanitized</em> to comply with Prometheus
/// rules (<c>[a-zA-Z_:][a-zA-Z0-9_:]*</c>). Any disallowed characters are replaced with
/// an underscore (<c>_</c>).</para>
/// <para>
/// <b>Format notes:</b> For histograms we emit:
/// <list type="bullet">
///   <item><description><c>&lt;name&gt;_bucket{le="&lt;bound&gt;"} &lt;cumulative count&gt;</c> for each bucket.</description></item>
///   <item><description><c>&lt;name&gt;_count &lt;total observations&gt;</c></description></item>
///   <item><description><c>&lt;name&gt;_sum &lt;sum of observations&gt;</c></description></item>
/// </list>
/// For summaries we emit:
/// <list type="bullet">
///   <item><description><c>&lt;name&gt;{quantile="0.5"} &lt;p50&gt;</c>, <c>0.9</c>, <c>0.95</c>, <c>0.99</c> (skipping <c>NaN</c> values)</description></item>
///   <item><description><c>&lt;name&gt;_count &lt;total observations&gt;</c></description></item>
///   <item><description><c>&lt;name&gt;_sum &lt;sum of observations&gt;</c></description></item>
/// </list>
/// </para>
/// <para>
/// <b>HELP/TYPE headers:</b> The serializer emits a <c># TYPE</c> line for each metric
/// family. If you also want a <c># HELP</c> line, emit it in your calling layer, where
/// you have access to richer descriptions.
/// </para>
/// <para>
/// <b>Labels:</b> This serializer writes families without additional labels. If you need
/// labels (e.g., <c>instance</c>, <c>job</c>, or custom dimensions), consider enriching at
/// aggregation time or extending the writer to accept label sets.
/// </para>
/// <example>
/// Example histogram output:
/// <code language="text">
/// # TYPE http_request_duration_seconds histogram
/// http_request_duration_seconds_bucket{le="0.1"} 42
/// http_request_duration_seconds_bucket{le="0.5"} 420
/// http_request_duration_seconds_bucket{le="1"} 900
/// http_request_duration_seconds_bucket{le="+Inf"} 1000
/// http_request_duration_seconds_count 1000
/// http_request_duration_seconds_sum 123.456
/// </code>
///
/// Example summary output:
/// <code language="text">
/// # TYPE queue_latency_seconds summary
/// queue_latency_seconds{quantile="0.5"} 0.012
/// queue_latency_seconds{quantile="0.9"} 0.024
/// queue_latency_seconds{quantile="0.95"} 0.030
/// queue_latency_seconds{quantile="0.99"} 0.050
/// queue_latency_seconds_count 12345
/// queue_latency_seconds_sum 234.567
/// </code>
/// </example>
/// </remarks>
internal static class PrometheusTextSerializer
{
    /// <summary>Registry'deki tüm metrikleri Prometheus formatında yazar.</summary>
    public static void WriteTo(TextWriter w, NorrMetricsRegistry r)
    {
        // =========================
        // EXISTING METRICS (IO/CORE)
        // =========================

        // IO totals
        HelpType(w, "norr_io_request_bytes_total", "Total request bytes (process-wide).", "counter");
        Counter(w, "norr_io_request_bytes_total", r.IoRequestBytesTotal);

        HelpType(w, "norr_io_response_bytes_total", "Total response bytes (process-wide).", "counter");
        Counter(w, "norr_io_response_bytes_total", r.IoResponseBytesTotal);

        // IO last
        HelpType(w, "norr_io_request_bytes_last", "Last observed request bytes.", "gauge");
        Gauge(w, "norr_io_request_bytes_last", r.IoRequestBytesLast);

        HelpType(w, "norr_io_response_bytes_last", "Last observed response bytes.", "gauge");
        Gauge(w, "norr_io_response_bytes_last", r.IoResponseBytesLast);

        // CORE totals
        HelpType(w, "norr_duration_ms_total", "Total duration milliseconds (process-wide).", "counter");
        Counter(w, "norr_duration_ms_total", r.DurationMsTotal);

        HelpType(w, "norr_cpu_ms_total", "Total CPU milliseconds (process-wide).", "counter");
        Counter(w, "norr_cpu_ms_total", r.CpuMsTotal);

        HelpType(w, "norr_alloc_bytes_total", "Total allocated bytes (process-wide).", "counter");
        Counter(w, "norr_alloc_bytes_total", r.AllocBytesTotal);

        // CORE last
        HelpType(w, "norr_duration_ms_last", "Last observed duration milliseconds.", "gauge");
        Gauge(w, "norr_duration_ms_last", r.DurationMsLast);

        HelpType(w, "norr_cpu_ms_last", "Last observed CPU milliseconds.", "gauge");
        Gauge(w, "norr_cpu_ms_last", r.CpuMsLast);

        HelpType(w, "norr_alloc_bytes_last", "Last observed allocated bytes.", "gauge");
        Gauge(w, "norr_alloc_bytes_last", r.AllocBytesLast);

        // =========================
        // NEW METRICS
        // =========================

        // GC
        HelpType(w, "norr_gc_heap_bytes", "GC heap size in bytes.", "gauge");
        Gauge(w, "norr_gc_heap_bytes", r.GcHeapSizeBytes);

        HelpType(w, "norr_gc_time_in_gc_percent", "Percentage of time spent in GC.", "gauge");
        Gauge(w, "norr_gc_time_in_gc_percent", r.GcTimeInGcPercent);

        HelpType(w, "norr_gc_fragmentation_percent", "GC heap fragmentation percent.", "gauge");
        Gauge(w, "norr_gc_fragmentation_percent", r.GcFragmentationPercent);

        HelpType(w, "norr_gc_committed_bytes", "Committed bytes for GC.", "gauge");
        Gauge(w, "norr_gc_committed_bytes", r.GcCommittedBytes);

        HelpType(w, "norr_gc_pause_seconds_total", "Total GC pause time in seconds.", "counter");
        Counter(w, "norr_gc_pause_seconds_total", r.GcPauseSecondsTotal);

        HelpType(w, "norr_gc_gen0_total", "Total Gen 0 collections.", "counter");
        Counter(w, "norr_gc_gen0_total", r.Gen0Total);

        HelpType(w, "norr_gc_gen1_total", "Total Gen 1 collections.", "counter");
        Counter(w, "norr_gc_gen1_total", r.Gen1Total);

        HelpType(w, "norr_gc_gen2_total", "Total Gen 2 collections.", "counter");
        Counter(w, "norr_gc_gen2_total", r.Gen2Total);

        HelpType(w, "norr_gc_loh_bytes", "Large Object Heap size in bytes.", "gauge");
        Gauge(w, "norr_gc_loh_bytes", r.LohBytes);

        HelpType(w, "norr_gc_poh_bytes", "Pinned Object Heap size in bytes.", "gauge");
        Gauge(w, "norr_gc_poh_bytes", r.PohBytes);

        // ThreadPool & Lock
        HelpType(w, "norr_threadpool_queue_length", "ThreadPool global queue length.", "gauge");
        Gauge(w, "norr_threadpool_queue_length", r.ThreadPoolQueueLength);

        HelpType(w, "norr_threadpool_thread_count", "ThreadPool thread count.", "gauge");
        Gauge(w, "norr_threadpool_thread_count", r.ThreadPoolThreadCount);

        HelpType(w, "norr_threadpool_completed_items_total", "Total completed ThreadPool work items.", "counter");
        Counter(w, "norr_threadpool_completed_items_total", r.ThreadPoolCompletedItemsTotal);

        HelpType(w, "norr_monitor_lock_contention_total", "Total Monitor lock contentions.", "counter");
        Counter(w, "norr_monitor_lock_contention_total", r.MonitorLockContentionTotal);

        // Process (Linux)
        HelpType(w, "norr_process_socket_count", "Current open socket descriptor count (Linux).", "gauge");
        GaugeOptional(w, "norr_process_socket_count", r.ProcessSocketCount);

        HelpType(w, "norr_process_fd_count", "Current open file descriptor count (Linux).", "gauge");
        GaugeOptional(w, "norr_process_fd_count", r.ProcessFdCount);

        HelpType(w, "norr_process_voluntary_context_switches_total", "Voluntary context switches (Linux).", "counter");
        CounterOptional(w, "norr_process_voluntary_context_switches_total", r.CtxSwitchVoluntaryTotal);

        HelpType(w, "norr_process_nonvoluntary_context_switches_total", "Non-voluntary context switches (Linux).", "counter");
        CounterOptional(w, "norr_process_nonvoluntary_context_switches_total", r.CtxSwitchNonVoluntaryTotal);
    }

    /// <summary>
    /// Writes all known metric families from the current <see cref="AggregationRegistry"/>
    /// snapshot to <paramref name="sb"/> in Prometheus text format.
    /// </summary>
    public static void WriteMetrics(StringBuilder sb)
    {
        foreach (var (name, agg) in AggregationRegistry.Snapshot())
        {
            switch (agg)
            {
                case HistogramAggregator h:
                    WriteHistogram(sb, name, h);
                    break;
                case SummaryAggregator s:
                    WriteSummary(sb, name, s);
                    break;
            }
        }
    }

    /// <summary>
    /// Serializes a histogram family according to Prometheus conventions.
    /// </summary>
    /// <param name="sb">The output buffer to append to.</param>
    /// <param name="name">The aggregator key (will be sanitized to a Prometheus metric name).</param>
    /// <param name="h">The <see cref="HistogramAggregator"/> to serialize.</param>
    /// <remarks>
    /// Emits a single <c># TYPE &lt;metric&gt; histogram</c> header, then bucket lines with
    /// cumulative counts, followed by <c>_count</c> and <c>_sum</c>.
    /// The final bucket uses <c>le="+Inf"</c> as required by the format.
    /// </remarks>
    private static void WriteHistogram(StringBuilder sb, string name, HistogramAggregator h)
    {
        var (metric, kind) = Split(name);
        sb.AppendLine($"# TYPE {metric} histogram");
        var bounds = h.Bounds;
        var buckets = h.Buckets;
        long cum = 0;
        for (int i = 0; i < buckets.Length; i++)
        {
            cum += buckets[i];
            var le = i < bounds.Length ? bounds[i].ToString(CultureInfo.InvariantCulture) : "+Inf";
            sb.Append(metric).Append("_bucket{le=\"").Append(le).Append("\"} ").Append(cum).Append('\n');
        }
        sb.Append(metric).Append("_count ").Append(h.Count).Append('\n');
        sb.Append(metric).Append("_sum ").Append(Invariant(h.Sum)).Append('\n');
    }

    /// <summary>
    /// Serializes a summary family, emitting selected quantiles (p50, p90, p95, p99),
    /// and the total <c>_count</c> and <c>_sum</c>.
    /// </summary>
    /// <param name="sb">The output buffer to append to.</param>
    /// <param name="name">The aggregator key (will be sanitized to a Prometheus metric name).</param>
    /// <param name="s">The <see cref="SummaryAggregator"/> to serialize.</param>
    /// <remarks>
    /// Any quantile whose value is <see cref="double.NaN"/> is skipped to avoid emitting
    /// invalid samples.
    /// </remarks>
    private static void WriteSummary(StringBuilder sb, string name, SummaryAggregator s)
    {
        var (metric, kind) = Split(name);
        sb.AppendLine($"# TYPE {metric} summary");
        var snap = s.Snapshot();
        WriteQuantile(sb, metric, "0.5", snap.p50);
        WriteQuantile(sb, metric, "0.9", snap.p90);
        WriteQuantile(sb, metric, "0.95", snap.p95);
        WriteQuantile(sb, metric, "0.99", snap.p99);
        sb.Append(metric).Append("_count ").Append(snap.count).Append('\n');
        sb.Append(metric).Append("_sum ").Append(Invariant(snap.sum)).Append('\n');
    }

    /// <summary>
    /// Writes a single quantile sample line if the value is a valid number.
    /// </summary>
    /// <param name="sb">The output buffer.</param>
    /// <param name="metric">Base metric name.</param>
    /// <param name="q">Quantile label value (e.g., <c>"0.95"</c>).</param>
    /// <param name="v">Quantile value.</param>
    private static void WriteQuantile(StringBuilder sb, string metric, string q, double v)
    {
        if (double.IsNaN(v))
            return;
        sb.Append(metric).Append("{quantile=\"").Append(q).Append("\"} ").Append(Invariant(v)).Append('\n');
    }

    /// <summary>
    /// Splits a composite aggregator key into a Prometheus‑safe metric name and a secondary
    /// component (e.g., a kind or unit), using the last colon (<c>:</c>) as a separator.
    /// </summary>
    /// <param name="name">The original aggregator key.</param>
    /// <returns>
    /// A tuple of <c>(metric, kind)</c> where <c>metric</c> is sanitized to meet Prometheus
    /// naming rules. If no separator is found, <c>kind</c> is an empty string.
    /// </returns>
    /// <remarks>
    /// The returned <c>kind</c> is currently not appended as a label; it is preserved to
    /// enable future enrichment scenarios at the call site.
    /// </remarks>
    private static (string metric, string kind) Split(string name)
    {
        var idx = name.LastIndexOf(':');
        if (idx < 0)
            return (Sanitize(name), "");
        return (Sanitize(name.Substring(0, idx)), Sanitize(name[(idx + 1)..]));
    }

    /// <summary>
    /// Converts an arbitrary string into a Prometheus‑compatible metric identifier by
    /// replacing disallowed characters with an underscore (<c>_</c>).
    /// </summary>
    /// <param name="s">The raw metric name.</param>
    /// <returns>A sanitized metric name that matches <c>[a-zA-Z_:][a-zA-Z0-9_:]*</c>.</returns>
    private static string Sanitize(string s)
    {
        // Prometheus metric name rules: [a-zA-Z_:][a-zA-Z0-9_:]*
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if ((ch >= 'a' && ch <= 'z') ||
                (ch >= 'A' && ch <= 'Z') ||
                (ch >= '0' && ch <= '9') ||
                ch == '_' || ch == ':')
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append('_');
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Formats a <see cref="double"/> using the invariant culture to ensure a portable
    /// representation for Prometheus ingestion.
    /// </summary>
    /// <param name="d">The numeric value.</param>
    /// <returns>Invariant string form (e.g., dot as decimal separator).</returns>
    private static string Invariant(double d) => d.ToString(CultureInfo.InvariantCulture);

    private static void HelpType(TextWriter w, string name, string help, string type)
    {
        w.Write("# HELP ");
        w.Write(name);
        w.Write(' ');
        w.WriteLine(help);
        w.Write("# TYPE ");
        w.Write(name);
        w.Write(' ');
        w.WriteLine(type);
    }

    private static void Gauge(TextWriter w, string name, double value)
    {
        w.Write(name);
        w.Write(' ');
        w.Write(value.ToString(CultureInfo.InvariantCulture));
        w.Write('\n');
    }

    private static void Gauge(TextWriter w, string name, long value) =>
        Gauge(w, name, (double)value);

    private static void Counter(TextWriter w, string name, double value) =>
        Gauge(w, name, value);

    private static void Counter(TextWriter w, string name, long value) =>
        Gauge(w, name, (double)value);

    private static void GaugeOptional(TextWriter w, string name, long? value)
    {
        if (value.HasValue)
            Gauge(w, name, value.Value);
    }

    private static void CounterOptional(TextWriter w, string name, long? value)
    {
        if (value.HasValue)
            Counter(w, name, value.Value);
    }
}
