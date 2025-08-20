// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using Norr.PerformanceMonitor.Telemetry;

namespace Norr.PerformanceMonitor.Integrations.AspNetCore;

/// <summary>
/// Middleware that extracts the low-cardinality route template (e.g., <c>"/users/{id}"</c>)
/// from the current request and adds it as an ambient tag (<c>http.route</c>) to the
/// <see cref="TagContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// This middleware avoids reflection and is compatible with trimming and AOT scenarios.
/// It should be placed early in the ASP.NET Core pipeline (after routing) so that the route
/// template is available when processing the request.
/// </para>
/// <para>
/// The route tag is added only if the route template can be resolved. If no template is found,
/// the middleware simply invokes the next delegate without modification.
/// </para>
/// </remarks>
public sealed class RouteTaggingMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="RouteTaggingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware delegate in the request pipeline.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="next"/> is <see langword="null"/>.</exception>
    public RouteTaggingMiddleware(RequestDelegate next)
        => _next = next ?? throw new ArgumentNullException(nameof(next));

    /// <summary>
    /// Processes the incoming HTTP request, tagging it with the <c>http.route</c> value if available.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task Invoke(HttpContext context)
    {
        var route = EndpointAccessor.TryGetRouteTemplate(context);

        if (!string.IsNullOrWhiteSpace(route))
        {
            using var _ = TagContext.Begin("http.route", route!);
            await _next(context).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }
}

/// <summary>
/// Extension methods for enabling <see cref="RouteTaggingMiddleware"/> in the ASP.NET Core pipeline.
/// </summary>
public static class RouteTaggingExtensions
{
    /// <summary>
    /// Adds the <see cref="RouteTaggingMiddleware"/> to the application's request pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The updated <see cref="IApplicationBuilder"/> for chaining.</returns>
    public static IApplicationBuilder UseRouteTagging(this IApplicationBuilder app)
        => app.UseMiddleware<RouteTaggingMiddleware>();
}
