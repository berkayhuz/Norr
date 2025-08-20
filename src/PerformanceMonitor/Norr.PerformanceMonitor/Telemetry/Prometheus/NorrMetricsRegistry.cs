// -----------------------------------------------------------------------------
//  Copyright (c) Norr
//  Licensed under the MIT license.
// -----------------------------------------------------------------------------

#nullable enable

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;

using Norr.PerformanceMonitor.Integrations.AspNetCore;

namespace Norr.PerformanceMonitor.Telemetry.Prometheus;

/// <summary>
/// Prometheus metrik deposu: IO + Core (duration/cpu/alloc) + GC + ThreadPool/Lock + Process.
/// Dinleyiciler ve probeler bu sınıfın setter'larını çağırır; yazdırma <see cref="PrometheusTextSerializer"/> ile yapılır.
/// </summary>
internal sealed class NorrMetricsRegistry
{
    // ---- IO counters/gauges ----
    private double _reqBytesTotal, _respBytesTotal;
    private double _reqBytesLast, _respBytesLast;

    // ---- Core counters/gauges ----
    private double _durationMsTotal, _cpuMsTotal, _allocBytesTotal;
    private double _durationMsLast, _cpuMsLast, _allocBytesLast;

    // ---- GC ----
    private long _gcHeapSizeBytes;
    private double _gcTimeInGcPercent;
    private double _gcFragmentationPercent;
    private long _gcCommittedBytes;
    private long _gen0Total, _gen1Total, _gen2Total;
    private double _gcPauseSecondsTotal;
    private long _lohBytes, _pohBytes;

    // ---- ThreadPool & Lock ----
    private long _threadPoolQueueLength;
    private long _threadPoolThreadCount;
    private long _threadPoolCompletedItemsTotal;
    private long _monitorLockContentionTotal;

    // ---- Process (Linux) ----
    private long? _processSocketCount;
    private long? _processFdCount;
    private long? _ctxSwitchVoluntaryTotal;
    private long? _ctxSwitchNonVoluntaryTotal;

    // ---------------------------
    // Public getters (serializer bunları okur)
    // ---------------------------

    // IO
    public double IoRequestBytesTotal => Volatile.Read(ref _reqBytesTotal);
    public double IoResponseBytesTotal => Volatile.Read(ref _respBytesTotal);
    public double IoRequestBytesLast => Volatile.Read(ref _reqBytesLast);
    public double IoResponseBytesLast => Volatile.Read(ref _respBytesLast);

    // Core
    public double DurationMsTotal => Volatile.Read(ref _durationMsTotal);
    public double CpuMsTotal => Volatile.Read(ref _cpuMsTotal);
    public double AllocBytesTotal => Volatile.Read(ref _allocBytesTotal);

    public double DurationMsLast => Volatile.Read(ref _durationMsLast);
    public double CpuMsLast => Volatile.Read(ref _cpuMsLast);
    public double AllocBytesLast => Volatile.Read(ref _allocBytesLast);

    // GC
    public long GcHeapSizeBytes => _gcHeapSizeBytes;
    public double GcTimeInGcPercent => _gcTimeInGcPercent;
    public double GcFragmentationPercent => _gcFragmentationPercent;
    public long GcCommittedBytes => _gcCommittedBytes;
    public long Gen0Total => _gen0Total;
    public long Gen1Total => _gen1Total;
    public long Gen2Total => _gen2Total;
    public double GcPauseSecondsTotal => _gcPauseSecondsTotal;
    public long LohBytes => _lohBytes;
    public long PohBytes => _pohBytes;

    // ThreadPool & Lock
    public long ThreadPoolQueueLength => _threadPoolQueueLength;
    public long ThreadPoolThreadCount => _threadPoolThreadCount;
    public long ThreadPoolCompletedItemsTotal => _threadPoolCompletedItemsTotal;
    public long MonitorLockContentionTotal => _monitorLockContentionTotal;

    // Process (Linux)
    public long? ProcessSocketCount => _processSocketCount;
    public long? ProcessFdCount => _processFdCount;
    public long? CtxSwitchVoluntaryTotal => _ctxSwitchVoluntaryTotal;
    public long? CtxSwitchNonVoluntaryTotal => _ctxSwitchNonVoluntaryTotal;

    // ---------------------------
    // Mutators (dinleyiciler/probeler için)
    // ---------------------------

    // IO
    public void AddRequestBytes(double inc)
    {
        if (inc > 0)
            Interlocked.Exchange(ref _reqBytesTotal, _reqBytesTotal + inc);
    }
    public void AddResponseBytes(double inc)
    {
        if (inc > 0)
            Interlocked.Exchange(ref _respBytesTotal, _respBytesTotal + inc);
    }
    public void SetRequestLast(double v) => Interlocked.Exchange(ref _reqBytesLast, v);
    public void SetResponseLast(double v) => Interlocked.Exchange(ref _respBytesLast, v);

    // Core
    public void AddDurationMs(double inc)
    {
        if (inc > 0)
            Interlocked.Exchange(ref _durationMsTotal, _durationMsTotal + inc);
    }
    public void AddCpuMs(double inc)
    {
        if (inc > 0)
            Interlocked.Exchange(ref _cpuMsTotal, _cpuMsTotal + inc);
    }
    public void AddAllocBytes(double inc)
    {
        if (inc > 0)
            Interlocked.Exchange(ref _allocBytesTotal, _allocBytesTotal + inc);
    }
    public void SetDurationLast(double v) => Interlocked.Exchange(ref _durationMsLast, v);
    public void SetCpuLast(double v) => Interlocked.Exchange(ref _cpuMsLast, v);
    public void SetAllocLast(double v) => Interlocked.Exchange(ref _allocBytesLast, v);

    // GC
    public void SetGcHeapSizeBytes(long v) => _gcHeapSizeBytes = v;
    public void SetGcTimeInGcPercent(double v) => _gcTimeInGcPercent = v;
    public void SetGcFragmentationPercent(double v) => _gcFragmentationPercent = v;
    public void SetGcCommittedBytes(long v) => _gcCommittedBytes = v;

    public void AddGen0(long d) => Interlocked.Add(ref _gen0Total, d);
    public void AddGen1(long d) => Interlocked.Add(ref _gen1Total, d);
    public void AddGen2(long d) => Interlocked.Add(ref _gen2Total, d);

    public void AddGcPauseSeconds(double d)
    {
        // double için atomik toplama (bit seviyesinde)
        long deltaBits = BitConverter.DoubleToInt64Bits(d);
        Interlocked.Add(ref Unsafe.As<double, long>(ref _gcPauseSecondsTotal), deltaBits);
    }

    public void SetLohBytes(long v) => _lohBytes = v;
    public void SetPohBytes(long v) => _pohBytes = v;

    // ThreadPool & Lock
    public void SetThreadPoolQueueLength(long v) => _threadPoolQueueLength = v;
    public void SetThreadPoolThreadCount(long v) => _threadPoolThreadCount = v;
    public void AddThreadPoolCompleted(long d) => Interlocked.Add(ref _threadPoolCompletedItemsTotal, d);
    public void AddMonitorLockContention(long d) => Interlocked.Add(ref _monitorLockContentionTotal, d);

    // Process (Linux)
    public void SetProcessSocketCount(long? v) => _processSocketCount = v;
    public void SetProcessFdCount(long? v) => _processFdCount = v;
    public void SetContextSwitchTotals(long? vol, long? nvol)
    {
        _ctxSwitchVoluntaryTotal = vol;
        _ctxSwitchNonVoluntaryTotal = nvol;
    }

    // -------------------------------------------------------------
    // (Opsiyonel) var olan Export() korunacaksa, mevcut davranışıyla
    // bırakılabilir. Tercihen PrometheusTextSerializer kullanılmalı.
    // -------------------------------------------------------------
    public string Export()
    {
        var inv = CultureInfo.InvariantCulture;
        return string.Join('\n', new[]
        {
            "# HELP norr_io_request_bytes_total Total request bytes (process-wide).",
            "# TYPE norr_io_request_bytes_total counter",
            $"norr_io_request_bytes_total {IoRequestBytesTotal.ToString(inv)}",

            "# HELP norr_io_response_bytes_total Total response bytes (process-wide).",
            "# TYPE norr_io_response_bytes_total counter",
            $"norr_io_response_bytes_total {IoResponseBytesTotal.ToString(inv)}",

            "# HELP norr_io_request_bytes_last Last observed request bytes.",
            "# TYPE norr_io_request_bytes_last gauge",
            $"norr_io_request_bytes_last {IoRequestBytesLast.ToString(inv)}",

            "# HELP norr_io_response_bytes_last Last observed response bytes.",
            "# TYPE norr_io_response_bytes_last gauge",
            $"norr_io_response_bytes_last {IoResponseBytesLast.ToString(inv)}",

            "# HELP norr_duration_ms_total Total duration milliseconds (process-wide).",
            "# TYPE norr_duration_ms_total counter",
            $"norr_duration_ms_total {DurationMsTotal.ToString(inv)}",

            "# HELP norr_cpu_ms_total Total CPU milliseconds (process-wide).",
            "# TYPE norr_cpu_ms_total counter",
            $"norr_cpu_ms_total {CpuMsTotal.ToString(inv)}",

            "# HELP norr_alloc_bytes_total Total allocated bytes (process-wide).",
            "# TYPE norr_alloc_bytes_total counter",
            $"norr_alloc_bytes_total {AllocBytesTotal.ToString(inv)}",

            "# HELP norr_duration_ms_last Last observed duration milliseconds.",
            "# TYPE norr_duration_ms_last gauge",
            $"norr_duration_ms_last {DurationMsLast.ToString(inv)}",

            "# HELP norr_cpu_ms_last Last observed CPU milliseconds.",
            "# TYPE norr_cpu_ms_last gauge",
            $"norr_cpu_ms_last {CpuMsLast.ToString(inv)}",

            "# HELP norr_alloc_bytes_last Last observed allocated bytes.",
            "# TYPE norr_alloc_bytes_last gauge",
            $"norr_alloc_bytes_last {AllocBytesLast.ToString(inv)}",
            ""
        });
    }
}
