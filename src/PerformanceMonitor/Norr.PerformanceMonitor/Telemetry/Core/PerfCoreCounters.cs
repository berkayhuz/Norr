// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable

using System;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Norr.PerformanceMonitor.Telemetry.Core;

/// <summary>
/// Süre (ms), CPU (ms) ve allocated bytes ölçümlerini OpenTelemetry olmadan EventSource ile yayınlar.
/// - IncrementingEventCounter: *_total  (interval başına artış = rate hesabı için uygun)
/// - EventCounter: *_last   (son gözlenen değer)
/// </summary>
public static class PerfCoreCounters
{
    public const string EventSourceName = "Norr.Perf.Core";

    private static double _durationMsTotal;
    private static double _cpuMsTotal;
    private static double _allocBytesTotal;

    private static double _durationMsLast;
    private static double _cpuMsLast;
    private static double _allocBytesLast;

    private static readonly PerfCoreEventSource _es = PerfCoreEventSource.Log;

    /// <summary>
    /// Ölçümü kaydedin. Paketinizde ölçüm bittiği noktada çağırın.
    /// </summary>
    /// <param name="durationMs">İşlem süresi (ms)</param>
    /// <param name="cpuMs">CPU süresi (ms)</param>
    /// <param name="allocatedBytes">Tahmini/ölçülen ayrılan bayt</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Record(double durationMs, double cpuMs, long allocatedBytes)
    {
        if (durationMs > 0)
        {
            Interlocked.Exchange(ref _durationMsLast, durationMs);
            Interlocked.Exchange(ref _durationMsTotal, _durationMsTotal + durationMs);
            _es.IncrementDuration(durationMs);
            _es.WriteDurationLast(durationMs);
        }

        if (cpuMs > 0)
        {
            Interlocked.Exchange(ref _cpuMsLast, cpuMs);
            Interlocked.Exchange(ref _cpuMsTotal, _cpuMsTotal + cpuMs);
            _es.IncrementCpu(cpuMs);
            _es.WriteCpuLast(cpuMs);
        }

        if (allocatedBytes > 0)
        {
            var alloc = (double)allocatedBytes;
            Interlocked.Exchange(ref _allocBytesLast, alloc);
            Interlocked.Exchange(ref _allocBytesTotal, _allocBytesTotal + alloc);
            _es.IncrementAlloc(alloc);
            _es.WriteAllocLast(alloc);
        }
    }

    public static (double durTotal, double cpuTotal, double allocTotal, double durLast, double cpuLast, double allocLast)
        Snapshot()
        => (Volatile.Read(ref _durationMsTotal),
            Volatile.Read(ref _cpuMsTotal),
            Volatile.Read(ref _allocBytesTotal),
            Volatile.Read(ref _durationMsLast),
            Volatile.Read(ref _cpuMsLast),
            Volatile.Read(ref _allocBytesLast));

    [EventSource(Name = EventSourceName)]
    private sealed class PerfCoreEventSource : EventSource
    {
        public static readonly PerfCoreEventSource Log = new();

        private IncrementingEventCounter? _durationTotal;
        private IncrementingEventCounter? _cpuTotal;
        private IncrementingEventCounter? _allocTotal;

        private EventCounter? _durationLast;
        private EventCounter? _cpuLast;
        private EventCounter? _allocLast;

        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command != EventCommand.Enable)
                return;

            _durationTotal ??= new IncrementingEventCounter("duration_ms.total", this)
            {
                DisplayName = "Duration (ms total, per interval)",
                DisplayUnits = "ms"
            };

            _cpuTotal ??= new IncrementingEventCounter("cpu_ms.total", this)
            {
                DisplayName = "CPU (ms total, per interval)",
                DisplayUnits = "ms"
            };

            _allocTotal ??= new IncrementingEventCounter("alloc_bytes.total", this)
            {
                DisplayName = "Allocated Bytes (total, per interval)",
                DisplayUnits = "bytes"
            };

            _durationLast ??= new EventCounter("duration_ms.last", this)
            {
                DisplayName = "Duration (ms last)",
                DisplayUnits = "ms"
            };

            _cpuLast ??= new EventCounter("cpu_ms.last", this)
            {
                DisplayName = "CPU (ms last)",
                DisplayUnits = "ms"
            };

            _allocLast ??= new EventCounter("alloc_bytes.last", this)
            {
                DisplayName = "Allocated Bytes (last)",
                DisplayUnits = "bytes"
            };
        }

        [NonEvent] public void IncrementDuration(double delta) => _durationTotal?.Increment(delta);
        [NonEvent] public void IncrementCpu(double delta) => _cpuTotal?.Increment(delta);
        [NonEvent] public void IncrementAlloc(double delta) => _allocTotal?.Increment(delta);

        [NonEvent] public void WriteDurationLast(double v) => _durationLast?.WriteMetric(v);
        [NonEvent] public void WriteCpuLast(double v) => _cpuLast?.WriteMetric(v);
        [NonEvent] public void WriteAllocLast(double v) => _allocLast?.WriteMetric(v);
    }
}
