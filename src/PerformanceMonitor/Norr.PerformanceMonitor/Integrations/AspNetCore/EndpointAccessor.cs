// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Net;
using System.Net.Http;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace Norr.PerformanceMonitor.Integrations.AspNetCore;

/// <summary>
/// Lightweight helper for retrieving ASP.NET Core endpoint/route information
/// without relying on <c>HttpContext.GetEndpoint()</c> extension methods
/// or using reflection.
/// </summary>
/// <remarks>
/// <para>
/// This utility reads the current <see cref="IEndpointFeature"/> directly from
/// <see cref="HttpContext.Features"/> and attempts to extract a **low-cardinality**
/// route template from the resolved endpoint. When the endpoint is a
/// <see cref="RouteEndpoint"/>, the canonical source of truth is
/// For other endpoint types, it falls back to <see cref="Endpoint.DisplayName"/>.
/// </para>
/// <para>
/// <b>Why not use <c>HttpContext.GetEndpoint()</c>?</b> Depending on where you consume
/// this code (e.g., shared libraries, analyzers, or minimal ASP.NET Core references),
/// you may prefer to avoid extension-method dependencies and stick to feature access,
/// which is part of the core HTTP abstractions.
/// </para>
/// <para>
/// <b>Thread safety:</b> The type is stateless and thread-safe.
/// </para>
/// <para>
/// <b>Performance:</b> Access is O(1). It performs no allocations beyond those incurred
/// by the underlying framework when materializing endpoint metadata. If no endpoint is
/// available (e.g., early in the pipeline or for non-routed requests), the method returns
/// <see langword="null"/>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp"><![CDATA[
/// public sealed class RouteTaggingMiddleware
/// {
///     private readonly RequestDelegate _next;
///     private readonly ILogger<RouteTaggingMiddleware> _logger;
///
///     public RouteTaggingMiddleware(RequestDelegate next, ILogger<RouteTaggingMiddleware> logger)
///     {
///         _next = next;
///         _logger = logger;
///     }
///
///     public async Task InvokeAsync(HttpContext context)
///     {
///         var routeTemplate = EndpointAccessor.TryGetRouteTemplate(context)
///                            ?? "(unknown)";
///
///         // Use a low-cardinality value for metrics/tracing:
///         // e.g., tag your Activity or meter with "http.route"
///         _logger.LogDebug("Resolved http.route = {RouteTemplate}", routeTemplate);
///
///         await _next(context);
///     }
/// }
/// ]]></code>
/// </example>
/// <seealso cref="IEndpointFeature"/>
/// <seealso cref="Endpoint"/>
/// <seealso cref="RouteEndpoint"/>
internal static class EndpointAccessor
{
    /// <summary>
    /// Attempts to return a low-cardinality route template for the current request
    /// (for example, <c>"/api/products/{id:int}"</c>). If the route cannot be determined,
    /// returns <see langword="null"/>.
    /// </summary>
    /// <param name="context">
    /// The current <see cref="HttpContext"/> from which to resolve the endpoint.
    /// </param>
    /// <returns>
    /// The resolved route template (preferably a static template with parameter placeholders),
    /// or <see langword="null"/> if unavailable.
    /// </returns>
    /// <remarks>
    /// <para>
    /// When the resolved endpoint is a <see cref="RouteEndpoint"/>, this method returns
    /// yields a stable, low-cardinality template suitable for metrics and tracing tags
    /// (e.g., for <c>http.route</c>).
    /// </para>
    /// <para>
    /// For non-routing endpoints or custom endpoint types where a route pattern is not
    /// available, the method falls back to <see cref="Endpoint.DisplayName"/>. Be aware that
    /// display names may be higher-cardinality and should be used with caution in telemetry.
    /// </para>
    /// </remarks>
    public static string? TryGetRouteTemplate(HttpContext context)
    {
        var endpoint = context?.Features?.Get<IEndpointFeature>()?.Endpoint;
        if (endpoint is null)
            return null;

        // If this is a RouteEndpoint, the RoutePattern.RawText is the most accurate template.
        if (endpoint is RouteEndpoint re)
            return re.RoutePattern?.RawText;

        // For some custom endpoint types, DisplayName can be more descriptive.
        return endpoint.DisplayName;
    }
}
