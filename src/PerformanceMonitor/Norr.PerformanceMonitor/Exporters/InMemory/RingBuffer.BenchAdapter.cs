// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.ComponentModel;

using Norr.PerformanceMonitor.Exporters.Core;

namespace Norr.PerformanceMonitor.Exporters.InMemory;

/// <summary>
/// Benchmark-only façade over <see cref="RingBuffer{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type exists *solely* to enable controlled microbenchmarks without exposing the
/// internal <see cref="RingBuffer{T}"/> surface publicly. It intentionally offers a very
/// small API surface that maps to representative operations needed in performance tests:
/// enqueueing with a fixed backpressure strategy and requesting a snapshot.
/// </para>
/// <para>
/// <b>Not for production:</b> The adapter bypasses input validation and flexibility that
/// a production-facing wrapper would typically provide (e.g., selecting a drop policy per
/// call, exposing enumeration, etc.). Use it only in benchmark projects and test suites.
/// </para>
/// <para>
/// <b>Backpressure behavior:</b> <see cref="Add(T)"/> always uses
/// <see cref="DropPolicy.DropOldest"/>. This favors recency under pressure and is a common
/// choice for queue-like telemetry buffers. The method discards the oldest items when the
/// buffer is full, mirroring typical ring buffer semantics.
/// </para>
/// <para>
/// <b>Snapshot semantics:</b> <see cref="Snapshot"/> delegates to the underlying
/// <see cref="RingBuffer{T}"/> snapshot routine for timing/measurement purposes. The adapter
/// does not expose the snapshot contents; benchmarks can still measure allocation costs,
/// synchronization, and algorithmic overhead of the operation itself.
/// </para>
/// <para>
/// <b>Threading:</b> The adapter forwards calls directly to the underlying ring buffer,
/// inheriting its thread-safety characteristics. Consult <see cref="RingBuffer{T}"/> for
/// concurrency guarantees before using this type in multi-threaded benchmarks.
/// </para>
/// </remarks>
/// <typeparam name="T">
/// The element type stored in the ring buffer. When nullable reference types are enabled,
/// <c>T</c> may be a nullable reference if the underlying buffer supports it.
/// </typeparam>
/// <example>
/// Basic usage in a BenchmarkDotNet microbenchmark:
/// <code language="csharp">
/// using BenchmarkDotNet.Attributes;
/// using Norr.PerformanceMonitor.Exporters.InMemory;
///
/// public class RingBufferBench
/// {
///     private RingBufferBenchAdapter&lt;int&gt; _adapter = null!;
///
///     [GlobalSetup]
///     public void Setup() => _adapter = new RingBufferBenchAdapter&lt;int&gt;(capacity: 10_000);
///
///     [Benchmark]
///     public void Enqueue()
///     {
///         for (var i = 0; i &lt; 1_000; i++)
///         {
///             _adapter.Add(i);
///         }
///     }
///
///     [Benchmark]
///     public void TakeSnapshot()
///     {
///         _adapter.Snapshot();
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="RingBuffer{T}"/>
/// <seealso cref="DropPolicy"/>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RingBufferBenchAdapter<T>
{
    private readonly RingBuffer<T> _inner;

    /// <summary>
    /// Initializes a new benchmark adapter with the specified ring buffer capacity.
    /// </summary>
    /// <param name="capacity">
    /// The maximum number of items the underlying <see cref="RingBuffer{T}"/> can hold.
    /// Must be a positive integer; behavior for non-positive values mirrors the underlying
    /// implementation.
    /// </param>
    public RingBufferBenchAdapter(int capacity)
        => _inner = new RingBuffer<T>(capacity);

    /// <summary>
    /// Adds an item to the underlying buffer using <see cref="DropPolicy.DropOldest"/>
    /// when the buffer is full.
    /// </summary>
    /// <param name="item">The item to add to the buffer.</param>
    /// <remarks>
    /// The operation forwards directly to <see cref="RingBuffer{T}.Add(T, DropPolicy, out bool)"/>
    /// and discards the <c>wasDropped</c> result to simplify benchmark loops. If you need to
    /// track drops, benchmark the underlying ring buffer directly.
    /// </remarks>
    public void Add(T item)
        => _inner.Add(item, DropPolicy.DropOldest, out _);

    /// <summary>
    /// Triggers the ring buffer's snapshot routine for measurement purposes.
    /// </summary>
    /// <remarks>
    /// This method is intended for timing and allocation profiling of snapshot logic.
    /// It does not return or expose the snapshot contents.
    /// </remarks>
    public void Snapshot()
        => _inner.Snapshot();
}
