using MediatR;

using Norr.PerformanceMonitor.Abstractions;

namespace Norr.PerformanceMonitor.Integrations.MediatR;

/// <summary>
/// MediatR pipeline behavior that measures the execution time (and other
/// metrics) of every request/handler pair.  
/// Metric name format: <c>"MediatR {RequestType}"</c>.
/// </summary>
/// <typeparam name="TReq">The request type being handled.</typeparam>
/// <typeparam name="TRes">The response type returned by the handler.</typeparam>
public sealed class PerformanceBehavior<TReq, TRes> : IPipelineBehavior<TReq, TRes>
    where TReq : notnull
{
    private readonly IMonitor _monitor;

    /// <summary>
    /// Creates a new <see cref="PerformanceBehavior{TReq,TRes}"/>.
    /// </summary>
    public PerformanceBehavior(IMonitor monitor) => _monitor = monitor;

    /// <inheritdoc />
    public async Task<TRes> Handle(
        TReq request,
        RequestHandlerDelegate<TRes> next,
        CancellationToken ct)
    {
        using var _ = _monitor.Begin($"MediatR {typeof(TReq).Name}");
        return await next();
    }
}
