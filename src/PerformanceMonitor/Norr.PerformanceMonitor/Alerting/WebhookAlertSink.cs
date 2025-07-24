using System.Net.Http.Json;
using System.Text.Json;

using Norr.PerformanceMonitor.Abstractions;

namespace Norr.PerformanceMonitor.Alerting;

/// <summary>
/// Generic alert sink that POSTS a <see cref="PerfAlert"/> payload as JSON to
/// the specified <paramref name="url"/>.  
/// Useful when you have a custom webhook endpoint—e.g.&nbsp;Teams, Mattermost,
/// PagerDuty, or your own alert micro-service.
/// </summary>
public sealed class WebhookAlertSink : IAlertSink
{
    private readonly HttpClient _http;
    private readonly Uri _url;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Initializes a new instance of <see cref="WebhookAlertSink"/>.
    /// </summary>
    /// <param name="http">The <see cref="HttpClient"/> obtained from DI.</param>
    /// <param name="url">Destination webhook URL that will receive alert JSON.</param>
    public WebhookAlertSink(HttpClient http, Uri url)
    {
        _http = http;
        _url = url;
    }

    /// <inheritdoc />
    public Task SendAsync(PerfAlert alert, CancellationToken ct = default)
        => _http.PostAsJsonAsync(_url, alert, _json, ct);
}
