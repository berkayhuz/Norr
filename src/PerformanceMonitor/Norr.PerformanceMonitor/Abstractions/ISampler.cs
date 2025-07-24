namespace Norr.PerformanceMonitor.Abstractions;

/// <summary>
/// Determines, for each incoming operation, whether detailed metrics should be
/// recorded.  Implementations may use probabilistic algorithms (<em>p-sampling</em>),
/// deterministic hashing, adaptive/heat-map logic, etc., to reduce overhead on
/// high-throughput systems.
/// </summary>
public interface ISampler
{
    /// <summary>
    /// Returns <c>true</c> when the operation identified by
    /// <paramref name="name"/> should be measured and exported; otherwise
    /// <c>false</c>.
    /// </summary>
    /// <param name="name">
    /// Logical operation name (e.g.&nbsp;<c>"OrderService.PlaceOrder"</c> or
    /// <c>"HTTP GET /api/orders"</c>).
    /// </param>
    bool ShouldSample(string name);
}
