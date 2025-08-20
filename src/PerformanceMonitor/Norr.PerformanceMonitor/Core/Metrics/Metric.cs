// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 



namespace Norr.PerformanceMonitor.Core.Metrics;

/// <summary>
/// Immutable data-transfer object that represents a single recorded metric
/// (for example, duration, CPU time, or allocated bytes) for a specific operation.
/// </summary>
/// <remarks>
/// <para>
/// The value’s unit depends on <see cref="Kind"/>:
/// <list type="bullet">
///   <item><description><c>DurationMs</c> → milliseconds</description></item>
///   <item><description><c>CpuMs</c> → milliseconds of CPU time</description></item>
///   <item><description><c>AllocBytes</c> → bytes</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Timestamps:</b> <see cref="TimestampUtc"/> is expected to be in UTC.
/// </para>
/// </remarks>
/// <param name="Name">
/// Logical identifier of the operation, for example <c>OrderService.PlaceOrder</c>
/// or <c>HTTP GET /api/products</c>.
/// </param>
/// <param name="Kind">
/// The type of measurement captured; see <see cref="MetricKind"/>.
/// </param>
/// <param name="Value">
/// Numeric value of the metric; units depend on <paramref name="Kind"/>.
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
