namespace Norr.PerformanceMonitor.Core.Metrics;

/// <summary>
/// Immutable data-transfer object that represents a single recorded metric
/// (duration, CPU-time, allocated bytes, …) for a specific operation.
/// </summary>
/// <param name="Name">
/// Logical identifier of the operation, e.g.&nbsp;
/// <c>"OrderService.PlaceOrder"</c> or <c>"HTTP GET /api/products"</c>.
/// </param>
/// <param name="Kind">
/// The type of measurement captured, see <see cref="MetricKind"/>.
/// </param>
/// <param name="Value">
/// Numeric value of the metric; units depend on <paramref name="Kind"/>
/// (milliseconds, bytes, …).
/// </param>
/// <param name="TimestampUtc">
/// UTC timestamp when the metric was recorded.
/// </param>
public sealed record Metric(
    string Name,
    MetricKind Kind,
    double Value,
    DateTime TimestampUtc
);
