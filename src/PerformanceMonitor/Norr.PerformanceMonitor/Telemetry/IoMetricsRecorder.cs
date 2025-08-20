// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Norr.PerformanceMonitor.Telemetry;

/// <summary>
/// I/O bayt metriklerini OpenTelemetry olmadan yayınlar.
/// - Toplamlar için atomik sayaçlar (process-wide).
/// - Gözlemleme için EventSource + EventCounters:
///   - io.request.bytes.total (IncrementingEventCounter)
///   - io.response.bytes.total (IncrementingEventCounter)
///   - io.request.bytes.last (EventCounter, son gözlenen değer)
///   - io.response.bytes.last (EventCounter, son gözlenen değer)
/// </summary>
public static class IoMetricsRecorder
{
    /// <summary>EventSource adınız; dotnet-counters bununla bulur.</summary>
    public const string EventSourceName = "Norr.Perf.IO";

    private static long _reqTotalBytes;
    private static long _respTotalBytes;

    // “Son gözlenen değer”ler (grafikte faydalı olur)
    private static long _reqLastBytes;
    private static long _respLastBytes;

    // EventSource tekil instance
    private static readonly IoMetricsEventSource _es = IoMetricsEventSource.Log;

    /// <summary>İstek (giden/yazılan) baytları kaydeder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordRequest(long bytes, in TagList _ = default)
    {
        if (bytes <= 0)
            return;
        Interlocked.Add(ref _reqTotalBytes, bytes);
        Volatile.Write(ref _reqLastBytes, bytes);

        // EventSource Counters
        _es.IncrementRequest(bytes);
        _es.WriteRequestLast(bytes);
    }

    /// <summary>Yanıt (gelen/okunan) baytları kaydeder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordResponse(long bytes, in TagList _ = default)
    {
        if (bytes <= 0)
            return;
        Interlocked.Add(ref _respTotalBytes, bytes);
        Volatile.Write(ref _respLastBytes, bytes);

        _es.IncrementResponse(bytes);
        _es.WriteResponseLast(bytes);
    }

    /// <summary>Şu anki toplamlar (telemetry dışında programatik erişim için).</summary>
    public static (long requestTotal, long responseTotal, long requestLast, long responseLast) Snapshot()
    {
        return (Volatile.Read(ref _reqTotalBytes),
                Volatile.Read(ref _respTotalBytes),
                Volatile.Read(ref _reqLastBytes),
                Volatile.Read(ref _respLastBytes));
    }

    /// <summary>
    /// EventSource: IncrementingEventCounter’lar “per-interval artışları” verir (dotnet-counters rate hesaplar).
    /// EventCounter’lar son gözlenen değeri yayınlar (örn. büyük tek transferler).
    /// </summary>
    [EventSource(Name = EventSourceName)]
    private sealed class IoMetricsEventSource : EventSource
    {
        public static readonly IoMetricsEventSource Log = new();

        private IncrementingEventCounter? _reqBytesTotal;
        private IncrementingEventCounter? _respBytesTotal;
        private EventCounter? _reqBytesLast;
        private EventCounter? _respBytesLast;

        private IoMetricsEventSource()
        {
        }

        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command == EventCommand.Enable)
            {
                _reqBytesTotal ??= new IncrementingEventCounter("io.request.bytes.total", this)
                {
                    DisplayName = "I/O Request Bytes (Total, per interval)",
                    DisplayUnits = "bytes"
                };
                _respBytesTotal ??= new IncrementingEventCounter("io.response.bytes.total", this)
                {
                    DisplayName = "I/O Response Bytes (Total, per interval)",
                    DisplayUnits = "bytes"
                };
                _reqBytesLast ??= new EventCounter("io.request.bytes.last", this)
                {
                    DisplayName = "I/O Request Bytes (Last)",
                    DisplayUnits = "bytes"
                };
                _respBytesLast ??= new EventCounter("io.response.bytes.last", this)
                {
                    DisplayName = "I/O Response Bytes (Last)",
                    DisplayUnits = "bytes"
                };
            }
        }

        [NonEvent]
        public void IncrementRequest(long delta)
        {
            _reqBytesTotal?.Increment(delta);
        }

        [NonEvent]
        public void IncrementResponse(long delta)
        {
            _respBytesTotal?.Increment(delta);
        }

        [NonEvent]
        public void WriteRequestLast(long value)
        {
            _reqBytesLast?.WriteMetric(value);
        }

        [NonEvent]
        public void WriteResponseLast(long value)
        {
            _respBytesLast?.WriteMetric(value);
        }
    }
}
