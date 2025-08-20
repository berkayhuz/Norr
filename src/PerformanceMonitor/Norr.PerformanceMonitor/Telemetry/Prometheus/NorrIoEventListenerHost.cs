// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

namespace Norr.PerformanceMonitor.Telemetry.Prometheus;

/// <summary>
/// Hem I/O EventSource'ını (Norr.Perf.IO) hem de Core EventSource'ını (Norr.Perf.Core) dinler
/// ve gelen EventCounters payload'larını registry'e işler.
/// </summary>
internal sealed class NorrIoEventListenerHost : IHostedService
{
    private readonly NorrMetricsRegistry _registry;
    private EventListener? _listener;

    public NorrIoEventListenerHost(NorrMetricsRegistry registry) => _registry = registry;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _listener = new Listener(_registry);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _listener?.Dispose();
        _listener = null;
        return Task.CompletedTask;
    }

    private sealed class Listener : EventListener
    {
        private readonly NorrMetricsRegistry _registry;

        public Listener(NorrMetricsRegistry registry) => _registry = registry;

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name is IoName or CoreName)
            {
                EnableEvents(eventSource, EventLevel.Informational, EventKeywords.All,
                    new Dictionary<string, string?> { ["EventCounterIntervalSec"] = "5" });
            }
        }

        private const string IoName = "Norr.Perf.IO";
        private const string CoreName = "Norr.Perf.Core";

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (e.Payload is not { Count: > 0 } payload)
                return;
            if (payload[0] is not IDictionary map)
                return;

            var name = map["Name"] as string;
            if (string.IsNullOrEmpty(name))
                return;

            // IncrementingEventCounter: "Increment"
            if (map.Contains("Increment") && map["Increment"] is IConvertible incConv)
            {
                var inc = incConv.ToDouble(System.Globalization.CultureInfo.InvariantCulture);

                // IO totals
                if (name == "io.request.bytes.total")
                {
                    _registry.AddRequestBytes(inc);
                    return;
                }
                if (name == "io.response.bytes.total")
                {
                    _registry.AddResponseBytes(inc);
                    return;
                }

                // CORE totals
                if (name == "duration_ms.total")
                {
                    _registry.AddDurationMs(inc);
                    return;
                }
                if (name == "cpu_ms.total")
                {
                    _registry.AddCpuMs(inc);
                    return;
                }
                if (name == "alloc_bytes.total")
                {
                    _registry.AddAllocBytes(inc);
                    return;
                }

                return;
            }

            // EventCounter: "Mean"
            if (map.Contains("Mean") && map["Mean"] is IConvertible meanConv)
            {
                var mean = meanConv.ToDouble(System.Globalization.CultureInfo.InvariantCulture);

                // IO last
                if (name == "io.request.bytes.last")
                {
                    _registry.SetRequestLast(mean);
                    return;
                }
                if (name == "io.response.bytes.last")
                {
                    _registry.SetResponseLast(mean);
                    return;
                }

                // CORE last
                if (name == "duration_ms.last")
                {
                    _registry.SetDurationLast(mean);
                    return;
                }
                if (name == "cpu_ms.last")
                {
                    _registry.SetCpuLast(mean);
                    return;
                }
                if (name == "alloc_bytes.last")
                {
                    _registry.SetAllocLast(mean);
                    return;
                }
            }
        }
    }
}
