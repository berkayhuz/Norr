// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Collections.Concurrent;

namespace Norr.PerformanceMonitor.Core.Metrics.Aggregation
{
    /// <summary>
    /// Global, process-wide registry that stores metric aggregators by name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Exporters push raw metric observations into aggregators retrieved from this registry via
    /// <see cref="GetOrCreate(string, AggregatorKind)"/>. A separate HTTP endpoint (or another
    /// consumer) can periodically call <see cref="Snapshot"/> to obtain an immutable view of the
    /// current aggregation state for exposition (e.g., Prometheus text format).
    /// </para>
    /// <para>
    /// <b>Thread safety:</b> The registry is thread-safe. Multiple threads can concurrently get or
    /// create aggregators and observe values. <see cref="Snapshot"/> clones aggregator state to avoid
    /// contention and to provide a consistent point-in-time view.
    /// </para>
    /// </remarks>
    internal static class AggregationRegistry
    {
        private static readonly ConcurrentDictionary<string, IAggregator> _aggregators = new();
        private static int _registered;

        /// <summary>
        /// Gets an existing aggregator by name or creates a new one with the specified kind.
        /// </summary>
        /// <param name="name">The logical metric name (e.g., <c>"http_request_duration_ms:DurationMs"</c>).</param>
        /// <param name="kind">The aggregator kind to create when the name is first encountered.</param>
        /// <returns>An <see cref="IAggregator"/> instance associated with the provided name.</returns>
        /// <remarks>
        /// Creation is atomic and race-free thanks to <see cref="ConcurrentDictionary{TKey,TValue}"/>.
        /// The <paramref name="kind"/> parameter is only used on first creation; subsequent calls
        /// with the same name will return the originally created aggregator, irrespective of kind.
        /// </remarks>
        public static IAggregator GetOrCreate(string name, AggregatorKind kind)
        {
            return _aggregators.GetOrAdd(name, _ =>
            {
                Interlocked.Increment(ref _registered);
                return kind switch
                {
                    AggregatorKind.Histogram => new HistogramAggregator(),
                    AggregatorKind.Summary => new SummaryAggregator(),
                    _ => new SummaryAggregator(),
                };
            });
        }

        /// <summary>
        /// Returns a point-in-time snapshot of all registered aggregators.
        /// </summary>
        /// <returns>
        /// An array of tuples containing the aggregator name and a cloned, immutable copy of its state.
        /// </returns>
        /// <remarks>
        /// Each aggregator is cloned via <see cref="IAggregator.Clone"/> to ensure the returned objects
        /// are not mutated by concurrent observations after the snapshot is taken.
        /// </remarks>
        public static (string Name, IAggregator Aggregator)[] Snapshot()
        {
            var arr = new (string, IAggregator)[_aggregators.Count];
            int i = 0;
            foreach (var kvp in _aggregators)
            {
                arr[i++] = (kvp.Key, kvp.Value.Clone());
            }
            return arr;
        }

        /// <summary>
        /// Gets the number of unique aggregators that have been registered.
        /// </summary>
        public static int RegisteredCount => _registered;
    }

    /// <summary>
    /// Specifies the aggregation strategy used for a metric stream.
    /// </summary>
    internal enum AggregatorKind
    {
        /// <summary>
        /// Bucketed distribution suitable for latency and duration metrics.
        /// </summary>
        Histogram,

        /// <summary>
        /// Rolling summary (e.g., count/total/min/max/quantiles) suitable for general numeric metrics.
        /// </summary>
        Summary
    }

    /// <summary>
    /// Represents a metric aggregator that can observe values and expose a cloneable snapshot.
    /// </summary>
    internal interface IAggregator
    {
        /// <summary>
        /// Records a single numeric observation into the aggregator.
        /// </summary>
        /// <param name="value">The observed value.</param>
        void Observe(double value);

        /// <summary>
        /// Creates a snapshot copy of the aggregator with the current state.
        /// </summary>
        /// <returns>
        /// A clone whose state will no longer change as new observations occur in the original.
        /// </returns>
        IAggregator Clone();
    }
}
