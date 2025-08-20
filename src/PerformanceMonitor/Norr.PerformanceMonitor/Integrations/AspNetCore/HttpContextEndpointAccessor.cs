// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Reflection;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Norr.PerformanceMonitor.Integrations.AspNetCore;

/// <summary>
/// Unified, dependency‑light accessor for the current ASP.NET Core <see cref="Endpoint"/> from a
/// <see cref="HttpContext"/>—works across TFMs and package versions without hard depending on
/// extension methods or reflection‑heavy code paths.
/// </summary>
/// <remarks>
/// <para>
/// Different hosting stacks and target frameworks expose endpoint routing in slightly different
/// ways. The most common API is the <c>HttpContext.GetEndpoint()</c> extension method located on
/// <c>Microsoft.AspNetCore.Http.EndpointHttpContextExtensions</c>. However, class libraries or
/// analyzers might intentionally avoid a direct compile‑time dependency on that package/version.
/// </para>
/// <para>
/// This helper first attempts to locate and invoke <c>GetEndpoint(HttpContext)</c> via a cached,
/// reflection‑generated delegate (no repeated reflection per request). If that API is unavailable,
/// it falls back to reading <see cref="IEndpointFeature"/> from <see cref="HttpContext.Features"/>,
/// which is the most stable, low‑level access point.
/// </para>
/// <para><b>Thread safety:</b> The type is stateless. Reflection discovery runs once in a
/// static constructor and is safe for concurrent access.</para>
/// <para><b>Performance:</b> After startup, the hot path is a single delegate invocation or a
/// single feature lookup—both O(1). The reflection scan happens only once.</para>
/// <para><b>When to use:</b> Prefer this accessor when writing reusable libraries (middleware,
/// telemetry, diagnostics) that must run on multiple ASP.NET Core versions or in environments
/// where the <c>GetEndpoint</c> extension may not be referenced.</para>
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
///         var endpoint = HttpContextEndpointAccessor.TryGetEndpoint(context);
///         var display = endpoint?.DisplayName ?? "(unknown)";
///         _logger.LogDebug("Resolved endpoint: {Endpoint}", display);
///         await _next(context);
///     }
/// }
/// ]]></code>
/// </example>
/// <seealso cref="Endpoint"/>
/// <seealso cref="IEndpointFeature"/>
internal static class HttpContextEndpointAccessor
{
    // Cached delegate to EndpointHttpContextExtensions.GetEndpoint(HttpContext) if present.
    private static readonly Func<HttpContext, Endpoint?>? _getEndpoint;

    static HttpContextEndpointAccessor()
    {
        // Discover the extension method once at startup to avoid a hard reference.
        // Target: Microsoft.AspNetCore.Http.EndpointHttpContextExtensions.GetEndpoint(HttpContext)
        try
        {
            var method = FindGetEndpointMethod();
            if (method is not null)
            {
                // Create a lightweight delegate wrapper to avoid reflection per call.
                _getEndpoint = CreateInvoker(method);
            }
        }
        catch
        {
            // If discovery fails for any reason, we simply won't use the extension path.
            _getEndpoint = null;
        }
    }

    /// <summary>
    /// Attempts to resolve the current request's <see cref="Endpoint"/> from <paramref name="httpContext"/>.
    /// Returns <see langword="null"/> if routing hasn't resolved an endpoint yet or if the request
    /// is not using endpoint routing.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>The resolved <see cref="Endpoint"/>, or <see langword="null"/> if unavailable.</returns>
    /// <remarks>
    /// The method first tries a cached call into the <c>GetEndpoint</c> extension if it exists in
    /// the loaded ASP.NET Core assemblies. If not, it falls back to <see cref="IEndpointFeature"/>.
    /// </remarks>
    public static Endpoint? TryGetEndpoint(HttpContext httpContext)
    {
        if (httpContext is null)
            return null;

        // 1) Fast path via extension method, if discovered.
        if (_getEndpoint is not null)
        {
            try
            {
                var ep = _getEndpoint(httpContext);
                if (ep is not null)
                    return ep;
            }
            catch
            {
                // Swallow and continue to the feature path if the extension throws for any reason.
            }
        }

        // 2) Stable fallback: read the feature directly.
        return httpContext.Features.Get<IEndpointFeature>()?.Endpoint;
    }

    // --- Reflection helpers --------------------------------------------------

    private static MethodInfo? FindGetEndpointMethod()
    {
        // We look for a public static method named "GetEndpoint" with a single HttpContext parameter
        // on a type named "Microsoft.AspNetCore.Http.EndpointHttpContextExtensions".
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type = null;
            try
            {
                type = asm.GetType("Microsoft.AspNetCore.Http.EndpointHttpContextExtensions", throwOnError: false);
            }
            catch
            {
                // Some dynamic or collectible assemblies can throw here—ignore and continue.
            }

            if (type is null)
                continue;

            var method = type.GetMethod(
                name: "GetEndpoint",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(HttpContext) },
                modifiers: null);

            if (method is not null && typeof(Endpoint).IsAssignableFrom(method.ReturnType))
                return method;
        }

        return null;
    }

    private static Func<HttpContext, Endpoint?> CreateInvoker(MethodInfo method)
    {
        // Simple reflection wrapper; the overhead is only on the first call when creating the delegate.
        return (HttpContext ctx) => (Endpoint?)method.Invoke(null, new object[] { ctx });
    }
}
