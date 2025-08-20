// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Norr.PerformanceMonitor.OpenTelemetry.Extensions;

/// <summary>
/// Convenience extensions that wire up Prometheus (pull/scrape) support for ASP.NET Core
/// when using OpenTelemetry.
/// </summary>
/// <remarks>
/// <para>
/// These helpers do two things:
/// </para>
/// <list type="number">
///   <item>
///     <description><see cref="AddAspNetCorePrometheus(OpenTelemetryBuilder)"/> registers the
///     Prometheus exporter on the OpenTelemetry metrics pipeline.</description>
///   </item>
///   <item>
///     <description><see cref="UseAspNetCorePrometheusScrapingEndpoint(IApplicationBuilder, string, Func{HttpContext, bool}?)"/>
///     exposes a version-safe <c>/metrics</c> scraping endpoint in ASP.NET Core, optionally guarded
///     by a <c>predicate</c> function to allow/deny requests (e.g., IP allowlist, auth, or rate limits).
///     </description>
///   </item>
/// </list>
/// <para>
/// The scraping endpoint is provided by the
/// <c>OpenTelemetry.Exporter.Prometheus.AspNetCore</c> package.
/// </para>
/// </remarks>
public static class PrometheusAspNetCoreExtensions
{
    /// <summary>
    /// Adds the Prometheus exporter (ASP.NET Core) to the OpenTelemetry <see cref="MeterProvider"/>
    /// pipeline.
    /// </summary>
    /// <param name="builder">The OpenTelemetry root <see cref="OpenTelemetryBuilder"/>.</param>
    /// <returns>The same <paramref name="builder"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code language="csharp"><![CDATA[
    /// var builder = WebApplication.CreateBuilder(args);
    ///
    /// builder.Services.AddOpenTelemetry()
    ///     .WithMetrics(mb =>
    ///     {
    ///         mb.AddAspNetCoreInstrumentation()
    ///           .AddRuntimeInstrumentation();
    ///     })
    ///     .AddAspNetCorePrometheus(); // <-- registers Prometheus exporter
    /// ]]></code>
    /// </example>
    public static OpenTelemetryBuilder AddAspNetCorePrometheus(
        this OpenTelemetryBuilder builder)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        builder.WithMetrics(mb =>
        {
            // Provided by OpenTelemetry.Exporter.Prometheus.AspNetCore
            mb.AddPrometheusExporter();
        });

        return builder;
    }

    /// <summary>
    /// Enables a Prometheus scraping endpoint (default <c>/metrics</c>) in a version-safe manner.
    /// </summary>
    /// <param name="app">The current <see cref="IApplicationBuilder"/>.</param>
    /// <param name="scrapeEndpointPath">Path to expose for scraping. Defaults to <c>"/metrics"</c>.</param>
    /// <param name="predicate">
    /// Optional request gate. Return <see langword="true"/> to allow the scrape, otherwise
    /// <see langword="false"/>. Use this to implement IP allowlists, basic authentication,
    /// rate limits, or environment guards.
    /// </param>
    /// <returns>The same <paramref name="app"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// When <paramref name="predicate"/> is <see langword="null"/>, the method forwards to the
    /// exporter’s endpoint registration overload that accepts only a path. When a
    /// <paramref name="predicate"/> is provided, path matching is performed inside the predicate
    /// to remain resilient to overload changes across exporter versions.
    /// </para>
    /// <para>
    /// This endpoint should generally not be exposed publicly without protection. Consider binding
    /// Kestrel to a private interface, using reverse proxy rules, or guarding with
    /// <paramref name="predicate"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp"><![CDATA[
    /// var app = builder.Build();
    ///
    /// // Open endpoint without extra checks:
    /// app.UseAspNetCorePrometheusScrapingEndpoint(); // exposes /metrics
    ///
    /// // Gate with an allowlist:
    /// app.UseAspNetCorePrometheusScrapingEndpoint("/metrics", ctx =>
    /// {
    ///     var remote = ctx.Connection.RemoteIpAddress;
    ///     return remote is not null && (remote.IsLoopback() || remote.ToString() == "10.0.0.5");
    /// });
    /// ]]></code>
    /// </example>
    public static IApplicationBuilder UseAspNetCorePrometheusScrapingEndpoint(
        this IApplicationBuilder app,
        string scrapeEndpointPath = "/metrics",
        Func<HttpContext, bool>? predicate = null)
    {
        if (app is null)
            throw new ArgumentNullException(nameof(app));

        // Fast path: just the route (available in current exporter versions).
        if (predicate is null)
        {
            app.UseOpenTelemetryPrometheusScrapingEndpoint(scrapeEndpointPath);
            return app;
        }

        // Predicate path: include path matching inside the predicate to stay resilient
        // to overload changes across exporter versions.
        app.UseOpenTelemetryPrometheusScrapingEndpoint(ctx =>
            ctx.Request.Path.StartsWithSegments(scrapeEndpointPath, StringComparison.Ordinal) && predicate(ctx));

        return app;
    }
}
