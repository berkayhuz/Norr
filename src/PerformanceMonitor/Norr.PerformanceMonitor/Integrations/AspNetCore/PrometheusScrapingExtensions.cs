// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.AspNetCore.Builder;

namespace Norr.PerformanceMonitor.Integrations.AspNetCore;

/// <summary>
/// Provides extension methods to enable OpenTelemetry Prometheus scraping endpoint integration
/// dynamically at runtime.
/// </summary>
/// <remarks>
/// <para>
/// This uses reflection to locate and invoke the OpenTelemetry
/// <c>UseOpenTelemetryPrometheusScrapingEndpoint</c> method if it is available in the application's
/// referenced assemblies.  
/// </para>
/// <para>
/// ⚠ <b>Note:</b> This approach is <see cref="RequiresUnreferencedCodeAttribute">not trimming/AOT safe</see>
/// because it relies on reflection to discover types and methods.
/// Use it only when you are certain the required types will be preserved.
/// </para>
/// </remarks>
public static class PrometheusScrapingExtensions
{
    /// <summary>
    /// Attempts to enable the OpenTelemetry Prometheus scraping endpoint by using reflection to
    /// locate and call the <c>UseOpenTelemetryPrometheusScrapingEndpoint(IApplicationBuilder)</c> method.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> instance to configure.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> instance, for chaining calls.</returns>
    /// <remarks>
    /// If the OpenTelemetry Prometheus scraping endpoint extension method is not found,
    /// this method silently does nothing.
    /// </remarks>
    [RequiresUnreferencedCode(
        "Uses reflection to locate OpenTelemetry Prometheus scraping endpoint types/methods.")]
    public static IApplicationBuilder UseNorrPrometheusScrapingEndpoint(this IApplicationBuilder app)
    {
        try
        {
            // Locate the extension method: UseOpenTelemetryPrometheusScrapingEndpoint(IApplicationBuilder)
            var useMi = typeof(PrometheusScrapingExtensions).Assembly
                .GetReferencedAssemblies()
                .Select(Assembly.Load)
                .Where(a => a != null &&
                            a.FullName?.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase) == true)
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsSealed && t.IsAbstract && t.IsPublic)
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .FirstOrDefault(m =>
                    m.Name == "UseOpenTelemetryPrometheusScrapingEndpoint" &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(IApplicationBuilder));

            if (useMi is not null)
            {
                useMi.Invoke(null, new object[] { app });
                return app;
            }
        }
        catch
        {
            // Swallow all exceptions — no-op if reflection fails
        }

        return app;
    }
}
