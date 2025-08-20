// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 
using System.Diagnostics;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Norr.Diagnostics.Abstractions.Logging;

namespace Norr.Diagnostics.Logging.AspNetCore;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        => (_next, _logger) = (next, logger);

    public async Task Invoke(HttpContext ctx)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogTrace("[DL:1001] HTTP {Method} {Path} started", ctx.Request.Method, ctx.Request.Path);

        await _next(ctx);

        sw.Stop();
        var status = ctx.Response.StatusCode;
        var level = status >= 500 ? LogLevel.Error
                  : status >= 400 ? LogLevel.Warning
                  : LogLevel.Information;

        _logger.Log(level, new EventId(level == LogLevel.Warning ? 3001 :
                                       level == LogLevel.Error ? 2001 : 1201),
                    "[DL:{Code}] HTTP {Status} {Method} {Path} in {ElapsedMs} ms",
                    level == LogLevel.Warning ? 3001 :
                    level == LogLevel.Error ? 2001 : 1201,
                    status, ctx.Request.Method, ctx.Request.Path, sw.Elapsed.TotalMilliseconds);
    }
}
