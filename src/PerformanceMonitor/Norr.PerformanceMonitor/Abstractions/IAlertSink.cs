using Norr.PerformanceMonitor.Alerting;

namespace Norr.PerformanceMonitor.Abstractions;

/// <summary>
/// Abstraction for dispatching <see cref="PerfAlert"/> instances to an external system
/// (e.g., Slack, generic webhook, pager‑duty, etc.). Implementations are expected to
/// be **fire‑and‑forget**; they should <strong>not</strong> throw, block, or bring down the
/// caller in case the remote endpoint is unavailable.
/// </summary>
public interface IAlertSink
{
    /// <summary>
    /// Sends the specified <paramref name="alert"/> to the underlying destination.
    /// Implementations should swallow exceptions internally (or convert them to log
    /// entries) to avoid breaking the metrics pipeline.
    /// </summary>
    /// <param name="alert">The alert payload containing metric name, kind, value and threshold.</param>
    /// <param name="ct">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A task that completes when the alert has been dispatched.</returns>
    Task SendAsync(PerfAlert alert, CancellationToken ct = default);
}
