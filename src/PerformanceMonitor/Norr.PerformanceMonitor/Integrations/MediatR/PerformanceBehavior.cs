// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using MediatR;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Telemetry;

namespace Norr.PerformanceMonitor.Integrations.MediatR;

/// <summary>
/// MediatR pipeline behavior that measures the performance of each request/response pair
/// and annotates metrics with messaging-related ambient tags.
/// </summary>
/// <remarks>
/// <para>
/// For every request that flows through this behavior, an ambient <see cref="TagContext"/> is established with
/// the following tags:
/// <list type="bullet">
///   <item><description><c>messaging.system = mediatr</c></description></item>
///   <item><description><c>messaging.operation = handle</c></description></item>
///   <item><description><c>messaging.message_type = {typeof(TReq).Name}</c></description></item>
/// </list>
/// A performance scope is then started via <see cref="IMonitor.Begin(string)"/> named <c>"MediatR {TReq}"</c>.
/// When the scope is disposed (after the handler completes), duration, allocations, and CPU metrics are recorded
/// by the configured <see cref="IMonitor"/>.
/// </para>
/// <para>
/// <b>Thread safety:</b> The behavior is stateless and safe to reuse across requests; per-request state is scoped
/// to the call to <see cref="Handle(TReq, RequestHandlerDelegate{TRes}, CancellationToken)"/>.
/// </para>
/// </remarks>
/// <typeparam name="TReq">The MediatR request type.</typeparam>
/// <typeparam name="TRes">The handler response type.</typeparam>
public sealed class PerformanceBehavior<TReq, TRes> : IPipelineBehavior<TReq, TRes>
    where TReq : notnull
{
    private readonly IMonitor _monitor;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceBehavior{TReq, TRes}"/> class.
    /// </summary>
    /// <param name="monitor">The performance monitor used to create measurement scopes.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="monitor"/> is <see langword="null"/>.</exception>
    public PerformanceBehavior(IMonitor monitor)
        => _monitor = monitor ?? throw new System.ArgumentNullException(nameof(monitor));

    /// <summary>
    /// Invoked by MediatR for each request. Establishes ambient tags and measures the handler execution.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="next">Delegate that invokes the next component/handler in the pipeline.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The handler’s response.</returns>
    public async Task<TRes> Handle(
        TReq request,
        RequestHandlerDelegate<TRes> next,
        CancellationToken ct)
    {
        var reqType = typeof(TReq).Name;

        // Establish messaging tags that will be attached to all metrics recorded within this scope.
        using var ambient = TagContext.Begin(new[]
        {
            new KeyValuePair<string, object?>("messaging.system", "mediatr"),
            new KeyValuePair<string, object?>("messaging.operation", "handle"),
            new KeyValuePair<string, object?>("messaging.message_type", reqType),
        });

        using var _ = _monitor.Begin($"MediatR {reqType}");
        return await next().ConfigureAwait(false);
    }
}
