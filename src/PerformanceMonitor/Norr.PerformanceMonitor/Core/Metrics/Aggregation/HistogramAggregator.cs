// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


namespace Norr.PerformanceMonitor.Core.Metrics.Aggregation
{
    /// <summary>
    /// Lock-free, bucketed histogram aggregator for numeric observations (typically durations in milliseconds).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The histogram uses fixed, monotonically increasing bucket bounds with an additional <c>+Inf</c> bucket.
    /// Default bounds (ms): <c>1, 5, 10, 25, 50, 100, 250, 500, 1_000, 2_500, 5_000, +Inf</c>.
    /// </para>
    /// <para>
    /// <b>Thread safety:</b> All operations are safe for concurrent use across multiple threads.
    /// Counters use <see cref="System.Threading.Interlocked"/>; <see cref="Sum"/>, <see cref="Min"/>, and
    /// <see cref="Max"/> are maintained using CAS loops tolerant of race conditions. Aggregated values are
    /// approximate under extreme contention, which is acceptable for telemetry aggregation.
    /// </para>
    /// </remarks>
    internal sealed class HistogramAggregator : IAggregator
    {
        private static readonly double[] _bounds =
        [
            1d, 5d, 10d, 25d, 50d, 100d, 250d, 500d, 1_000d, 2_500d, 5_000d
        ];

        // +Inf bucket is the last element
        private readonly long[] _buckets = new long[_bounds.Length + 1];

        private long _count;
        private double _sum;
        private double _min = double.PositiveInfinity;
        private double _max = double.NegativeInfinity;

        /// <summary>
        /// Records a single observation into the histogram.
        /// </summary>
        /// <param name="value">The observed value (typically a duration in milliseconds).</param>
        /// <remarks>
        /// The value is placed into the first bucket whose upper bound is greater than or equal to the value.
        /// Values greater than the largest configured bound go to the <c>+Inf</c> bucket.
        /// </remarks>
        public void Observe(double value)
        {
            // Determine bucket index (first bound strictly less than value moves forward)
            int i = 0;
            while (i < _bounds.Length && value > _bounds[i])
                i++;

            Interlocked.Increment(ref _buckets[i]);
            Interlocked.Increment(ref _count);

            // Sum (approximate) via CAS loop
            double old, @new;
            do
            {
                old = _sum;
                @new = old + value;
            }
            while (Interlocked.CompareExchange(ref _sum, @new, old) != old);

            // Min (best-effort under contention)
            double currentMin;
            do
            {
                currentMin = _min;
                if (value >= currentMin)
                    break;
            } while (Interlocked.CompareExchange(ref _min, value, currentMin) != currentMin);

            // Max (best-effort under contention)
            double currentMax;
            do
            {
                currentMax = _max;
                if (value <= currentMax)
                    break;
            } while (Interlocked.CompareExchange(ref _max, value, currentMax) != currentMax);
        }

        /// <summary>
        /// Gets the backing bucket counters, including the final <c>+Inf</c> bucket.
        /// </summary>
        /// <remarks>
        /// The returned array is the live backing store; treat it as read-only. Values may change
        /// concurrently as observations occur.
        /// </remarks>
        public long[] Buckets => _buckets;

        /// <summary>
        /// Gets the immutable array of bucket upper bounds (excluding the implicit <c>+Inf</c> bucket).
        /// </summary>
        public double[] Bounds => _bounds;

        /// <summary>
        /// Gets the total number of observations recorded.
        /// </summary>
        public long Count => Interlocked.Read(ref _count);

        /// <summary>
        /// Gets the arithmetic sum of all observed values.
        /// </summary>
        public double Sum => _sum;

        /// <summary>
        /// Gets the minimum observed value.
        /// </summary>
        public double Min => _min;

        /// <summary>
        /// Gets the maximum observed value.
        /// </summary>
        public double Max => _max;

        /// <summary>
        /// Creates a snapshot copy of the histogram with current state.
        /// </summary>
        /// <returns>
        /// A new <see cref="HistogramAggregator"/> instance containing a point-in-time copy of buckets
        /// and summary statistics. Subsequent observations will not affect the snapshot.
        /// </returns>
        public IAggregator Clone()
        {
            var copy = new HistogramAggregator();
            Array.Copy(_buckets, copy._buckets, _buckets.Length);
            copy._count = _count;
            copy._sum = _sum;
            copy._min = _min;
            copy._max = _max;
            return copy;
        }
    }
}
