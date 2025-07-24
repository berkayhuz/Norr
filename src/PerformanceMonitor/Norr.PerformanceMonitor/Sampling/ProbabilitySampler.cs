using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Configuration;

namespace Norr.PerformanceMonitor.Sampling;

/// <summary>
/// Implements a probabilistic sampler that decides whether to record a metric based
/// on a given sampling rate. The sampling rate is configurable via <see cref="SamplingOptions.Probability"/>.
/// This implementation uses a simple and efficient Xorshift RNG to generate random values for sampling.
/// </summary>
public sealed class ProbabilitySampler : ISampler
{
    private readonly double _p;          // Sampling probability (0.0 – 1.0)
    private readonly ulong _mask;       // Threshold for RNG output

    // Xorshift64* RNG state, initialized with the provided seed (or default)
    private ulong _state;

    /// <summary>
    /// Initializes a new <see cref="ProbabilitySampler"/> using the given <see cref="SamplingOptions"/>.
    /// </summary>
    public ProbabilitySampler(SamplingOptions o)
    {
        _p = Math.Clamp(o.Probability, 0, 1);    // Ensure the probability is within [0, 1]
        _state = o.Seed ?? (ulong)Environment.TickCount64;  // Default to the system tick count
        _mask = (ulong)(ulong.MaxValue * _p);      // Create a mask based on probability
    }

    /// <inheritdoc />
    public bool ShouldSample(string _)
    {
        // Xorshift64* RNG implementation: fast, deterministic randomness
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;

        var rnd = _state * 0x2545F4914F6CDD1DUL;  // RNG multiplier
        return rnd <= _mask;    // If the random value is smaller than the threshold, sample
    }
}
