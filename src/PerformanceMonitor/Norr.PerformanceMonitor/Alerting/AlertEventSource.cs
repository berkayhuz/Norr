// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Diagnostics.Tracing;

namespace Norr.PerformanceMonitor.Alerting;

/// <summary>
/// ETW/EventSource for alert delivery operations in Norr Performance Monitor.
/// </summary>
/// <remarks>
/// <para>
/// This source emits structured events covering the lifecycle of alert sends across sinks
/// (e.g., webhooks, Slack, etc.). It is primarily intended for diagnostic tooling, ETW
/// consumers, and high‑fidelity telemetry pipelines.
/// </para>
/// <para>
/// <b>Event IDs:</b>
/// <list type="table">
///   <listheader>
///     <term>ID</term><description>Name</description>
///   </listheader>
///   <item><term>1</term><description><see cref="SendStart(string, string)"/></description></item>
///   <item><term>2</term><description><see cref="SendSuccess(string, int)"/></description></item>
///   <item><term>3</term><description><see cref="SendRetry(string, int, int, string?, long)"/></description></item>
///   <item><term>4</term><description><see cref="SendFailed(string, int, string?)"/></description></item>
///   <item><term>5</term><description><see cref="SendException(string, string, string?)"/></description></item>
/// </list>
/// </para>
/// <para>
/// <b>Performance note:</b> Avoid logging large payloads or PII in event parameters. Prefer short,
/// stable identifiers for sink and target values.
/// </para>
/// </remarks>
[EventSource(Name = "Norr.PerformanceMonitor.Alerting")]
internal sealed class AlertEventSource : EventSource
{
    /// <summary>
    /// Singleton instance used to emit alert events.
    /// </summary>
    public static readonly AlertEventSource Log = new();

    private AlertEventSource()
    {
    }

    /// <summary>
    /// Emits the start of an alert send attempt.
    /// </summary>
    /// <param name="sink">
    /// Logical name of the alert sink (e.g., <c>"webhook"</c>, <c>"slack"</c>).
    /// </param>
    /// <param name="target">
    /// Opaque identifier of the destination (e.g., URL host or channel key). Do not include secrets.
    /// </param>
    [Event(1, Level = EventLevel.Informational)]
    public void SendStart(string sink, string target)
        => WriteEvent(1, sink, target);

    /// <summary>
    /// Emits a successful alert send completion.
    /// </summary>
    /// <param name="sink">Logical name of the alert sink.</param>
    /// <param name="statusCode">Transport/status code returned by the destination (e.g., HTTP 200).</param>
    [Event(2, Level = EventLevel.Informational)]
    public void SendSuccess(string sink, int statusCode)
        => WriteEvent(2, sink, statusCode);

    /// <summary>
    /// Emits a retry attempt following a transient failure.
    /// </summary>
    /// <param name="sink">Logical name of the alert sink.</param>
    /// <param name="attempt">Zero‑based retry attempt count.</param>
    /// <param name="statusCode">Status code of the failed attempt.</param>
    /// <param name="reason">Optional textual reason or short error description.</param>
    /// <param name="delayMs">Delay before the next retry, in milliseconds.</param>
    [Event(3, Level = EventLevel.Warning)]
    public void SendRetry(string sink, int attempt, int statusCode, string? reason, long delayMs)
        => WriteEvent(3, sink, attempt, statusCode, reason ?? string.Empty, delayMs);

    /// <summary>
    /// Emits a terminal failure when the alert could not be delivered.
    /// </summary>
    /// <param name="sink">Logical name of the alert sink.</param>
    /// <param name="statusCode">Final status code (if available).</param>
    /// <param name="reason">Optional textual reason or error message.</param>
    [Event(4, Level = EventLevel.Error)]
    public void SendFailed(string sink, int statusCode, string? reason)
        => WriteEvent(4, sink, statusCode, reason ?? string.Empty);

    /// <summary>
    /// Emits an exception encountered during alert sending.
    /// </summary>
    /// <param name="sink">Logical name of the alert sink.</param>
    /// <param name="exceptionType">Exception type name (e.g., <c>System.TimeoutException</c>).</param>
    /// <param name="message">Optional exception message.</param>
    [Event(5, Level = EventLevel.Error)]
    public void SendException(string sink, string exceptionType, string? message)
        => WriteEvent(5, sink, exceptionType, message ?? string.Empty);
}
