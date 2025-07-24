namespace Norr.PerformanceMonitor.Abstractions;

/// <summary>
/// Root entry point for recording performance data within the application.
/// Acquire a new <see cref="IPerformanceScope"/> via <see cref="Begin"/> and
/// dispose it (e.g. with a <c>using</c> block) to have execution time,
/// allocations, CPU time, etc. automatically captured and forwarded to the
/// configured exporters/alert sinks.
/// </summary>
public interface IMonitor   // synonymous with IPerformanceMonitor
{
    /// <summary>
    /// Starts a new performance-measurement scope.
    /// </summary>
    /// <param name="name">
    /// Logical name of the operation being measured
    /// (service &amp; method, queue consumer, HTTP route, …).
    /// </param>
    /// <returns>
    /// An <see cref="IPerformanceScope"/> that must be disposed to complete
    /// the measurement.
    /// </returns>
    IPerformanceScope Begin(string name);
}
