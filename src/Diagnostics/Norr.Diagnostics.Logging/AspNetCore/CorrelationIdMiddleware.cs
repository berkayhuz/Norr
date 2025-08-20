// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Norr.Diagnostics.Abstractions.Logging;
using Norr.Diagnostics.Logging.Context;

namespace Norr.Diagnostics.Logging.AspNetCore;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ICorrelationIdAccessor _accessor;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public const string HeaderName = "X-Correlation-Id";

    public CorrelationIdMiddleware(RequestDelegate next, ICorrelationIdAccessor accessor, ILogger<CorrelationIdMiddleware> logger)
        => (_next, _accessor, _logger) = (next, accessor, logger);

    public async Task Invoke(HttpContext ctx)
    {
        var id = ctx.Request.Headers.TryGetValue(HeaderName, out var v) && !string.IsNullOrWhiteSpace(v)
            ? v.ToString()
            : System.Guid.NewGuid().ToString("N");

        _accessor.CorrelationId = id;
        using (_logger.BeginScope("{CorrelationId}", id))
        {
            ctx.Response.Headers[HeaderName] = id;
            await _next(ctx);
        }
    }
}
