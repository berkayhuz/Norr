// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using MassTransit;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Telemetry;

namespace Norr.PerformanceMonitor.Integrations.MassTransit;

/// <summary>
/// MassTransit consume filter that measures the performance of each consumed message of type <typeparamref name="T"/>.
/// </summary>
/// <remarks>
/// <para>
/// For every message that flows through this filter, an ambient <see cref="TagContext"/> is established with
/// messaging-related tags (system, destination, message type). The filter then begins a performance scope via
/// <see cref="IMonitor.Begin(string)"/> named <c>"Consumer {MessageType}"</c>. When the scope is disposed
/// (after the downstream pipe completes), duration, allocations, and CPU metrics are recorded by the monitor.
/// </para>
/// <para>
/// The filter is intended to be registered in the MassTransit pipeline; it is lightweight and safe to use in production.
/// </para>
/// </remarks>
/// <typeparam name="T">The message type handled by the consumer.</typeparam>
public class PerformanceFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    private readonly IMonitor _monitor;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceFilter{T}"/> class.
    /// </summary>
    /// <param name="monitor">The performance monitor used to create measurement scopes.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="monitor"/> is <see langword="null"/>.</exception>
    public PerformanceFilter(IMonitor monitor)
        => _monitor = monitor ?? throw new System.ArgumentNullException(nameof(monitor));

    /// <summary>
    /// Adds diagnostic information about this filter to the MassTransit probing output.
    /// </summary>
    /// <param name="context">The probe context provided by MassTransit.</param>
    public void Probe(ProbeContext context)
        => context.CreateFilterScope("perfFilter");

    /// <summary>
    /// Invoked by MassTransit for each consumed message. Establishes ambient tags and measures the consumer execution.
    /// </summary>
    /// <param name="context">The current consume context.</param>
    /// <param name="next">The next pipe component in the pipeline.</param>
    /// <returns>A task that completes when the downstream pipeline finishes.</returns>
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var msgType = typeof(T).Name;

        // Establish messaging tags for the ambient scope (appears on all metrics recorded within).
        using var ambient = TagContext.Begin(new[]
        {
            new KeyValuePair<string, object?>("messaging.system", "masstransit"),
            new KeyValuePair<string, object?>("messaging.destination", context.DestinationAddress?.ToString()),
            new KeyValuePair<string, object?>("messaging.message_type", msgType),
        });

        // Measure the consumer execution
        using var _ = _monitor.Begin($"Consumer {msgType}");
        await next.Send(context).ConfigureAwait(false);
    }
}
