// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Norr.PerformanceMonitor.Alerting.Net;
using Norr.PerformanceMonitor.Alerting.Resilience;
using Norr.PerformanceMonitor.Configuration.Alerting;
using Norr.Diagnostics.Abstractions.Logging;

namespace Norr.PerformanceMonitor.Alerting.Slack;

public sealed class SlackAlertSink
{
    private static readonly JsonSerializerOptions _json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SlackAlertSink> _logger;
    private readonly WebhookSecurityOptions _security;

    int attempt = 0;

    public SlackAlertSink(
        IHttpClientFactory httpClientFactory,
        IOptions<WebhookSecurityOptions> securityOptions,
        ILogger<SlackAlertSink> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _security = securityOptions?.Value ?? new WebhookSecurityOptions();
    }

    public Task SendAsync(Uri slackWebhookUrl, string text, System.Threading.CancellationToken ct = default)
    {
        UriSafetyGuard.ValidateWebhookTarget(slackWebhookUrl, _security);

        _logger.PM().StartMonitoring("SlackWebhook");

        var payload = new
        {
            text
        };
        return PostJsonAsync(slackWebhookUrl, payload, ct);
    }

    private async Task PostJsonAsync(Uri target, object payload, System.Threading.CancellationToken ct)
    {
        using var client = _httpClientFactory.CreateClient("norr-alerts");
        using var req = BuildJsonPost(target, payload);

        var resp = await ResiliencePolicies.SendWithResilienceAsync(
                sender: async ctk =>
                {
                    var req = BuildJsonPost(target, payload);
                    return await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ctk).ConfigureAwait(false);
                },
                maxRetries: 3,
                perTryTimeout: TimeSpan.FromSeconds(5),
                ct: ct
            ).ConfigureAwait(false);

        if (resp is null)
        {
            _logger.PM().SendFailure(attempt, new InvalidOperationException($"Slack alert failed after retries. Target: {target}"));
            return;
        }

        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
            {
                var body = await SafeReadAsync(resp, ct).ConfigureAwait(false);
                _logger.PM().SendFailure(attempt, new InvalidOperationException($"Slack alert failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}"));
            }
            else
            {
                _logger.PM().SendSuccess(attempt);
            }
        }

    }
    private static HttpRequestMessage BuildJsonPost(Uri target, object payload)
    {
        var json = JsonSerializer.Serialize(payload, _json);
        var req = new HttpRequestMessage(HttpMethod.Post, target)
        {
            Version = new Version(2, 0),
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        req.Headers.UserAgent.Add(new ProductInfoHeaderValue("NorrPerformanceMonitor", "1.0"));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return req;
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage resp, System.Threading.CancellationToken ct)
    {
        try
        {
#if NET8_0_OR_GREATER
            return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
#else
            ct.ThrowIfCancellationRequested();
            return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
        }
        catch { return "(unavailable)"; }
    }
}
