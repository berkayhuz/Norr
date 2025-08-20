// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using BenchmarkDotNet.Attributes;

using Norr.PerformanceMonitor.Configuration;
using Norr.PerformanceMonitor.Sampling;

namespace Norr.PerformanceMonitor.Benchmarks;

/// <summary>
/// BenchmarkDotNet benchmarks for measuring the performance of <see cref="SmartSampler"/> 
/// in deterministic and random sampling modes.
/// </summary>
/// <remarks>
/// Two scenarios are benchmarked:
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="Deterministic_ByName"/> — deterministic sampling using a fixed seed and an operation name.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="Random_NoName"/> — probabilistic sampling without specifying a name (random mode).
/// </description>
/// </item>
/// </list>
/// </remarks>
[MemoryDiagnoser]
public class SmartSamplerBenchmarks
{
    private SmartSampler _det = null!;
    private SmartSampler _rnd = null!;

    /// <summary>
    /// Initializes the deterministic and random mode <see cref="SmartSampler"/> instances
    /// before the benchmarks are executed.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _det = new SmartSampler(new SamplingOptions
        {
            Probability = 0.25,
            Mode = SamplerMode.Deterministic,
            Seed = 42
        });

        _rnd = new SmartSampler(new SamplingOptions
        {
            Probability = 0.25,
            Mode = SamplerMode.Random
        });
    }

    /// <summary>
    /// Benchmark that tests deterministic sampling using a fixed seed and a specific name.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the sampler chooses to sample the provided name; otherwise, <c>false</c>.
    /// </returns>
    [Benchmark]
    public bool Deterministic_ByName() => _det.ShouldSample("OrderService.PlaceOrder");

    /// <summary>
    /// Benchmark that tests random sampling without providing a name (empty string is used).
    /// </summary>
    /// <returns>
    /// <c>true</c> if the sampler chooses to sample in random mode; otherwise, <c>false</c>.
    /// </returns>
    [Benchmark]
    public bool Random_NoName() => _rnd.ShouldSample(string.Empty); // use empty instead of null
}
