// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


namespace Norr.PerformanceMonitor.Exporters.InMemory;

/// <summary>
/// Simple rolling window counter with 1‑second buckets over an N‑second window.
/// </summary>
/// <remarks>
/// <para>
/// Designed for lightweight rate calculations (e.g., drops per second) without high GC pressure.
/// Values are aggregated into second‑resolution buckets keyed by Unix epoch seconds. The effective
/// rate is computed as the sum of the buckets divided by the configured window size.
/// </para>
/// <para>
/// <b>Time base:</b> Uses <see cref="DateTimeOffset.UtcNow"/> and <see cref="DateTimeOffset.ToUnixTimeSeconds"/>
/// for stable, timezone‑agnostic measurement. Clock adjustments (e.g., NTP) may affect bucket keys if
/// the system clock jumps significantly.
/// </para>
/// <para>
/// <b>Storage model:</b> Maintains a dictionary of second → count for the last <see cref="_windowSeconds"/>
/// seconds. Old buckets are pruned on each mutation/read to cap memory and lookup costs. At steady state,
/// the dictionary holds at most <c>_windowSeconds</c> entries.
/// </para>
/// </remarks>
/// <threadsafety>
/// All public members are thread‑safe. A single lock serializes updates and reads to the internal dictionary.
/// </threadsafety>
/// <example>
/// Basic usage:
/// <code language="csharp">
/// var rw = new RollingWindowCounter(windowSeconds: 60);
/// rw.Add();          // +1 event
/// rw.Add(5);         // +5 events
/// var rps = rw.RatePerSecond(); // average events/sec over last 60 seconds
/// </code>
/// </example>
internal sealed class RollingWindowCounter
{
    private readonly int _windowSeconds;
    private readonly Dictionary<int, long> _buckets = new(); // epoch-sec -> count
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new rolling window counter.
    /// </summary>
    /// <param name="windowSeconds">
    /// Window size in seconds. The counter averages totals over this horizon.
    /// Must be a positive integer. Default is 60 seconds.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="windowSeconds"/> is less than or equal to zero.
    /// </exception>
    public RollingWindowCounter(int windowSeconds = 60)
    {
        if (windowSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(windowSeconds));
        _windowSeconds = windowSeconds;
    }

    /// <summary>
    /// Adds a value to the current 1‑second bucket.
    /// </summary>
    /// <param name="value">
    /// Amount to add for the current second. Defaults to <c>1</c>. Negative values are not validated
    /// and will be recorded as provided.
    /// </param>
    /// <remarks>
    /// The method updates the bucket corresponding to the current UTC second and prunes expired buckets.
    /// </remarks>
    public void Add(long value = 1)
    {
        var sec = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        lock (_lock)
        {
            _buckets.TryGetValue(sec, out var cur);
            _buckets[sec] = cur + value;
            PruneLocked(sec);
        }
    }

    /// <summary>
    /// Computes the average rate (per second) over the configured window.
    /// </summary>
    /// <returns>
    /// The total sum of values in the active window divided by <c>windowSeconds</c>.
    /// Returns <c>0</c> when no values have been recorded in the window.
    /// </returns>
    /// <remarks>
    /// Prunes expired buckets before computing the rate. Complexity is O(k) where k is the number
    /// of live buckets (≤ window size).
    /// </remarks>
    public double RatePerSecond()
    {
        var sec = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        lock (_lock)
        {
            PruneLocked(sec);
            long sum = 0;
            foreach (var kvp in _buckets)
                sum += kvp.Value;
            return sum / (double)_windowSeconds;
        }
    }

    /// <summary>
    /// Removes buckets that fall outside the rolling window. Must be called under <see cref="_lock"/>.
    /// </summary>
    /// <param name="nowSec">Current Unix time in seconds.</param>
    private void PruneLocked(int nowSec)
    {
        var min = nowSec - _windowSeconds + 1;
        // Take a snapshot of keys to avoid mutation during enumeration.
        var keys = _buckets.Keys.ToArray();
        foreach (var k in keys)
        {
            if (k < min)
                _buckets.Remove(k);
        }
    }
}
