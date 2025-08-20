// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using BenchmarkDotNet.Attributes;

using Norr.PerformanceMonitor.Telemetry;

namespace Norr.PerformanceMonitor.Benchmarks;

/// <summary>
/// Benchmarks for measuring the overhead of creating and disposing <see cref="TagContext"/> 
/// instances with varying numbers of tags.
/// </summary>
/// <remarks>
/// <para>
/// These benchmarks use <see cref="BenchmarkDotNet"/> to measure the cost of creating
/// and disposing a tag context containing a small number of tags.
/// </para>
/// <para>
/// Each benchmark simulates the typical usage pattern of <c>TagContext.Begin</c>
/// in application code: begin a context, use it within a scope (<c>using</c>),
/// and automatically dispose it at the end.
/// </para>
/// </remarks>
[MemoryDiagnoser, ThreadingDiagnoser]
public class TagContextBenchmarks
{
    /// <summary>
    /// Measures the allocation and disposal cost of a <see cref="TagContext"/> containing exactly one tag.
    /// </summary>
    [Benchmark]
    public void BeginDispose_1Tag()
    {
        using var _ = TagContext.Begin("k", "v");
    }

    /// <summary>
    /// Measures the allocation and disposal cost of a <see cref="TagContext"/> containing four tags.
    /// </summary>
    [Benchmark]
    public void BeginDispose_4Tags()
    {
        using var _ = TagContext.Begin(new[]
        {
            new KeyValuePair<string, object?>("a", "1"),
            new KeyValuePair<string, object?>("b", "2"),
            new KeyValuePair<string, object?>("c", "3"),
            new KeyValuePair<string, object?>("d", "4"),
        });
    }
}
