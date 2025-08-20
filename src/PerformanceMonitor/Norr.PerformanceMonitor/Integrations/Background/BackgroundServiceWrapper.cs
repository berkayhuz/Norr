// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using Microsoft.Extensions.Hosting;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Telemetry;

namespace Norr.PerformanceMonitor.Integrations.Background;

/// <summary>
/// Base class for background services that automatically wrap execution in a performance
/// monitoring scope and attach ambient job-related tags.
/// </summary>
/// <remarks>
/// <para>
/// This class inherits from <see cref="BackgroundService"/> and standardizes how background
/// jobs are monitored. On execution start, it:
/// <list type="number">
///   <item>
///     <description>Creates an ambient <see cref="TagContext"/> with <c>job.name</c> set to the service's type name.</description>
///   </item>
///   <item>
///     <description>Begins a performance scope via <see cref="IMonitor.Begin(string)"/> with the service name.</description>
///   </item>
///   <item>
///     <description>Invokes the derived service's <see cref="ExecuteCoreAsync(CancellationToken)"/> method.</description>
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>Thread safety:</b> This type is not intended for concurrent use; it is managed by the hosting
/// infrastructure and will be executed on a single background thread per service instance.
/// </para>
/// </remarks>
public abstract class BackgroundServiceWrapper : BackgroundService
{
    private readonly IMonitor _monitor;
    private readonly string _serviceName;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundServiceWrapper"/> class.
    /// </summary>
    /// <param name="monitor">The performance monitor used to create measurement scopes.</param>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="monitor"/> is <see langword="null"/>.</exception>
    protected BackgroundServiceWrapper(IMonitor monitor, string serviceName)
    {
        _monitor = monitor ?? throw new System.ArgumentNullException(nameof(monitor));
        _serviceName = serviceName;
        _serviceName = GetType().Name;
    }

    /// <summary>
    /// Framework entry point for executing the background service.
    /// Wraps the derived service's execution in a monitoring scope and attaches ambient tags.
    /// </summary>
    /// <param name="stoppingToken">A token that is signaled when the service should stop.</param>
    /// <returns>A <see cref="Task"/> representing the background execution operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var ambient = TagContext.Begin(new[]
        {
            new KeyValuePair<string, object?>("job.name", _serviceName)
        });

        using var _ = _monitor.Begin($"BGService {_serviceName}");
        await ExecuteCoreAsync(stoppingToken).ConfigureAwait(false);
    }

    /// <summary>
    /// When implemented in a derived class, contains the background service's actual logic.
    /// </summary>
    /// <param name="stoppingToken">A token that is signaled when the service should stop.</param>
    /// <returns>A <see cref="Task"/> representing the background execution operation.</returns>
    protected abstract Task ExecuteCoreAsync(CancellationToken stoppingToken);
}
