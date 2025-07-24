using MassTransit;

using Norr.PerformanceMonitor.Abstractions;

namespace Norr.PerformanceMonitor.Integrations.MassTransit;

/// <summary>
/// MassTransit <see cref="IFilter{T}"/> that wraps each consumed message in a
/// performance-measurement scope.  
/// Name format: <c>"Consumer {MessageType}"</c>
/// (e.g. <c>"Consumer OrderSubmitted"</c>).
/// </summary>
/// <typeparam name="T">The message type handled by the consumer.</typeparam>
public class PerformanceFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    private readonly IMonitor _monitor;

    /// <summary>
    /// Initializes a new <see cref="PerformanceFilter{T}"/>.
    /// </summary>
    public PerformanceFilter(IMonitor monitor) => _monitor = monitor;

    /// <inheritdoc />
    public void Probe(ProbeContext context) => context.CreateFilterScope("perfFilter");

    /// <inheritdoc />
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        using var _ = _monitor.Begin($"Consumer {typeof(T).Name}");
        await next.Send(context);
    }
}
