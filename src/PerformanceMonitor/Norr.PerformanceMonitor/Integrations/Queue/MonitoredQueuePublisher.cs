#nullable enable
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Telemetry;

namespace Norr.PerformanceMonitor.Integrations.Queue;

public sealed class MonitoredQueuePublisher : IQueuePublisher
{
    private readonly IMonitor _monitor;
    private readonly IQueuePublisher _inner; // gerçek publisher (MassTransit, Kafka client, vs.)

    public MonitoredQueuePublisher(IMonitor monitor, IQueuePublisher inner)
    {
        _monitor = monitor;
        _inner = inner;
    }

    public async Task PublishAsync<T>(string topic, T payload, CancellationToken ct = default)
    {
        using var scope = _monitor.Begin("queue.publish");
        using var _ctx = TagContext.Begin(new[]
        {
            new KeyValuePair<string, object?>("messaging.system", "custom"),
            new KeyValuePair<string, object?>("messaging.destination", topic),
            new KeyValuePair<string, object?>("messaging.operation", "publish"),
            new KeyValuePair<string, object?>("messaging.msg_type", typeof(T).Name),
        });

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);

        using var _tag = TagContext.Begin("io.request.bytes", (long)bytes.LongLength);

        IoMetricsRecorder.RecordRequest(bytes.LongLength, new TagList
        {
            { "messaging.system", "custom" },
            { "messaging.destination", topic },
            { "messaging.operation", "publish" },
            { "messaging.msg_type", typeof(T).Name }
        });

        await _inner.PublishAsync(topic, payload, ct).ConfigureAwait(false);
    }
}
