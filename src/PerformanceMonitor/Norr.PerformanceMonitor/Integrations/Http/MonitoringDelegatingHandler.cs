// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Telemetry;

// Ambiguity fix: net tip alias
using HttpClientMonitorOptions = Norr.PerformanceMonitor.Configuration.HttpClientMonitorOptions;

namespace Norr.PerformanceMonitor.Integrations.Http;

public sealed class MonitoringDelegatingHandler : DelegatingHandler
{
    private readonly IMonitor _monitor;
    private readonly HttpClientMonitorOptions _options;

    public MonitoringDelegatingHandler(
        IMonitor monitor,
        IOptions<HttpClientMonitorOptions> options)
    {
        _monitor = monitor;
        _options = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var name = $"http.{request.Method.Method.ToLowerInvariant()}";
        using var scope = _monitor.Begin(name);

        // Ambient tag frame
        using var _reqCtx = TagContext.Begin(new[]
        {
            new KeyValuePair<string, object?>("net.peer.name", request.RequestUri?.Host),
            new KeyValuePair<string, object?>("net.peer.port", request.RequestUri?.Port),
            new KeyValuePair<string, object?>("http.method",   request.Method.Method),
            new KeyValuePair<string, object?>("http.path",     request.RequestUri?.AbsolutePath),
            new KeyValuePair<string, object?>("http.scheme",   request.RequestUri?.Scheme),
        });

        // Request boyutu (mümkünse)
        long? reqBytes = null;
        if (_options.CaptureRequestAndResponseBytes)
        {
            reqBytes = await TryGetContentLengthAsync(request.Content, ct).ConfigureAwait(false);
            if (reqBytes is long rb)
            {
                // using declaration bir "statement" olduğundan if bloğu gerekliydi
                using var _reqBytesTag = TagContext.Begin("io.request.bytes", rb);
            }
        }

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            using var _ex1 = TagContext.Begin("exception.type", ex.GetType().Name);
            using var _ex2 = TagContext.Begin("exception.message", ex.Message);
            using var _ex3 = TagContext.Begin("http.failed", true);

            // Metrik (istek baytı ölçüldüyse)
            if (reqBytes is long rb)
            {
                var tags = new TagList
                {
                    { "http.method", request.Method.Method },
                    { "http.scheme", request.RequestUri?.Scheme ?? "" },
                    { "http.host",   request.RequestUri?.Host ?? "" },
                    { "http.path",   request.RequestUri?.AbsolutePath ?? "" }
                };
                IoMetricsRecorder.RecordRequest(rb, tags);
            }

            throw;
        }

        using var _respCtx = TagContext.Begin(new[]
        {
            new KeyValuePair<string, object?>("http.status_code", (int)response.StatusCode),
            new KeyValuePair<string, object?>("http.version",     response.Version.ToString())
        });

        long? respBytes = null;
        if (_options.CaptureRequestAndResponseBytes)
        {
            respBytes = await TryGetContentLengthAsync(response.Content, ct, _options.ResponseProbeMaxBytes)
                               .ConfigureAwait(false);
            if (respBytes is long sb)
            {
                using var _respBytesTag = TagContext.Begin("io.response.bytes", sb);
            }
        }

        // ---- OpenTelemetry gerçek metrik kayıtları ----

        if (reqBytes is long rb2)
        {
            var t = new TagList
            {
                { "http.method", request.Method.Method },
                { "http.scheme", request.RequestUri?.Scheme ?? "" },
                { "http.host",   request.RequestUri?.Host ?? "" },
                { "http.path",   request.RequestUri?.AbsolutePath ?? "" }
            };
            IoMetricsRecorder.RecordRequest(rb2, t);
        }

        if (respBytes is long sb2)
        {
            var t = new TagList
            {
                { "http.method", request.Method.Method },
                { "http.status_code", (int)response.StatusCode },
                { "http.host",   request.RequestUri?.Host ?? "" },
                { "http.path",   request.RequestUri?.AbsolutePath ?? "" }
            };
            IoMetricsRecorder.RecordResponse(sb2, t);
        }

        return response;
    }

    private static async Task<long?> TryGetContentLengthAsync(
        HttpContent? content, CancellationToken ct, int probeLimit = 0)
    {
        if (content is null)
            return null;

        if (content.Headers.ContentLength is long h)
            return h;

        if (probeLimit <= 0)
            return null;

        // Uyarı: Bu, içeriği buffer'lar. Büyük body’lerde açmayın.
        var buffer = await content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        return buffer.Length;
    }
}
