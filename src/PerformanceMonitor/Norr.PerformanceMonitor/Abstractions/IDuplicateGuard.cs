namespace Norr.PerformanceMonitor.Abstractions;

/// <summary>
/// Guards against excessive repetition of the same metric name within a short
/// period of time (e.g.&nbsp;to avoid log/alert spam).  
/// Typical implementation: Bloom filter with a cooldown window.
/// </summary>
public interface IDuplicateGuard
{
    /// <summary>
    /// Determines whether a metric with the given <paramref name="name"/> should
    /// be emitted at the specified <paramref name="utcNow"/> timestamp.
    /// </summary>
    /// <param name="name">
    /// The fully-qualified metric identifier
    /// (for example <c>"OrderService.PlaceOrder:DurationMs"</c>).
    /// </param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <returns>
    /// <c>true</c> if the metric has **not** been seen recently and is therefore
    /// allowed to be published; otherwise <c>false</c>.
    /// </returns>
    bool ShouldEmit(string name, DateTime utcNow);
}
