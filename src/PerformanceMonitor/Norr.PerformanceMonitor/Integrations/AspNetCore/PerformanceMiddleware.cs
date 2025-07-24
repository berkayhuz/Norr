using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using Norr.PerformanceMonitor.Abstractions;

namespace Norr.PerformanceMonitor.Integrations.AspNetCore;

/// <summary>
/// ASP.NET Core middleware that wraps each HTTP request in a performance-measurement
/// scope (<see cref="IMonitor.Begin"/>).  
/// <list type="bullet">
///   <item>
///     Name format: <c>"HTTP {METHOD} {Path}"</c>
///     (e.g. <c>"HTTP GET /api/products"</c>)
///   </item>
///   <item>Skips <c>/health</c> endpoints to avoid noise.</item>
/// </list>
/// </summary>
public sealed class PerformanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMonitor _monitor;

    /// <summary>
    /// Constructs the middleware; invoked by DI.
    /// </summary>
    public PerformanceMiddleware(RequestDelegate next, IMonitor monitor)
    {
        _next = next;
        _monitor = monitor;
    }

    /// <summary>
    /// Executes the middleware logic.  If the request path starts with
    /// <c>/health</c> it simply forwards the call; otherwise it measures the
    /// full request lifetime.
    /// </summary>
    public async Task Invoke(HttpContext ctx)
    {
        // Ignore health-check(s) to keep dashboards clean
        if (ctx.Request.Path.StartsWithSegments("/health"))
        {
            await _next(ctx);
            return;
        }

        using var _ = _monitor.Begin($"HTTP {ctx.Request.Method} {ctx.Request.Path}");
        await _next(ctx);
    }
}

/// <summary>
/// <see cref="IApplicationBuilder"/> extension that registers
/// <see cref="PerformanceMiddleware"/> in the ASP.NET Core pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <see cref="PerformanceMiddleware"/> right where the call is placed.
    /// Put it as early as possible (after routing) to capture the entire request.
    /// </summary>
    public static IApplicationBuilder UsePerformanceMonitoring(this IApplicationBuilder app)
        => app.UseMiddleware<PerformanceMiddleware>();
}
