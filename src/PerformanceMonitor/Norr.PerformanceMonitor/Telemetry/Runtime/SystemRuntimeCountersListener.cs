// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable

using System.Diagnostics.Tracing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Norr.PerformanceMonitor.Configuration;
using Norr.PerformanceMonitor.Telemetry.Prometheus;

/// <summary>
/// System.Runtime EventCounter’larını dinler ve NorrMetricsRegistry’ye yazar.
/// </summary>
internal sealed class SystemRuntimeCountersListener : EventListener, IHostedService
{
    private readonly RuntimeCountersOptions _opt;
    private readonly NorrMetricsRegistry _reg;


    public SystemRuntimeCountersListener(IOptions<RuntimeCountersOptions> opt, NorrMetricsRegistry reg)
    {
        _opt = opt.Value;
        _reg = reg;
    }


    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (!_opt.Enabled)
            return;
        if (eventSource?.Name is not "System.Runtime")
            return;


        // EventCounter update interval’ını ayarla
        EnableEvents(eventSource, EventLevel.Informational, EventKeywords.All,
        new Dictionary<string, string?>
        {
            ["EventCounterIntervalSec"] = Math.Max(1, _opt.RefreshIntervalSeconds).ToString()
        });
    }


    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (!_opt.Enabled)
            return;
        if (eventData?.Payload is null || eventData.Payload.Count == 0)
            return;


        for (int i = 0; i < eventData.Payload.Count; i++)
        {
            if (eventData.Payload[i] is not IDictionary<string, object> payload)
                continue;
            if (!payload.TryGetValue("Name", out var nObj) || nObj is not string name)
                continue;


            double? mean = payload.TryGetValue("Mean", out var m) ? m as double? : null;
            double? inc = payload.TryGetValue("Increment", out var incObj) ? incObj as double? : null;


            switch (name)
            {
                // GC
                case "time-in-gc":
                    if (mean is double tigc)
                        _reg.SetGcTimeInGcPercent(tigc);
                    break;
                case "gc-heap-size":
                    if (mean is double heapMb)
                        _reg.SetGcHeapSizeBytes((long)(heapMb * 1024 * 1024));
                    break;
                case "gc-fragmentation":
                    if (mean is double frag)
                        _reg.SetGcFragmentationPercent(frag);
                    break;
                case "gen-0-gc-count":
                    if (inc is double g0)
                        _reg.AddGen0((long)g0);
                    break;
                case "gen-1-gc-count":
                    if (inc is double g1)
                        _reg.AddGen1((long)g1);
                    break;
                case "gen-2-gc-count":
                    if (inc is double g2)
                        _reg.AddGen2((long)g2);
                    break;
                case "loh-size":
                    if (mean is double loh)
                        _reg.SetLohBytes((long)loh);
                    break;
                case "poh-size":
                    if (mean is double poh)
                        _reg.SetPohBytes((long)poh);
                    break;
                case "gc-committed":
                    if (mean is double c)
                        _reg.SetGcCommittedBytes((long)c);
                    break;
                case "total-pause-time-by-gc":
                    if (inc is double p)
                        _reg.AddGcPauseSeconds(p);
                    break;


                // ThreadPool & Lock
                case "threadpool-queue-length":
                    if (mean is double ql)
                        _reg.SetThreadPoolQueueLength((long)ql);
                    break;
                case "threadpool-completed-items-count":
                    if (inc is double ci)
                        _reg.AddThreadPoolCompleted((long)ci);
                    break;
                case "threadpool-thread-count":
                    if (mean is double tc)
                        _reg.SetThreadPoolThreadCount((long)tc);
                    break;
                case "monitor-lock-contention-count":
                    if (inc is double lc)
                        _reg.AddMonitorLockContention((long)lc);
                    break;
            }
        }
    }


    // IHostedService – ömür yönetimi
    public System.Threading.Tasks.Task StartAsync(CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
    public System.Threading.Tasks.Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            Dispose();
        }
        catch { }
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
