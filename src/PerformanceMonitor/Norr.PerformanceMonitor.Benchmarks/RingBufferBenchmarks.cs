// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 
using BenchmarkDotNet.Attributes;

using Norr.PerformanceMonitor.Exporters.InMemory;

namespace Norr.PerformanceMonitor.Benchmarks;

/// <summary>
/// BenchmarkDotNet benchmarks for measuring the performance of the
/// <see cref="RingBufferBenchAdapter{T}"/> when adding elements and taking snapshots.
/// </summary>
/// <remarks>
/// Two scenarios are benchmarked:
/// <list type="bullet">
/// <item><description><see cref="Add_DropOldest"/> — adding items beyond capacity to test drop-oldest behavior.</description></item>
/// <item><description><see cref="Snapshot"/> — retrieving a snapshot of the buffer contents.</description></item>
/// </list>
/// </remarks>
[MemoryDiagnoser]
public class RingBufferBenchmarks
{
    private RingBufferBenchAdapter<int> _rb = null!;

    /// <summary>
    /// The capacity of the ring buffer to use during the benchmarks.
    /// </summary>
    [Params(128, 4096)]
    public int Capacity
    {
        get; set;
    }

    /// <summary>
    /// Initializes the ring buffer instance before benchmarks run.
    /// </summary>
    [GlobalSetup]
    public void Setup() => _rb = new RingBufferBenchAdapter<int>(Capacity);

    /// <summary>
    /// Benchmark that fills the buffer with twice its capacity,
    /// forcing it to drop the oldest items once full.
    /// </summary>
    [Benchmark]
    public void Add_DropOldest()
    {
        for (int i = 0; i < Capacity * 2; i++)
            _rb.Add(i);
    }

    /// <summary>
    /// Benchmark that takes a snapshot of the current buffer contents.
    /// </summary>
    [Benchmark]
    public void Snapshot() => _rb.Snapshot();
}
