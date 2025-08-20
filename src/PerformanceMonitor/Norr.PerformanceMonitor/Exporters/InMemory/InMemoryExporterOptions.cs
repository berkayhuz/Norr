// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 



using Norr.PerformanceMonitor.Exporters.Core;

namespace Norr.PerformanceMonitor.Exporters.InMemory;

/// <summary>
/// Configuration options for the in‑memory exporter.
/// </summary>
/// <remarks>
/// <para>
/// The in‑memory exporter buffers telemetry items inside a bounded, ring‑buffer–like queue.
/// When the queue reaches <see cref="Capacity"/>, the exporter applies the configured
/// <see cref="DropPolicy"/> to decide which items to discard. Use these options to tune the
/// balance between memory footprint, throughput, and loss behavior.
/// </para>
/// <para>
/// <b>Capacity:</b> Governs the maximum number of items retained concurrently. Larger values
/// reduce the chance of drops at the cost of additional memory. Smaller values bound memory
/// tightly but increase drop likelihood under bursty load.
/// </para>
/// <para>
/// <b>Drop policy:</b> Controls which items are discarded when the queue is full. Typical
/// strategies include dropping the oldest enqueued items (to favor recency) or the newest items
/// (to preserve backlog). See <see cref="DropPolicy"/> for details.
/// </para>
/// <para>
/// <b>Metrics name:</b> The <see cref="Name"/> is used as a low‑cardinality label in internal
/// metrics to distinguish multiple exporter instances (e.g., <c>"default"</c>, <c>"http"</c>,
/// <c>"background"</c>). Prefer short, stable identifiers to avoid metric cardinality issues.
/// </para>
/// </remarks>
/// <example>
/// The following example configures the in‑memory exporter in a typical ASP.NET Core application:
/// <code language="csharp">
/// builder.Services.Configure&lt;InMemoryExporterOptions&gt;(options =>
/// {
///     options.Capacity = 50_000;               // Increase buffer to tolerate bursts
///     options.DropPolicy = DropPolicy.DropOldest; // Favor newest data under pressure
///     options.Name = "http-pipeline";          // Appears as a label in internal metrics
/// });
/// </code>
/// </example>
/// <example>
/// Example <c>appsettings.json</c> binding:
/// <code language="json">
/// {
///   "Norr": {
///     "PerformanceMonitor": {
///       "Exporters": {
///         "InMemory": {
///           "Capacity": 20000,
///           "DropPolicy": "DropOldest",
///           "Name": "default"
///         }
///       }
///     }
///   }
/// }
/// </code>
/// </example>
/// <threadsafety>
/// This options type is immutable after construction (init‑only setters) and therefore
/// thread‑safe to share across components once bound/configured.
/// </threadsafety>
/// <seealso cref="DropPolicy"/>
public sealed class InMemoryExporterOptions
{
    /// <summary>
    /// Maximum number of items that the in‑memory queue can hold at any time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The exporter will begin dropping items according to <see cref="DropPolicy"/> once
    /// this capacity is reached. Choose a value that balances acceptable memory usage with
    /// the likelihood of drops under peak load.
    /// </para>
    /// <para>
    /// <b>Default:</b> <c>10_000</c>.
    /// </para>
    /// </remarks>
    /// <value>
    /// A positive integer representing the queue capacity in number of items.
    /// </value>
    public int Capacity { get; init; } = 10_000;

    /// <summary>
    /// Strategy applied when the queue is at capacity and a new item arrives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="DropPolicy.DropOldest"/> favors the most recent data by evicting the
    /// oldest items first. Alternative policies may preserve backlog or apply other
    /// heuristics based on implementation.
    /// </para>
    /// <para>
    /// <b>Default:</b> <see cref="DropPolicy.DropOldest"/>.
    /// </para>
    /// </remarks>
    /// <value>
    /// A <see cref="DropPolicy"/> value controlling item eviction under backpressure.
    /// </value>
    public DropPolicy DropPolicy { get; init; } = DropPolicy.DropOldest;

    /// <summary>
    /// Logical name used as a label in internal metrics for this exporter instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This should be a short, stable identifier (e.g., <c>"default"</c>, <c>"http"</c>,
    /// <c>"jobs"</c>) to keep metric cardinality low while still enabling per‑exporter
    /// visibility in dashboards and alerts.
    /// </para>
    /// <para>
    /// <b>Default:</b> <c>"default"</c>.
    /// </para>
    /// </remarks>
    /// <value>
    /// A non‑empty string used to tag metrics emitted by the exporter.
    /// </value>
    public string Name { get; init; } = "default";
}
