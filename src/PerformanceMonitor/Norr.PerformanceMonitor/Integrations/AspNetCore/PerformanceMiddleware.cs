// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Norr.PerformanceMonitor.Integrations.AspNetCore;

/// <summary>
/// ASP.NET Core middleware that creates an <see cref="Activity"/> for each incoming HTTP request
/// and enriches it with common HTTP/server tags.
/// </summary>
/// <remarks>
/// <para>
/// The middleware starts an activity named <c>"http.server.request"</c> using the source
/// <c>"Norr.PerformanceMonitor.Http"</c> and sets tags such as <c>http.method</c>,
/// <c>http.route</c>, <c>url.scheme</c>, and <c>net.peer.ip</c>. After the request completes,
/// it sets <c>http.status_code</c>. On exceptions, it marks the activity as error and attaches
/// <c>exception.type</c> and <c>exception.message</c>.
/// </para>
/// <para>
/// <b>Placement:</b> Register after routing so the endpoint metadata is available, but before
/// user code you want measured. The middleware is allocation‑conscious and safe for high‑throughput scenarios.
/// </para>
/// </remarks>
public sealed class PerformanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMiddleware> _logger;
    private static readonly ActivitySource _activitySource = new("Norr.PerformanceMonitor.Http");

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next delegate/middleware in the pipeline.</param>
    /// <param name="logger">Logger used for error reporting.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="next"/> or <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    public PerformanceMiddleware(RequestDelegate next, ILogger<PerformanceMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes the current HTTP request, creating and enriching an <see cref="Activity"/> and then
    /// invoking the next middleware.
    /// </summary>
    /// <param name="context">The current <see cref="HttpContext"/>.</param>
    /// <returns>A task that completes when the request has been processed.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Start an Activity for the request
        using var activity = _activitySource.StartActivity("http.server.request", ActivityKind.Server);
        if (activity is not null)
        {
            // Endpoint/route information (safe accessor for AOT/trim scenarios)
            var endpoint = HttpContextEndpointAccessor.TryGetEndpoint(context);
            var routePattern = endpoint?.DisplayName
                               ?? endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Routing.RouteNameMetadata>()?.RouteName;

            activity.SetTag("http.method", context.Request.Method);
            activity.SetTag("http.route", routePattern ?? "(unknown)");
            activity.SetTag("url.scheme", context.Request.Scheme);
            activity.SetTag("net.peer.ip", context.Connection.RemoteIpAddress?.ToString());
        }

        try
        {
            await _next(context).ConfigureAwait(false);

            if (activity is not null)
                activity.SetTag("http.status_code", context.Response.StatusCode);
        }
        catch (Exception ex)
        {
            if (activity is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity.SetTag("exception.type", ex.GetType().FullName);
                activity.SetTag("exception.message", ex.Message);
            }

            _logger.LogError(ex, "Request failed");
            throw;
        }
    }
}
