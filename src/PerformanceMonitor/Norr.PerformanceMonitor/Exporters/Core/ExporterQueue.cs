// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Norr.PerformanceMonitor.Exporters.Core;

/// <summary>
/// Background exporter loop that drains a bounded queue and delivers items to an
/// application-provided batch exporter.
/// </summary>
/// <remarks>
/// <para>
/// This component coordinates three responsibilities:
/// </para>
/// <list type="bullet">
///   <item><description>Back-pressure: enqueues flow through <see cref="BoundedChannelQueue{T}"/> with a configurable <see cref="DropPolicy"/>.</description></item>
///   <item><description>Batching: items are accumulated up to<c> maxBatchSize</c> and flushed via the provided delegate.</description></item>
///   <item><description>Telemetry: emits<c> norr.exporter.batch_duration_ms</c> as a<see cref = "Histogram{T}" /> per batch execution.</description></item>
/// </list>
/// <para>
/// <b>Thread safety:</b> Multiple producers may call <see cref="EnqueueAsync(T, System.Threading.CancellationToken)"/>
/// concurrently. A single background consumer loop performs batch export. Disposal cancels the loop and drains
/// any remaining buffered items before returning.
/// </para>
/// </remarks>
internal sealed class ExporterQueue<T> : IAsyncDisposable
{
    private static readonly Meter _meter = new("Norr.PerformanceMonitor.Exporter", "1.0.0");
    private readonly Histogram<double> _batchDurationMs =
        _meter.CreateHistogram<double>("norr.exporter.batch_duration_ms");

    private readonly BoundedChannelQueue<T> _queue;
    private readonly Func<IReadOnlyList<T>, CancellationToken, ValueTask> _exportBatchAsync;
    private readonly int _maxBatchSize;
    private readonly CancellationTokenSource _cts;
    private readonly Task _loop;

    /// <summary>
    /// Initializes a new exporter queue.
    /// </summary>
    /// <param name="capacity">Maximum number of items that can be queued before applying <paramref name="dropPolicy"/>.</param>
    /// <param name="maxBatchSize">Maximum number of items to flush per batch. Must be positive.</param>
    /// <param name="dropPolicy">Behavior to apply when the queue is full.</param>
    /// <param name="exportBatchAsync">
    /// Delegate invoked by the background loop to export a batch. Must not be <see langword="null"/>.
    /// The delegate should be resilient to transient failures and honor <see cref="CancellationToken"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxBatchSize"/> is not positive.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exportBatchAsync"/> is <see langword="null"/>.</exception>
    public ExporterQueue(
        int capacity,
        int maxBatchSize,
        DropPolicy dropPolicy,
        Func<IReadOnlyList<T>, CancellationToken, ValueTask> exportBatchAsync)
    {
        if (maxBatchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBatchSize));

        _queue = new BoundedChannelQueue<T>(capacity, dropPolicy);
        _maxBatchSize = maxBatchSize;
        _exportBatchAsync = exportBatchAsync ?? throw new ArgumentNullException(nameof(exportBatchAsync));
        _cts = new CancellationTokenSource();
        _loop = Task.Run(LoopAsync);
    }

    /// <summary>
    /// Attempts to enqueue a single item for export.
    /// </summary>
    /// <param name="item">The item to enqueue.</param>
    /// <param name="ct">A cancellation token observed during short backoffs (depending on <see cref="DropPolicy"/>).</param>
    /// <returns>
    /// A task that completes with <see langword="true"/> if the item was accepted into the queue; otherwise
    /// <see langword="false"/> if it was dropped by policy.
    /// </returns>
    public async Task<bool> EnqueueAsync(T item, CancellationToken ct = default)
        => await _queue.TryWriteAsync(item, ct).ConfigureAwait(false);

    // Background loop: drains the channel, accumulates up to _maxBatchSize, and flushes.
    private async Task LoopAsync()
    {
        var ct = _cts.Token;
        var buffer = new List<T>(_maxBatchSize);

        try
        {
            await foreach (var item in _queue.ReadAllAsync(ct).ConfigureAwait(false))
            {
                buffer.Add(item);
                if (buffer.Count >= _maxBatchSize)
                    await FlushAsync(buffer, CancellationToken.None).ConfigureAwait(false);
            }

            // Channel completed: flush any remaining items.
            if (buffer.Count > 0)
                await FlushAsync(buffer, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            // Let the debugger/host see the failure; exporters should log internally as well.
            Debug.Fail("Exporter loop faulted: " + ex);
        }
    }

    // Flushes the current buffer via the export delegate and records batch duration.
    private async ValueTask FlushAsync(List<T> buffer, CancellationToken ct)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            await _exportBatchAsync(buffer, ct).ConfigureAwait(false);
        }
        finally
        {
            _batchDurationMs.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            buffer.Clear();
        }
    }

    /// <summary>
    /// Asynchronously stops the background loop and releases resources.
    /// </summary>
    /// <remarks>
    /// Completion signals no further enqueues, cancels the consumer loop, and waits for it to finish.
    /// Remaining buffered items are flushed before the loop exits.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        _queue.Complete();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch
        {
            // Swallow loop exceptions during disposal; faults are already surfaced via Debug.Fail.
        }
    }
}
