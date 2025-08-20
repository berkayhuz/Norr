// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Text;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Norr.PerformanceMonitor.Integrations.AspNetCore;

/// <summary>
/// Provides extension methods to expose a minimal Prometheus metrics endpoint
/// compatible with the Prometheus text-based exposition format.
/// </summary>
public static class PrometheusEndpointExtensions
{
    /// <summary>
    /// Maps a minimal Prometheus exposition endpoint to the specified request path.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> instance to configure.</param>
    /// <param name="path">
    /// The request path for the endpoint.  
    /// Defaults to <c>/metrics</c>.
    /// </param>
    /// <returns>The same <see cref="IApplicationBuilder"/> instance, for chaining calls.</returns>
    /// <remarks>
    /// <para>
    /// The endpoint returns metrics in the Prometheus v0.0.4 text format
    /// with a <c>Content-Type</c> of <c>text/plain; version=0.0.4</c>.
    /// </para>
    /// <para>
    /// This method does not perform authentication or authorization;  
    /// it is the caller's responsibility to secure the endpoint if needed.
    /// </para>
    /// </remarks>
    public static IApplicationBuilder UseNorrPrometheusEndpoint(
        this IApplicationBuilder app,
        string path = "/metrics")
    {
        return app.Map(path, b => b.Run(async ctx =>
        {
            ctx.Response.ContentType = "text/plain; version=0.0.4";
            var sb = new StringBuilder(8 * 1024);
            PrometheusTextSerializer.WriteMetrics(sb);
            await ctx.Response.WriteAsync(sb.ToString());
        }));
    }
}
