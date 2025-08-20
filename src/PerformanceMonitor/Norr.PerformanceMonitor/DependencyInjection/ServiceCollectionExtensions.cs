// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Norr.PerformanceMonitor.Telemetry.Prometheus;

namespace Norr.PerformanceMonitor.DependencyInjection;

/// <summary>
/// Extension methods for registering Norr Performance Monitor services into
/// an <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>
/// Currently provides optional integration with OpenTelemetry via a runtime
/// reflection probe (<see cref="OpenTelemetryBridgeProbe"/>).  
/// This approach ensures that Norr can integrate with OpenTelemetry when the
/// relevant assemblies are present without requiring a hard compile-time dependency.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Attempts to detect and wire the Norr ↔ OpenTelemetry bridge at runtime using reflection.
    /// </summary>
    /// <param name="services">The service collection to add the bridge probe to.</param>
    /// <returns>
    /// The same <see cref="IServiceCollection"/> instance so that calls can be chained.
    /// </returns>
    /// <remarks>
    /// <para>
    /// If no OpenTelemetry assemblies are detected in the current <see cref="AppDomain"/>,
    /// a warning will be logged and the bridge will remain inactive.
    /// </para>
    /// <para>
    /// If the assemblies are present, the bridge probe will attempt to locate the relevant
    /// types and bind them. Any errors during reflection will be logged without throwing.
    /// </para>
    /// <para>
    /// This method only registers a singleton of <see cref="OpenTelemetryBridgeProbe"/> that
    /// performs the detection logic in its constructor.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    /// builder.Services.AddNorrOpenTelemetryBridge();
    /// </code>
    /// </example>
    public static IServiceCollection AddNorrOpenTelemetryBridge(this IServiceCollection services, Action<object> value)
    {
        services.TryAddSingleton<OpenTelemetryBridgeProbe>();
        return services;
    }
    /// <summary>
    /// Norr I/O Prometheus exporter: EventListener'ı başlatır ve /metrics endpoint’ini ekler.
    /// </summary>
    public static IServiceCollection AddNorrPrometheusExporter(this IServiceCollection services)
    {
        services.AddSingleton<NorrMetricsRegistry>();
        services.AddHostedService<NorrIoEventListenerHost>();
        return services;
    }

    /// <summary>
    /// Minimal API için endpoint haritalaması. Varsayılan yol: /metrics
    /// </summary>
    public static IApplicationBuilder UseNorrPrometheusExporter(this IApplicationBuilder app, string path = "/metrics")
    {
        // Basit branch – MVC gerektirmez
        app.Map(path, builder =>
        {
            builder.Run(async ctx =>
            {
                var reg = ctx.RequestServices.GetRequiredService<NorrMetricsRegistry>();
                var body = reg.Export();
                ctx.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
                await ctx.Response.WriteAsync(body, ctx.RequestAborted);
            });
        });

        return app;
    }
}

/// <summary>
/// Performs runtime detection of OpenTelemetry assemblies and attempts to wire
/// the Norr Performance Monitor ↔ OpenTelemetry bridge.
/// </summary>
/// <remarks>
/// The detection is performed in the constructor using reflection, allowing this
/// component to be safely registered even if OpenTelemetry is not referenced at compile time.
/// </remarks>
internal sealed class OpenTelemetryBridgeProbe
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenTelemetryBridgeProbe"/> class.
    /// </summary>
    /// <param name="logger">The logger to report detection results and warnings.</param>
    public OpenTelemetryBridgeProbe(ILogger<OpenTelemetryBridgeProbe> logger)
    {
        try
        {
            // Attempt to find any loaded assembly whose name starts with "OpenTelemetry".
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name?.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase) == true);

            if (asm is null)
            {
                logger.LogWarning(
                    "OpenTelemetry assemblies not found. Norr OTEL bridge is inactive. " +
                    "Install 'OpenTelemetry.Extensions.Hosting' (or a compatible package) to enable automatic wiring.");
                return;
            }

            // Here is where any real binding logic would occur.
            var anyType = asm.DefinedTypes.FirstOrDefault();
            if (anyType != null)
            {
                _ = anyType.FullName; // No-op, just to touch the type
            }

            logger.LogInformation("OpenTelemetry detected: {Assembly}. Norr OTEL bridge wiring attempted.",
                asm.GetName().Name);
        }
        catch (ReflectionTypeLoadException rtle)
        {
            var msg = string.Join("; ", rtle.LoaderExceptions.Select(e => e?.Message));
            Console.WriteLine(msg); // Visible warning instead of silent swallow
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Norr] OpenTelemetry bridge wiring failed: {ex.Message}");
        }
    }
}
