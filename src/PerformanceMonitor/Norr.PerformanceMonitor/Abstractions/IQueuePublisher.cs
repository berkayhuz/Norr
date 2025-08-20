#nullable enable
using System.Threading;
using System.Threading.Tasks;

using Norr;
using Norr.PerformanceMonitor;
using Norr.PerformanceMonitor.Integrations;

namespace Norr.PerformanceMonitor.Abstractions;

public interface IQueuePublisher
{
    Task PublishAsync<T>(string topic, T payload, CancellationToken ct = default);
}
