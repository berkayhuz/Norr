// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using BenchmarkDotNet.Attributes;

using Norr.PerformanceMonitor.Configuration;
using Norr.PerformanceMonitor.Sampling;

namespace Norr.PerformanceMonitor.Benchmarks;

/// <summary>
/// BenchmarkDotNet benchmarks for <see cref="ConcurrentBloomDuplicateGuard"/> performance
/// and duplicate suppression accuracy.
/// </summary>
/// <remarks>
/// This benchmark measures the behavior of the duplicate guard when the same key is evaluated
/// multiple times within its configured cooldown window.
/// </remarks>
[MemoryDiagnoser]
public class DuplicateGuardBenchmarks
{
    private ConcurrentBloomDuplicateGuard _guard = null!;

    /// <summary>
    /// Initializes the <see cref="ConcurrentBloomDuplicateGuard"/> instance
    /// before benchmarks are executed.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _guard = new ConcurrentBloomDuplicateGuard(new DuplicateGuardOptions
        {
            BitCount = 1 << 20, // ~1M bits
            CoolDown = TimeSpan.FromSeconds(10)
        });
    }

    /// <summary>
    /// Benchmark that calls <see cref="ConcurrentBloomDuplicateGuard.ShouldEmit(string, DateTime)"/>
    /// twice in quick succession with the same key, expecting only the first call to return <c>true</c>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if exactly one of the two calls returned <c>true</c>; otherwise, <c>false</c>.
    /// </returns>
    [Benchmark]
    public bool ShouldEmit_SameKey()
    {
        var now = DateTime.UtcNow;
        bool first = _guard.ShouldEmit("OrderService.PlaceOrder:duration", now);
        bool second = _guard.ShouldEmit("OrderService.PlaceOrder:duration", now);
        return first ^ second; // only first call should be true
    }
}
