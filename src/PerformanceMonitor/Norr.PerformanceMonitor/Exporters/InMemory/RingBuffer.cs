// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Collections.Immutable;

using Norr.PerformanceMonitor.Exporters.Core;

namespace Norr.PerformanceMonitor.Exporters.InMemory;

/// <summary>
/// Thread-safe, low-GC-pressure ring buffer with a single lock for simplicity and speed.
/// </summary>
/// <remarks>
/// <para>
/// The buffer stores up to <see cref="Capacity"/> items and supports two backpressure strategies
/// via <see cref="Add(T, DropPolicy, out bool)"/>:
/// <list type="bullet">
///   <item>
///     <description>
///     <see cref="DropPolicy.DropOldest"/>: When full, overwrite the oldest item to favor recency.
///     </description>
///   </item>
///   <item>
///     <description>
///     <see cref="DropPolicy.DropNewest"/>: When full, reject the new item to preserve backlog.
///     </description>
///   </item>
/// </list>
/// </para>
/// <para>
/// The implementation uses a single object lock guarding all state (<c>_buffer</c>, <c>_next</c>,
/// and <c>_count</c>). This keeps the design straightforward and efficient for typical producer/consumer
/// usage with moderate contention. If you require lock-free semantics or very high contention
/// scalability, consider a specialized concurrent structure.
/// </para>
/// <para>
/// <b>Allocation behavior:</b> The buffer allocates a single backing array of size <see cref="Capacity"/>
/// at construction and does not grow. <see cref="Snapshot"/> allocates a temporary array sized to the
/// current <see cref="Count"/> and returns an <see cref="ImmutableArray{T}"/> copy for safety.
/// </para>
/// <para>
/// <b>Ordering:</b> Items are logically ordered from oldest to newest. <see cref="Snapshot"/> returns
/// the items in that order (oldest first, newest last).
/// </para>
/// </remarks>
/// <typeparam name="T">
/// Element type stored in the buffer. With nullable reference types enabled, <c>T</c> may be a nullable
/// reference if the usage permits.
/// </typeparam>
/// <threadsafety>
/// All public members are thread-safe. A single lock serializes mutations and snapshots. The design
/// aims to minimize GC pressure by reusing the backing array; however, snapshots necessarily allocate
/// a new array proportional to <see cref="Count"/>.
/// </threadsafety>
/// <example>
/// Basic usage with different drop policies:
/// <code language="csharp">
/// var rb = new RingBuffer&lt;int&gt;(capacity: 3);
///
/// // Fill
/// rb.Add(1, DropPolicy.DropOldest, out _);
/// rb.Add(2, DropPolicy.DropOldest, out _);
/// rb.Add(3, DropPolicy.DropOldest, out _);
///
/// // Overwrite oldest (1) with 4
/// rb.Add(4, DropPolicy.DropOldest, out var evicted); // evicted == true
///
/// var items = rb.Snapshot(); // [2, 3, 4]
///
/// // Try to add with DropNewest: will be rejected when full
/// var accepted = rb.Add(5, DropPolicy.DropNewest, out _); // accepted == false
/// </code>
/// </example>
/// <seealso cref="DropPolicy"/>
internal sealed class RingBuffer<T>
{
    private readonly T?[] _buffer;
    private int _next;      // next write index
    private int _count;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the maximum number of elements the ring buffer can hold.
    /// </summary>
    /// <value>A positive integer set at construction time.</value>
    public int Capacity
    {
        get;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RingBuffer{T}"/> class.
    /// </summary>
    /// <param name="capacity">Maximum number of items the buffer can hold. Must be &gt; 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="capacity"/> is not a positive integer.
    /// </exception>
    public RingBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        Capacity = capacity;
        _buffer = new T[capacity];
        _next = 0;
        _count = 0;
    }

    /// <summary>
    /// Gets the current number of items stored in the buffer.
    /// </summary>
    /// <remarks>
    /// The value is retrieved under lock and therefore reflects a consistent snapshot at the
    /// time of the call.
    /// </remarks>
    public int Count
    {
        get
        {
            lock (_lock)
                return _count;
        }
    }

    /// <summary>
    /// Attempts to add an item to the buffer, applying the specified dropout policy when full.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <param name="policy">
    /// Backpressure behavior when the buffer is at <see cref="Capacity"/>:
    /// <see cref="DropPolicy.DropOldest"/> overwrites the oldest item and succeeds;
    /// <see cref="DropPolicy.DropNewest"/> rejects the new item and fails.
    /// </param>
    /// <param name="evictedOldest">
    /// Set to <see langword="true"/> if an oldest item was evicted (only possible when
    /// <paramref name="policy"/> is <see cref="DropPolicy.DropOldest"/> and the buffer was full);
    /// otherwise <see langword="false"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the item was accepted into the buffer; otherwise
    /// <see langword="false"/> (only when <paramref name="policy"/> is
    /// <see cref="DropPolicy.DropNewest"/> and the buffer was full).
    /// </returns>
    /// <remarks>
    /// The operation is O(1). When overwriting, the oldest item is replaced at the logical head,
    /// and the ring advances the write index.
    /// </remarks>
    public bool Add(T item, DropPolicy policy, out bool evictedOldest)
    {
        lock (_lock)
        {
            if (_count < Capacity)
            {
                _buffer[_next] = item;
                _next = (_next + 1) % Capacity;
                _count++;
                evictedOldest = false;
                return true;
            }

            if (policy == DropPolicy.DropOldest)
            {
                // Overwrite the oldest item
                _buffer[_next] = item;
                _next = (_next + 1) % Capacity;
                // _count is already Capacity
                evictedOldest = true;
                return true;
            }

            // DropNewest: reject the item
            evictedOldest = false;
            return false;
        }
    }

    /// <summary>
    /// Produces an immutable snapshot of the buffer contents in chronological order.
    /// </summary>
    /// <returns>
    /// An <see cref="ImmutableArray{T}"/> containing the items from oldest to newest.
    /// Returns <see cref="ImmutableArray{T}.Empty"/> when the buffer is empty.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The snapshot is taken under lock to ensure a consistent view. The method allocates an
    /// intermediate array sized to <see cref="Count"/>, then converts it to
    /// <see cref="ImmutableArray{T}"/> to prevent external mutation.
    /// </para>
    /// <para>
    /// Complexity is O(n) with respect to <see cref="Count"/>.
    /// </para>
    /// </remarks>
    public ImmutableArray<T> Snapshot()
    {
        lock (_lock)
        {
            if (_count == 0)
                return ImmutableArray<T>.Empty;

            var result = new T[_count];
            var start = (_next - _count) % Capacity;
            if (start < 0)
                start += Capacity;

            for (int i = 0; i < _count; i++)
            {
                var idx = (start + i) % Capacity;
                result[i] = _buffer[idx]!;
            }

            return result.ToImmutableArray();
        }
    }

    /// <summary>
    /// Clears the buffer, removing all items and resetting indices.
    /// </summary>
    /// <remarks>
    /// The backing array is zeroed to release references for GC, and both the write index
    /// and count are reset to zero.
    /// </remarks>
    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _count = 0;
            _next = 0;
        }
    }
}
