using System.Collections;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Configuration;

namespace Norr.PerformanceMonitor.Sampling;

/// <summary>
/// Bloom-filter implementation that suppresses repetitive metrics to avoid
/// flooding exporters / alert channels.  
/// The hash combines the metric name and a time-bucket derived from
/// <see cref="DuplicateGuardOptions.CoolDown"/>, so entries automatically
/// expire without having to track timestamps per key.
/// </summary>
public sealed class BloomDuplicateGuard : IDuplicateGuard, IDisposable
{
    private readonly BitArray _bits;
    private readonly int _mask;
    private readonly TimeSpan _coolDown;
    private readonly Timer _sweeper;

    /// <summary>
    /// Creates a guard with the specified Bloom-filter size and cooldown window.
    /// </summary>
    /// <param name="opt">
    /// Options that define filter bit-count and cooldown period.
    /// </param>
    public BloomDuplicateGuard(DuplicateGuardOptions opt)
    {
        _bits = new BitArray(opt.BitCount, false);
        _mask = opt.BitCount - 1;               // bitcount must be power-of-two
        _coolDown = opt.CoolDown;

        // Periodic sweeper clears the entire bit-array once every cooldown
        // interval → avoids per-entry timestamp storage.
        _sweeper = new Timer(_ => _bits.SetAll(false),
                             null,
                             opt.CoolDown,
                             opt.CoolDown);
    }

    /// <inheritdoc />
    public bool ShouldEmit(string name, DateTime nowUtc)
    {
        // Two fast hashes => “metric name” + “time bucket”
        var bucket = (ulong)nowUtc.Ticks / (ulong)_coolDown.Ticks;
        var h1 = (int)(XXHash64(name) ^ bucket) & _mask;
        var h2 = (int)(XXHash64(name) ^ (bucket << 1)) & _mask;

        if (_bits[h1] && _bits[h2])
            return false;           // already present → suppress

        _bits[h1] = _bits[h2] = true;
        return true;
    }

    /// <inheritdoc />
    public void Dispose() => _sweeper.Dispose();

    // ----------------------------------------------------------------------

    /// <summary>
    /// Ultra-small 64-bit xxHash-like hash; collision risk is low and performance
    /// is sufficient for per-metric calls.
    /// </summary>
    private static ulong XXHash64(string str)
    {
        unchecked
        {
            const ulong fnvOffset = 14695981039346656037UL;
            const ulong fnvPrime = 1099511628211UL;
            var hash = fnvOffset;
            foreach (var c in str)
                hash = (hash ^ (byte)c) * fnvPrime;
            return hash;
        }
    }
}
