using Microsoft.Extensions.Hosting;

using Norr.PerformanceMonitor.Abstractions;

namespace Norr.PerformanceMonitor.Integrations.Background;

/// <summary>
/// Convenience base-class for <see cref="BackgroundService"/> implementations
/// that automatically wrap their lifetime in a performance-measurement scope.
///
/// ```csharp
/// public sealed class OrderWorker : BackgroundServiceWrapper
/// {
///     public OrderWorker(IMonitor monitor) : base(monitor) { }
///
///     protected override async Task ExecuteCoreAsync(CancellationToken stop)
///     {
///         while (!stop.IsCancellationRequested)
///         {
///             // do work…
///             await Task.Delay(1_000, stop);
///         }
///     }
/// }
/// ```
///
/// When the worker starts you’ll get a metric named
/// <c>"BGService OrderWorker"</c> with duration, CPU-time and allocations.
/// </summary>
public abstract class BackgroundServiceWrapper : BackgroundService
{
    private readonly IMonitor _monitor;
    private readonly string _serviceName;

    /// <summary>
    /// Creates a new wrapper; DI will supply the <see cref="IMonitor"/>.
    /// </summary>
    protected BackgroundServiceWrapper(IMonitor monitor)
    {
        _monitor = monitor;
        _serviceName = GetType().Name;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var _ = _monitor.Begin($"BGService {_serviceName}");
        await ExecuteCoreAsync(stoppingToken);
    }

    /// <summary>
    /// User-code override that contains the main loop / logic for the background
    /// worker.  Exceptions will bubble up and stop the host as usual.
    /// </summary>
    protected abstract Task ExecuteCoreAsync(CancellationToken stoppingToken);
}
