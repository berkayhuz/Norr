// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Norr.Diagnostics.Abstractions.Logging;
using Norr.PerformanceMonitor.Alerting.Net;
using Norr.PerformanceMonitor.Alerting.Resilience;
using Norr.PerformanceMonitor.Configuration.Alerting;

namespace Norr.PerformanceMonitor.Alerting.Webhook;

/// <summary>
/// Sends alert payloads to an arbitrary HTTP(S) webhook endpoint as JSON.
/// </summary>
/// <remarks>
/// <para>
/// This sink serializes a provided payload to JSON and POSTs it to a target URL,
/// after validating that URL against <see cref="WebhookSecurityOptions"/> to reduce
/// SSRF risk. Requests are executed with retry and timeout policies via
/// <see cref="ResiliencePolicies"/>.
/// </para>
/// <para><b>Thread safety:</b> The type is stateless and thread‑safe.</para>
/// </remarks>
public sealed class WebhookAlertSink
{
    private static readonly JsonSerializerOptions _json =
        new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookAlertSink> _logger;
    private readonly WebhookSecurityOptions _security;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookAlertSink"/> class.
    /// </summary>
    /// <param name="httpClientFactory">
    /// Factory used to create HTTP clients for sending webhook requests.
    /// The client named <c>"norr-alerts"</c> should be configured with appropriate
    /// timeouts, proxy settings, and default headers.
    /// </param>
    /// <param name="securityOptions">
    /// Security policy settings controlling allowed webhook targets, HTTPS enforcement,
    /// and private address blocking.
    /// </param>
    /// <param name="logger">
    /// Logger for diagnostics and failure reporting.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="httpClientFactory"/> or <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    public WebhookAlertSink(
        IHttpClientFactory httpClientFactory,
        IOptions<WebhookSecurityOptions> securityOptions,
        ILogger<WebhookAlertSink> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _security = securityOptions?.Value ?? new WebhookSecurityOptions();
    }

    /// <summary>Sends the specified payload as JSON to the target webhook URL.</summary>
    /// <exception cref="ArgumentException">If <paramref name="target"/> is not an absolute URI.</exception>
    /// <exception cref="InvalidOperationException">If the target is disallowed by <see cref="WebhookSecurityOptions"/>.</exception>
    public async Task SendAsync(Uri target, object payload, CancellationToken ct = default)
    {
        if (target is null || !target.IsAbsoluteUri)
            throw new ArgumentException("Target must be an absolute URI.", nameof(target));

        _logger.PM().StartMonitoring(target.ToString());

        // Security validation
        UriSafetyGuard.ValidateWebhookTarget(target, _security);

        using var client = _httpClientFactory.CreateClient("norr-alerts");
        using var req = BuildJsonPost(target, payload);

        HttpResponseMessage? resp = null;
        int attempt = 0;

        try
        {
            resp = await ResiliencePolicies.SendWithResilienceAsync(
                sender: async ctk =>
                {
                    attempt++;
                    return await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ctk);
                },
                maxRetries: 3,
                perTryTimeout: TimeSpan.FromSeconds(5),
                ct: ct).ConfigureAwait(false);

            if (resp is null)
            {
                _logger.PM().SendFailure(attempt, new InvalidOperationException("Webhook alert failed after retries. Target: " + target));
                return;
            }

            using (resp)
            {
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await SafeReadAsync(resp, ct).ConfigureAwait(false);
                    _logger.PM().SendFailure(attempt, new InvalidOperationException(
                        $"Webhook alert failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}"));
                }
                else
                {
                    _logger.PM().SendSuccess(attempt);
                }
            }

            _logger.PM().Completed(0); // İstersen süre ölçümünü ekleyebiliriz
        }
        catch (Exception ex)
        {
            _logger.PM().SendFailure(attempt > 0 ? attempt : 1, ex);
            throw;
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

    // Safely read response body for logging; never throws.
    private static async Task<string> SafeReadAsync(HttpResponseMessage resp, CancellationToken ct)
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
        catch
        {
            return "(unavailable)";
        }
    }
}
