// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


namespace Norr.PerformanceMonitor.Configuration;

/// <summary>
/// Thresholds and destinations that control when an alert is raised and where it is sent.
/// All properties are optional; set a property to <see langword="null"/> to disable that
/// particular alert type or sink.
/// </summary>
/// <remarks>
/// <para>
/// <b>Semantics:</b> Implementations typically consider a threshold to be exceeded when
/// the measured value is strictly greater than the configured value (i.e., a <c>&gt;</c> comparison).
/// If an option is <see langword="null"/>, that check is skipped entirely.
/// </para>
/// <para>
/// <b>Units:</b> <see cref="DurationMs"/> and <see cref="CpuMs"/> are expressed in milliseconds.
/// <see cref="AllocBytes"/> is expressed in bytes.
/// </para>
/// <para>
/// <b>Sinks:</b> When <see cref="SlackWebhook"/> or <see cref="WebhookUrl"/> is set, alerts are
/// sent to the corresponding destinations. Implementations may support sending to multiple sinks
/// in parallel if multiple endpoints are configured.
/// </para>
/// <para>
/// <b>Security:</b> Webhook targets should be validated against your policy (for example,
/// using <c>WebhookSecurityOptions</c>) to mitigate SSRF and data exfiltration risks.
/// Store webhook URLs as secrets and prefer HTTPS.
/// </para>
/// <para>
/// <b>Example</b>
/// <code language="csharp"><![CDATA[
/// var alerts = new AlertOptions
/// {
///     DurationMs  = 500.0,          // wall-clock latency threshold (ms)
///     CpuMs       = 300.0,          // CPU time threshold (ms)
///     AllocBytes  = 5 * 1024 * 1024,// allocation threshold (bytes)
///     SlackWebhook = new Uri("https://hooks.slack.com/services/AAA/BBB/CCC"),
///     WebhookUrl   = new Uri("https://example.com/hooks/alerts")
/// };
/// ]]></code>
/// </para>
/// </remarks>
public sealed class AlertOptions
{
    /// <summary>
    /// Emits an alert when the measured <b>elapsed wall‑clock time</b> of an operation
    /// exceeds this value, in milliseconds. Set to <see langword="null"/> to disable.
    /// </summary>
    /// <value>The latency threshold in milliseconds; <see langword="null"/> to disable.</value>
    public double? DurationMs
    {
        get; init;
    }

    /// <summary>
    /// Emits an alert when the measured <b>CPU time</b> (user + kernel) consumed by an operation
    /// exceeds this value, in milliseconds. Set to <see langword="null"/> to disable.
    /// </summary>
    /// <remarks>
    /// Availability depends on platform support for per‑thread or per‑operation CPU timing.
    /// </remarks>
    /// <value>The CPU time threshold in milliseconds; <see langword="null"/> to disable.</value>
    public double? CpuMs
    {
        get; init;
    }

    /// <summary>
    /// Emits an alert when the <b>managed memory allocated</b> during an operation
    /// exceeds this value, in bytes. Set to <see langword="null"/> to disable.
    /// </summary>
    /// <remarks>
    /// Depending on the measurement strategy, this may reflect total bytes allocated
    /// by the operation (including transient objects) rather than retained memory.
    /// </remarks>
    /// <value>The allocation threshold in bytes; <see langword="null"/> to disable.</value>
    public long? AllocBytes
    {
        get; init;
    }

    /// <summary>
    /// If set, alerts are pushed to a Slack channel via an Incoming Webhook.
    /// </summary>
    /// <remarks>
    /// The URL typically looks like <c>https://hooks.slack.com/services/…</c>.
    /// Ensure the target is permitted by your webhook security policy and use HTTPS.
    /// </remarks>
    /// <value>The Slack Incoming Webhook URL; <see langword="null"/> to disable.</value>
    public Uri? SlackWebhook
    {
        get; init;
    }

    /// <summary>
    /// If set, alerts are POSTed as JSON to this generic webhook endpoint.
    /// </summary>
    /// <remarks>
    /// Useful for integrating with services such as Microsoft Teams, Mattermost, PagerDuty,
    /// or a custom receiver. Ensure the endpoint is trusted and validated by policy.
    /// </remarks>
    /// <value>The generic webhook URL; <see langword="null"/> to disable.</value>
    public Uri? WebhookUrl
    {
        get; init;
    }
}
