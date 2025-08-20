// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Norr.PerformanceMonitor.Exporters.Core;

/// <summary>
/// Bounded, telemetry‑aware queue built on <see cref="Channel{T}"/> for deterministic behavior under load.
/// </summary>
/// <remarks>
/// <para>
/// The queue exposes drop behavior via <see cref="DropPolicy"/> and publishes exporter metrics:
/// <list type="bullet">
///   <item><c>norr.exporter.enqueued_total</c> (<see cref="Counter{T}"/>)</item>
///   <item><c>norr.exporter.dropped_total</c> (<see cref="Counter{T}"/>)</item>
///   <item><c>norr.exporter.queue_depth</c> (<see cref="ObservableGauge{T}"/>)</item>
/// </list>
/// </para>
/// <para>
/// <b>Thread safety:</b> Multiple writers and readers are supported. Depth accounting is performed
/// with <see cref="Interlocked"/> operations. Metrics counters are additive and safe under concurrency.
/// </para>
/// <para>
/// <b>DropOldest note:</b> Implementing true drop‑oldest with <see cref="Channel{T}"/> requires a deque.
/// This queue uses a pragmatic approach: when full, it yields briefly and retries a single write to
/// provoke reader progress; if still full, it drops the new item. This provides good behavior in practice
/// without a custom deque.
/// </para>
/// </remarks>
internal sealed class BoundedChannelQueue<T>
{
    private static readonly Meter _meter = new("Norr.PerformanceMonitor.Exporter", "1.0.0");

    private readonly Channel<T> _channel;
    private readonly DropPolicy _dropPolicy;
    private readonly int _capacity;

    private readonly Counter<long> _enqueued;
    private readonly Counter<long> _dropped;
    private readonly ObservableGauge<long> _depthGauge;

    private long _depth;

    /// <summary>
    /// Initializes a new bounded queue with the specified capacity and drop policy.
    /// </summary>
    /// <param name="capacity">Maximum number of items the queue can hold. Must be positive.</param>
    /// <param name="dropPolicy">Behavior to apply when the queue is full.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="capacity"/> is not positive.</exception>
    public BoundedChannelQueue(int capacity, DropPolicy dropPolicy)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _capacity = capacity;
        _dropPolicy = dropPolicy;

        // Bounded, multi‑producer / multi‑consumer channel. We manage full behavior ourselves.
        var opts = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait, // we'll decide the outcome in TryWriteAsync
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };
        _channel = Channel.CreateBounded<T>(opts);

        _enqueued = _meter.CreateCounter<long>("norr.exporter.enqueued_total");
        _dropped = _meter.CreateCounter<long>("norr.exporter.dropped_total");

        _depthGauge = _meter.CreateObservableGauge(
            "norr.exporter.queue_depth",
            () => new[] { new Measurement<long>(_depth) });
    }

    /// <summary>
    /// Gets the configured capacity of the queue.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Attempts to enqueue an item immediately, applying the configured <see cref="DropPolicy"/> if full.
    /// </summary>
    /// <param name="item">The item to enqueue.</param>
    /// <param name="ct">A cancellation token observed during small backoffs for retry logic.</param>
    /// <returns>
    /// <see langword="true"/> if the item was enqueued; otherwise <see langword="false"/> when dropped by policy.
    /// </returns>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><see cref="DropPolicy.DropNewest"/>: drops the new item immediately.</item>
    ///   <item><see cref="DropPolicy.DropOldest"/>: yields once to allow reader progress; if still full, drops the new item.</item>
    ///   <item><see cref="DropPolicy.BackoffRetry"/>: performs tiny exponential delays (≈2–3ms total) before giving up.</item>
    /// </list>
    /// </remarks>
    public async ValueTask<bool> TryWriteAsync(T item, CancellationToken ct)
    {
        // Fast path: space available
        if (_channel.Writer.TryWrite(item))
        {
            Interlocked.Increment(ref _depth);
            _enqueued.Add(1);
            return true;
        }

        // Full: apply policy
        switch (_dropPolicy)
        {
            case DropPolicy.DropNewest:
                _dropped.Add(1);
                return false;

            case DropPolicy.DropOldest:
                // See remarks: nudge reader, then retry once.
                await Task.Yield();
                    for (int i = 0; i < 4; i++)
                        {
                            if (_channel.Writer.TryWrite(item))
                                {
                        Interlocked.Increment(ref _depth);
                        _enqueued.Add(1);
                                    return true;
                                }
                            // 0 süreli gecikme: farklı iş parçalarında ilerleme şansı
                    await Task.Delay(0, ct).ConfigureAwait(false);
                        }
                _dropped.Add(1);
                return false;

            case DropPolicy.BackoffRetry:
                // Small bounded backoff loop
                var sw = Stopwatch.StartNew();
                var delay = 0;
                while (sw.ElapsedMilliseconds < 3 && !ct.IsCancellationRequested)
                {
                    delay++;
                    await Task.Delay(TimeSpan.FromTicks(50 * delay), ct).ConfigureAwait(false);
                    if (_channel.Writer.TryWrite(item))
                    {
                        Interlocked.Increment(ref _depth);
                        _enqueued.Add(1);
                        return true;
                    }
                }
                _dropped.Add(1);
                return false;

            default:
                _dropped.Add(1);
                return false;
        }
    }

    /// <summary>
    /// Enqueues an item, asynchronously waiting for space if necessary.
    /// </summary>
    /// <param name="item">The item to enqueue.</param>
    /// <param name="ct">A cancellation token to cancel the wait.</param>
    /// <returns>A task that completes when the item is enqueued.</returns>
    /// <remarks>
    /// On completion, queue depth and the <c>enqueued_total</c> counter are incremented.
    /// </remarks>
    public async ValueTask WriteAsync(T item, CancellationToken ct)
    {
        await _channel.Writer.WriteAsync(item, ct).ConfigureAwait(false);
        Interlocked.Increment(ref _depth);
        _enqueued.Add(1);
    }

    /// <summary>
    /// Reads all items from the queue as an asynchronous stream until completion.
    /// </summary>
    /// <param name="ct">A cancellation token to end the read early.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> that yields queued items.</returns>
    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken ct) => ReadAllCore(ct);

    // Internal iterator to ensure [EnumeratorCancellation] is applied correctly.
    private async IAsyncEnumerable<T> ReadAllCore([EnumeratorCancellation] CancellationToken ct)
    {
        while (await _channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            while (_channel.Reader.TryRead(out var item))
            {
                Interlocked.Decrement(ref _depth);
                yield return item;
            }
        }
    }

    /// <summary>
    /// Completes the writer side of the queue, signaling no further items will be enqueued.
    /// </summary>
    public void Complete() => _channel.Writer.TryComplete();
}
