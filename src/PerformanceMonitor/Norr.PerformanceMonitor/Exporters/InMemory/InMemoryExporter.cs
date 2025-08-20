// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Collections.Concurrent;

using Norr.PerformanceMonitor.Exporters.Core;

namespace Norr.PerformanceMonitor.Exporters.InMemory;

/// <summary>
/// A lightweight, in-memory exporter intended for tests, demos, and micro-benchmarks.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="InMemoryExporter{T}"/> couples a bounded, back‑pressure aware producer/consumer queue
/// (<see cref="ExporterQueue{T}"/>) with a simple in-memory storage (<see cref="ConcurrentQueue{T}"/>).
/// Items are enqueued via <see cref="EnqueueAsync(T, System.Threading.CancellationToken)"/> and
/// exported in batches into the internal storage by a background loop.
/// </para>
/// <para>
/// <b>Thread safety:</b> The type is thread-safe. Enqueue operations may be performed concurrently
/// from multiple threads. Snapshotting is lock‑free and does not block the background export loop.
/// </para>
/// <para>
/// <b>Use cases:</b>
/// <list type="bullet">
///   <item>End-to-end unit/integration tests that need to assert what was exported.</item>
///   <item>Local development or demos that do not require a durable sink.</item>
///   <item>Performance investigations where an in-memory endpoint avoids I/O costs.</item>
/// </list>
/// </para>
/// <para>
/// <b>Limitations:</b> This exporter keeps all exported items in memory. It is not suitable for
/// production persistence or for unbounded message volumes.
/// </para>
/// </remarks>
/// <typeparam name="T">
/// The item type to export. This is typically a telemetry envelope (e.g., activities, logs, metrics),
/// but any reference or value type is supported.
/// </typeparam>
/// <seealso cref="ExporterQueue{T}"/>
/// <seealso cref="DropPolicy"/>
public sealed class InMemoryExporter<T> : IAsyncDisposable
{
    private readonly ConcurrentQueue<T> _storage = new();
    private readonly ExporterQueue<T> _queue;

    /// <summary>
    /// Creates a new <see cref="InMemoryExporter{T}"/> with the specified queue capacity,
    /// batch size, and drop/backoff policy.
    /// </summary>
    /// <param name="capacity">
    /// The maximum number of items the bounded queue will hold before applying
    /// the <paramref name="dropPolicy"/>. Defaults to <c>4096</c>.
    /// </param>
    /// <param name="maxBatch">
    /// The upper limit for items exported per batch to the in-memory storage.
    /// Defaults to <c>256</c>.
    /// </param>
    /// <param name="dropPolicy">
    /// The policy to apply when the queue is full (e.g., backoff/retry or drop).
    /// Defaults to <see cref="DropPolicy.BackoffRetry"/>.
    /// </param>
    /// <remarks>
    /// The constructor starts the background export loop owned by the underlying
    /// <see cref="ExporterQueue{T}"/>. Call <see cref="DisposeAsync"/> to stop the loop
    /// and release its resources.
    /// </remarks>
    public InMemoryExporter(int capacity = 4096, int maxBatch = 256, DropPolicy dropPolicy = DropPolicy.BackoffRetry)
    {
        _queue = new ExporterQueue<T>(
            capacity: capacity,
            maxBatchSize: maxBatch,
            dropPolicy: dropPolicy,
            exportBatchAsync: ExportBatchAsync);
    }

    /// <summary>
    /// Attempts to enqueue an item for export.
    /// </summary>
    /// <param name="item">The item to enqueue.</param>
    /// <param name="ct">
    /// A <see cref="System.Threading.CancellationToken"/> to observe while waiting to enqueue.
    /// </param>
    /// <returns>
    /// A task that completes with <see langword="true"/> if the item was accepted into the queue;
    /// otherwise <see langword="false"/> if it could not be enqueued due to the active
    /// <see cref="DropPolicy"/>.
    /// </returns>
    /// <remarks>
    /// The exact behavior when the queue is at capacity depends on the configured <see cref="DropPolicy"/>.
    /// For example, <see cref="DropPolicy.BackoffRetry"/> may perform bounded waits before giving up,
    /// while a drop policy may immediately return <see langword="false"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// var exporter = new InMemoryExporter&lt;MyEnvelope&gt;();
    /// await exporter.EnqueueAsync(new MyEnvelope { Id = 1 });
    /// var snapshot = exporter.Snapshot();
    /// </code>
    /// </example>
    public Task<bool> EnqueueAsync(T item, CancellationToken ct = default)
        => _queue.EnqueueAsync(item, ct);

    // Called by the background ExporterQueue&lt;T&gt; loop to flush batches into the in-memory store.
    private ValueTask ExportBatchAsync(IReadOnlyList<T> batch, CancellationToken _)
    {
        for (int i = 0; i < batch.Count; i++)
        {
            _storage.Enqueue(batch[i]);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Returns a point‑in‑time, read‑only snapshot of all items that have been exported so far.
    /// </summary>
    /// <returns>
    /// A read‑only collection representing a snapshot of the current in‑memory storage.
    /// </returns>
    /// <remarks>
    /// The snapshot is an immutable array created at call time; subsequent exports will not
    /// change the returned instance.
    /// </remarks>
    public IReadOnlyCollection<T> Snapshot()
        => _storage.ToArray();

    /// <summary>
    /// Asynchronously stops the background export loop and releases associated resources.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when disposal has finished.</returns>
    /// <remarks>
    /// After disposal, subsequent calls to <see cref="EnqueueAsync(T, System.Threading.CancellationToken)"/>
    /// will fail according to the behavior of the underlying <see cref="ExporterQueue{T}"/>.
    /// </remarks>
    public async ValueTask DisposeAsync()
        => await _queue.DisposeAsync().ConfigureAwait(false);
}
