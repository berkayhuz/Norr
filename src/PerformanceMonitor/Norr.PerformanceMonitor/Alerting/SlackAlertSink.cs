using System.Net.Http.Json;
using System.Text.Json;

using Norr.PerformanceMonitor.Abstractions;

namespace Norr.PerformanceMonitor.Alerting;

/// <summary>
/// Sends <see cref="PerfAlert"/> notifications to a Slack channel using an
/// <see href="https://api.slack.com/messaging/webhooks">Incoming Webhook</see>.
/// </summary>
public sealed class SlackAlertSink : IAlertSink
{
    private readonly HttpClient _http;
    private readonly Uri _url;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Creates a new <see cref="SlackAlertSink"/>.
    /// </summary>
    /// <param name="http">An <see cref="HttpClient"/> instance (supplied by DI).</param>
    /// <param name="webhookUrl">
    /// The Slack Incoming-Webhook URL (looks like
    /// <c>https://hooks.slack.com/services/T000/B000/XXXXXXXX</c>).
    /// </param>
    public SlackAlertSink(HttpClient http, Uri webhookUrl)
    {
        _http = http;
        _url = webhookUrl;
    }

    /// <inheritdoc />
    public async Task SendAsync(PerfAlert alert, CancellationToken ct = default)
    {
        // Minimal payload: Slack's "text" field is enough for basic alerts.
        var payload = new
        {
            text =
                $"*{alert.MetricName}* {alert.Kind} " +
                $"*{alert.Value:N1}* (>{alert.Threshold})"
        };

        using var res = await _http.PostAsJsonAsync(_url, payload, _json, ct);
        res.EnsureSuccessStatusCode();   // Slack rate limit ≈ 1 msg/second per channel
    }
}
