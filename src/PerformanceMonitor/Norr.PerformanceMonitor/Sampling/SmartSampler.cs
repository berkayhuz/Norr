// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Diagnostics;
using System.Runtime.CompilerServices;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Configuration;

namespace Norr.PerformanceMonitor.Sampling;

/// <summary>
/// Thread-safe, low-overhead sampler with deterministic and random modes, optional rate limiting,
/// and per-name probability overrides.
/// </summary>
/// <remarks>
/// <para>
/// <b>Decision sources</b>:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>Deterministic</b> — Uses a 64-bit hash of <c>(name, seed)</c> to produce a stable decision
///     per operation name across processes/hosts. Suitable for consistent sampling in distributed systems.
///   </description></item>
///   <item><description>
///     <b>Random</b> — Uses <see cref="Random.Shared"/> for a fast, contention-friendly decision.
///   </description></item>
/// </list>
/// <para>
/// <b>Rate limiting</b> (optional): If <see cref="SamplingOptions.MaxSamplesPerSecond"/> is set, a token-bucket
/// is applied <em>after</em> the probability decision to cap the number of accepted samples per second, with an
/// optional <see cref="SamplingOptions.RateLimiterBurst"/>.
/// </para>
/// <para>
/// <b>Name overrides</b>: Specific operation names can override the base probability via
/// <see cref="SamplingOptions.NameProbabilities"/>.
/// </para>
/// <para>
/// <b>Thread safety</b>: All members are safe for concurrent use by multiple threads.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp"><![CDATA[
/// var sampler = new SmartSampler(new SamplingOptions
/// {
///     Probability = 0.10,                 // 10% baseline
///     Mode = SamplerMode.Deterministic,   // consistent across nodes
///     Seed = 12345UL,                     // optional
///     MaxSamplesPerSecond = 200,          // optional rate limit
///     RateLimiterBurst = 400,
///     NameProbabilities = new Dictionary<string, double>
///     {
///         ["HTTP GET /health"] = 0.01,    // 1% for noisy endpoint
///         ["Orders/Create"] = 1.0         // always sample business-critical op
///     }
/// });
///
/// if (sampler.ShouldSample("Orders/Create"))
/// {
///     // measure…
///
/// }
/// ]]></code>
/// </example>
public sealed class SmartSampler : ISampler, IDisposable
{
    private readonly double _p;                       // [0..1]
    private readonly SamplerMode _mode;
    private readonly ulong _seed;
    private readonly IReadOnlyDictionary<string, double> _overrides;

    private readonly TokenBucket? _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartSampler"/> class from the provided options.
    /// </summary>
    /// <param name="o">Sampling configuration (probability, mode, seed, rate limit, and name overrides).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="o"/> is <see langword="null"/>.</exception>
    public SmartSampler(SamplingOptions o)
    {
        if (o is null)
            throw new ArgumentNullException(nameof(o));

        _p = Clamp01(o.Probability);
        _mode = o.Mode;
        _seed = o.Seed ?? 0xCAFEBABE_D15EA5E5UL;
        _overrides = o.NameProbabilities ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        if (o.MaxSamplesPerSecond.HasValue && o.MaxSamplesPerSecond.Value > 0)
        {
            var burst = Math.Max(1, o.RateLimiterBurst);
            _rateLimiter = new TokenBucket(o.MaxSamplesPerSecond.Value, burst);
        }
    }

    /// <summary>
    /// Determines whether the given operation <paramref name="name"/> should be sampled.
    /// </summary>
    /// <param name="name">Operation name (should be stable and low-cardinality for deterministic mode).</param>
    /// <returns>
    /// <see langword="true"/> if the operation passes probability (including name override) and the
    /// optional rate limit; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Decision flow:
    /// </para>
    /// <list type="number">
    ///   <item><description>Resolve effective probability: base <c>p</c> optionally overridden by <paramref name="name"/>.</description></item>
    ///   <item><description>Make the probability decision using deterministic or random mode.</description></item>
    ///   <item><description>If accepted, apply the token-bucket rate limit (if configured).</description></item>
    /// </list>
    /// </remarks>
    public bool ShouldSample(string name)
    {
        // 1) Effective probability
        double p = _p;
        if (_overrides.Count > 0 && name is not null && _overrides.TryGetValue(name, out var po))
            p = Clamp01(po);

        if (p <= 0)
            return false;
        if (p >= 1)
            return PassRateLimit();

        // 2) Probability decision
        bool accepted;
        if (_mode == SamplerMode.Deterministic)
        {
            // Deterministic: hash64(name, seed)/Max < p
            var h = Hash64FNV1a(name.AsSpan(), _seed);
            var v = (h / (double)ulong.MaxValue); // normalize to [0,1)
            accepted = v < p;
        }
        else
        {
            // Random: Random.Shared is thread-safe and fast
            accepted = Random.Shared.NextDouble() < p;
        }

        if (!accepted)
            return false;

        // 3) Optional rate limit
        return PassRateLimit();
    }

    /// <summary>
    /// Releases any resources associated with the internal rate limiter (if enabled).
    /// </summary>
    public void Dispose() => _rateLimiter?.Dispose();

    // ---- Helpers ------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Clamp01(double x) => x < 0 ? 0 : (x > 1 ? 1 : x);

    /// <summary>
    /// 64‑bit FNV‑1a hash with light mixing; deterministic and fast. Accepts a seed for stability across nodes.
    /// </summary>
    private static ulong Hash64FNV1a(ReadOnlySpan<char> s, ulong seed)
    {
        const ulong fnvOffset = 14695981039346656037UL;
        const ulong fnvPrime = 1099511628211UL;

        unchecked
        {
            ulong h = fnvOffset ^ seed;
            for (int i = 0; i < s.Length; i++)
            {
                // char is 2 bytes; mixing the low byte is adequate for ASCII-heavy names
                h ^= (byte)s[i];
                h *= fnvPrime;
                // light avalanche
                h ^= h >> 32;
                h *= 0x9E3779B185EBCA87UL;
            }
            // finalization
            h ^= h >> 33;
            h *= 0xff51afd7ed558ccdUL;
            h ^= h >> 33;
            h *= 0xc4ceb9fe1a85ec53UL;
            h ^= h >> 33;
            return h;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool PassRateLimit()
        => _rateLimiter is null || _rateLimiter.TryConsume(1.0);

    // ---------------- Token‑bucket rate limiter ----------------
    /// <summary>
    /// Simple token-bucket used to cap accepted samples per second with an optional burst.
    /// </summary>
    private sealed class TokenBucket : IDisposable
    {
        private readonly double _rate;       // tokens / second
        private readonly double _capacity;   // burst capacity
        private double _tokens;              // current tokens
        private long _lastTicks;             // Stopwatch ticks of last refill
        private readonly object _lock = new();

        public TokenBucket(double ratePerSecond, int burstCapacity)
        {
            _rate = ratePerSecond;
            _capacity = burstCapacity;
            _tokens = burstCapacity;
            _lastTicks = Stopwatch.GetTimestamp();
        }

        public bool TryConsume(double tokens)
        {
            lock (_lock)
            {
                Refill();

                if (_tokens + 1e-9 >= tokens) // small numeric tolerance
                {
                    _tokens -= tokens;
                    return true;
                }

                return false;
            }
        }

        private void Refill()
        {
            var now = Stopwatch.GetTimestamp();
            var elapsed = (now - _lastTicks) / (double)Stopwatch.Frequency; // seconds
            if (elapsed <= 0)
                return;

            var add = elapsed * _rate;
            _tokens = Math.Min(_capacity, _tokens + add);
            _lastTicks = now;
        }

        public void Dispose()
        {
            // no unmanaged resources
        }
    }
}
