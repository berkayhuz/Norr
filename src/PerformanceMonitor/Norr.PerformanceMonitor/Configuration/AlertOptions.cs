namespace Norr.PerformanceMonitor.Configuration;

/// <summary>
/// Thresholds and destinations that control when an alert is raised and where it
/// is sent.  All properties are optional; leave a value <see langword="null"/> to
/// disable that particular alert type.
/// </summary>
public sealed class AlertOptions
{
    /// <summary>
    /// Emits an alert when the measured <b>elapsed time</b> of an operation
    /// exceeds this value (in&nbsp;milliseconds).
    /// </summary>
    public double? DurationMs
    {
        get; init;
    }

    /// <summary>
    /// Emits an alert when the measured <b>CPU time</b> consumed by an operation
    /// exceeds this value (in&nbsp;milliseconds).
    /// </summary>
    public double? CpuMs
    {
        get; init;
    }

    /// <summary>
    /// Emits an alert when the <b>managed memory allocated</b> during an
    /// operation exceeds this value (in&nbsp;bytes).
    /// </summary>
    public long? AllocBytes
    {
        get; init;
    }

    /// <summary>
    /// If set, alerts are pushed to a Slack channel via
    /// a <see href="https://api.slack.com/messaging/webhooks">Slack Incoming Webhook</see>
    /// (URL looks like <c>https://hooks.slack.com/services/…</c>).
    /// </summary>
    public Uri? SlackWebhook
    {
        get; init;
    }

    /// <summary>
    /// If set, alerts are POST-ed as JSON to this generic webhook endpoint.
    /// Use when integrating with Teams, Mattermost, PagerDuty, or a custom service.
    /// </summary>
    public Uri? WebhookUrl
    {
        get; init;
    }
}
