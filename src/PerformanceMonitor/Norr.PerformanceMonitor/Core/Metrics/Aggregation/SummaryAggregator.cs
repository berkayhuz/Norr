// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable

using System;

namespace Norr.PerformanceMonitor.Core.Metrics.Aggregation
{
    /// <summary>
    /// Maintains a rolling summary of a stream of <see cref="double"/> observations:
    /// <list type="bullet">
    ///   <item><description>Total <c>count</c> and <c>sum</c></description></item>
    ///   <item><description>Approximate percentiles p50, p90, p95, p99 via the P² algorithm</description></item>
    ///   <item><description>Observed <c>min</c> and <c>max</c> bounds for robust clamping</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Percentiles.</b> Percentiles are estimated using the P² (P-squared) algorithm
    /// described by Jain &amp; Chlamtac (1985). P² keeps five markers per target quantile and
    /// updates them in amortized O(1) time per sample, allowing streaming estimation without
    /// retaining the entire sample set.
    /// </para>
    /// <para>
    /// <b>Thread safety.</b> The estimator uses a per-quantile lock and simple field updates
    /// for counters and range. It is designed for common telemetry pipelines where occasional
    /// races have negligible impact on aggregates. If you require strictly lock-free or fully
    /// consistent snapshots under heavy contention, wrap access with external synchronization
    /// or prefer histogram-based aggregators.
    /// </para>
    /// <para>
    /// <b>Accuracy.</b> P² is an approximation; early in the stream (fewer than 5 samples),
    /// estimates are reported as <see cref="double.NaN"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var agg = new SummaryAggregator();
    /// agg.Observe(12.3);
    /// agg.Observe(9.7);
    /// var snap = agg.Snapshot();
    /// Console.WriteLine($"n={snap.count} sum={snap.sum} p50={snap.p50}");
    /// </code>
    /// </example>
    internal sealed class SummaryAggregator : IAggregator
    {
        // Target quantiles
        private readonly P2Quantile _q50 = new(0.50);
        private readonly P2Quantile _q90 = new(0.90);
        private readonly P2Quantile _q95 = new(0.95);
        private readonly P2Quantile _q99 = new(0.99);

        // Range tracking
        private double _min = double.PositiveInfinity;
        private double _max = double.NegativeInfinity;

        // Simple aggregates
        private long _count;
        private double _sum;

        /// <summary>
        /// Records a single observation into the summary.
        /// </summary>
        /// <param name="value">Observed value.</param>
        /// <remarks>
        /// Updates P² markers (p50, p90, p95, p99), increments count/sum, and maintains min/max.
        /// </remarks>
        public void Observe(double value)
        {
            // Percentiles
            _q50.Add(value);
            _q90.Add(value);
            _q95.Add(value);
            _q99.Add(value);

            // Aggregates
            _count++;
            _sum += value;

            // Range
            if (value < _min)
                _min = value;
            if (value > _max)
                _max = value;
        }

        /// <summary>
        /// Returns a point-in-time snapshot of summary statistics.
        /// </summary>
        /// <returns>
        /// A tuple (count, sum, p50, p90, p95, p99). If fewer than five observations have been
        /// seen, percentile estimates are <see cref="double.NaN"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Percentile outputs are clamped to the current [min, max] range to improve robustness
        /// against transient marker overshoot. Monotonicity is enforced (p50 ≤ p90 ≤ p95 ≤ p99).
        /// </para>
        /// <para>
        /// A small, range-scaled deadband snaps very near-min outputs to the exact min to reduce
        /// flicker when distributions are narrow.
        /// </para>
        /// </remarks>
        public (long count, double sum, double p50, double p90, double p95, double p99) Snapshot()
        {
            var count = _count;
            var sum = _sum;

            var p50 = _q50.Estimate;
            var p90 = _q90.Estimate;
            var p95 = _q95.Estimate;
            var p99 = _q99.Estimate;

            // Guard for early calls: not enough samples for P²
            if (double.IsNaN(p50) || double.IsNaN(p90) || double.IsNaN(p95) || double.IsNaN(p99))
                return (count, sum, double.NaN, double.NaN, double.NaN, double.NaN);

            // Clamp to observed range if we have a valid window
            var min = _min;
            var max = _max;
            if (!(min <= max))
            {
                // No data yet (should not happen if percentiles are valid), normalize to zeros.
                min = 0;
                max = 0;
            }

            p50 = Math.Clamp(p50, min, max);
            p90 = Math.Clamp(p90, min, max);
            p95 = Math.Clamp(p95, min, max);
            p99 = Math.Clamp(p99, min, max);

            // Enforce monotonicity across quantiles
            if (p90 < p50)
                p90 = p50;
            if (p95 < p90)
                p95 = p90;
            if (p99 < p95)
                p99 = p95;

            // Scaled deadband: if outputs are extremely close to min, snap to min
            var range = max - min;
            if (range > 0)
            {
                const double frac = 5e-3; // 0.5%
                var eps = range * frac;

                if (Math.Abs(p50 - min) <= eps)
                    p50 = min;
                if (Math.Abs(p90 - min) <= eps)
                    p90 = min; // optional; keeps visual stability
            }

            return (count, sum, p50, p90, p95, p99);
        }

        /// <summary>
        /// Creates a snapshot copy of the aggregator with current state.
        /// </summary>
        /// <returns>
        /// A new <see cref="SummaryAggregator"/> containing copies of counters, range, and P² markers.
        /// </returns>
        public IAggregator Clone()
        {
            var c = new SummaryAggregator
            {
                _count = _count,
                _sum = _sum,
                _min = _min, // copied
                _max = _max  // copied
            };
            c._q50.CopyFrom(_q50);
            c._q90.CopyFrom(_q90);
            c._q95.CopyFrom(_q95);
            c._q99.CopyFrom(_q99);
            return c;
        }

        /// <summary>
        /// Streaming percentile estimator using the P² algorithm (Jain &amp; Chlamtac, 1985).
        /// </summary>
        /// <remarks>
        /// Maintains five marker positions and heights to approximate a target quantile <c>p</c>
        /// without storing the full sample set. After initialization with the first five samples,
        /// each new observation updates marker positions; heights are adjusted parabolically,
        /// with a linear fallback when the parabolic prediction would violate monotonicity.
        /// </remarks>
        private sealed class P2Quantile
        {
            private readonly object _gate = new();
            private readonly double _p;

            // Marker heights q0..q4 (min .. max)
            private readonly double[] _q = new double[5];

            // Marker positions (n), desired positions (np), and desired increments (dn)
            private readonly double[] _n = new double[5];
            private readonly double[] _np = new double[5];
            private readonly double[] _dn = new double[5];

            private int _count;

            /// <summary>
            /// Initializes a new P² estimator for the target quantile <paramref name="p"/>.
            /// </summary>
            /// <param name="p">Target quantile in (0, 1), e.g., 0.5 for the median.</param>
            public P2Quantile(double p) => _p = p;

            /// <summary>
            /// Adds a single observation and updates the percentile estimate.
            /// </summary>
            /// <param name="x">Observed value.</param>
            public void Add(double x)
            {
                // Treat extremely small magnitudes as zero to suppress subnormal noise
                if (x is > -1e-12 and < 1e-12)
                    x = 0.0;

                lock (_gate)
                {
                    // Initialization phase: collect first five values
                    if (_count < 5)
                    {
                        _q[_count++] = x;
                        if (_count == 5)
                        {
                            Array.Sort(_q);              // q0..q4
                            for (int i = 0; i < 5; i++)
                                _n[i] = i + 1;

                            _np[0] = 1;
                            _np[1] = 1 + 2 * _p;
                            _np[2] = 1 + 4 * _p;
                            _np[3] = 3 + 2 * _p;
                            _np[4] = 5;

                            _dn[0] = 0;
                            _dn[1] = _p / 2;
                            _dn[2] = _p;
                            _dn[3] = (1 + _p) / 2;
                            _dn[4] = 1;
                        }
                        return;
                    }

                    // Cell selection (ties handled consistently)
                    int k;
                    if (x <= _q[0])
                    {
                        if (x < _q[0])
                            _q[0] = x; // new minimum
                        k = 0;
                    }
                    else if (x <= _q[1])
                        k = 0;
                    else if (x <= _q[2])
                        k = 1;
                    else if (x <= _q[3])
                        k = 2;
                    else
                    {
                        if (x > _q[4])
                            _q[4] = x; // new maximum
                        k = 3;
                    }

                    for (int i = k + 1; i < 5; i++)
                        _n[i]++;
                    for (int i = 0; i < 5; i++)
                        _np[i] += _dn[i];

                    // Adjust internal markers toward desired positions
                    for (int i = 1; i <= 3; i++)
                    {
                        var d = _np[i] - _n[i];
                        var canInc = _n[i + 1] - _n[i] > 1;
                        var canDec = _n[i - 1] - _n[i] < -1;

                        if ((d >= 1 && canInc) || (d <= -1 && canDec))
                        {
                            var s = Math.Sign(d);
                            var qi = Parabolic(i, s);

                            // Parabolic proposal must preserve order; otherwise do linear step
                            if (_q[i - 1] < qi && qi < _q[i + 1])
                                _q[i] = qi;
                            else
                                _q[i] += s * Linear(i, s);

                            _n[i] += s;
                        }
                    }
                }
            }

            /// <summary>
            /// Parabolic (quadratic) height adjustment for marker <paramref name="i"/>.
            /// </summary>
            private double Parabolic(int i, int d)
            {
                return _q[i] + d / (_n[i + 1] - _n[i - 1]) *
                       ((_n[i] - _n[i - 1] + d) * (_q[i + 1] - _q[i]) / (_n[i + 1] - _n[i]) +
                        (_n[i + 1] - _n[i] - d) * (_q[i] - _q[i - 1]) / (_n[i] - _n[i - 1]));
            }

            /// <summary>
            /// Linear fallback height adjustment when the parabolic prediction would violate ordering.
            /// </summary>
            private double Linear(int i, int d) => (_q[i + d] - _q[i]) / (_n[i + d] - _n[i]);

            /// <summary>
            /// Gets the current percentile estimate.
            /// Returns <see cref="double.NaN"/> until at least five observations have been processed.
            /// </summary>
            public double Estimate
            {
                get
                {
                    lock (_gate)
                        return _count < 5 ? double.NaN : _q[2];
                }
            }

            /// <summary>
            /// Copies the internal marker state from another estimator.
            /// </summary>
            /// <param name="other">Source estimator to copy from.</param>
            public void CopyFrom(P2Quantile other)
            {
                lock (_gate)
                    lock (other._gate)
                    {
                        Array.Copy(other._q, _q, 5);
                        Array.Copy(other._n, _n, 5);
                        Array.Copy(other._np, _np, 5);
                        Array.Copy(other._dn, _dn, 5);
                        _count = other._count;
                    }
            }
        }
    }
}
