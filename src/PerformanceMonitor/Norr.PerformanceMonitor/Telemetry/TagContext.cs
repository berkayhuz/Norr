// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Norr.PerformanceMonitor.Telemetry;

/// <summary>
/// Ambient tag context backed by <see cref="AsyncLocal{T}"/> that aims for zero GC pressure by using
/// <see cref="ArrayPool{T}"/> to store tag frames.
/// </summary>
/// <remarks>
/// <para>
/// Each call to <see cref="Begin(string, object?)"/> or
/// <see cref="Begin(System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{System.String,System.Object}})"/>
/// pushes a new frame that holds a rented array of key/value pairs and chains it to the previous frame.
/// The most inner frame wins when resolving duplicate keys.
/// See <see cref="CopyTo(ref TagList)"/> for how tags are materialized into a <see cref="TagList"/>.
/// </para>
/// <para><b>Thread safety:</b> The type is static and thread-safe. Frames are scoped to the current async-flow via <see cref="AsyncLocal{T}"/>.</para>
/// </remarks>
public static class TagContext
{
    private static readonly AsyncLocal<Frame?> _current = new();

    /// <summary>
    /// Gets a snapshot view of the currently active ambient tags.
    /// </summary>
    /// <remarks>
    /// The view is materialized on demand and honors the inner-wins rule. Enumeration is allocation-friendly.
    /// </remarks>
    public static IReadOnlyDictionary<string, object?> Current
        => new SnapshotDictionary(_current.Value);

    /// <summary>
    /// Begins a tag frame with the specified <paramref name="tags"/> sequence.
    /// When disposed, the previous ambient frame is restored and the rented array is returned to the pool.
    /// </summary>
    /// <param name="tags">A sequence of key/value pairs to add to the ambient context.</param>
    /// <returns>An <see cref="IDisposable"/> that pops the frame when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tags"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// If <paramref name="tags"/> is a collection, the method pre-sizes the pooled buffer and copies items.
    /// Otherwise, the method grows a pooled buffer as needed. The user-provided array (if any) is never returned to the pool.
    /// </remarks>
    public static IDisposable Begin(IEnumerable<KeyValuePair<string, object?>> tags)
    {
        if (tags is null)
        {
            throw new ArgumentNullException(nameof(tags));
        }

        if (tags is ICollection<KeyValuePair<string, object?>> coll)
        {
            var arr = ArrayPool<KeyValuePair<string, object?>>.Shared.Rent(coll.Count);
            int i = 0;
            foreach (var kv in coll)
            {
                arr[i++] = kv;
            }

            return PushFrame(arr, i);
        }

        var builder = new KvBuilder(initialCapacity: 8);
        try
        {
            foreach (var kv in tags)
                builder.Add(kv);

            return PushFrame(builder.Items!, builder.Count);
        }
        catch
        {
            builder.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Begins a tag frame with the specified <paramref name="tags"/> array (or params list).
    /// </summary>
    /// <param name="tags">Key/value pairs to add to the ambient context.</param>
    /// <returns>An <see cref="IDisposable"/> that pops the frame when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tags"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// This overload rents a new buffer and copies the provided tags to avoid returning user memory to the pool.
    /// </remarks>
    public static IDisposable Begin(params KeyValuePair<string, object?>[] tags)
    {
        if (tags is null)
            throw new ArgumentNullException(nameof(tags));

        var len = tags.Length;
        if (len == 0)
            return PushFrame(Array.Empty<KeyValuePair<string, object?>>(), 0);

        var arr = ArrayPool<KeyValuePair<string, object?>>.Shared.Rent(len);
        Array.Copy(tags, arr, len);
        return PushFrame(arr, len);
    }

    /// <summary>
    /// Begins a tag frame for a single key/value pair (hot path).
    /// </summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    /// <returns>An <see cref="IDisposable"/> that pops the frame when disposed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IDisposable Begin(string key, object? value)
    {
        var arr = ArrayPool<KeyValuePair<string, object?>>.Shared.Rent(1);
        arr[0] = new KeyValuePair<string, object?>(key, value);
        return PushFrame(arr, 1);
    }

    /// <summary>
    /// Begins a tag frame from a <see cref="ReadOnlySpan{T}"/> of key/value pairs (allocation-friendly path).
    /// </summary>
    /// <param name="tags">A span of key/value pairs.</param>
    /// <returns>An <see cref="IDisposable"/> that pops the frame when disposed.</returns>
    public static IDisposable Begin(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var len = tags.Length;
        if (len == 0)
            return PushFrame(Array.Empty<KeyValuePair<string, object?>>(), 0);

        var arr = ArrayPool<KeyValuePair<string, object?>>.Shared.Rent(len);
        tags.CopyTo(arr);
        return PushFrame(arr, len);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IDisposable PushFrame(KeyValuePair<string, object?>[] items, int count)
    {
        var prev = _current.Value;
        var frame = new Frame(prev, items, count);
        _current.Value = frame;
        return new Popper(frame, prev);
    }

    /// <summary>
    /// Copies the ambient tags into the provided <see cref="TagList"/> instance, honoring the inner-wins rule.
    /// </summary>
    /// <param name="tl">The destination <see cref="TagList"/>.</param>
    public static void CopyTo(ref TagList tl)
    {
        var frame = _current.Value;
        if (frame is null)
            return;

        PooledKeySet seen = default;
        try
        {
            seen = new PooledKeySet(initialCapacity: 8);

            for (var f = frame; f is not null; f = f.Parent)
            {
                var items = f.Items;
                var cnt = f.Count;

                for (int i = 0; i < cnt; i++)
                {
                    ref readonly var kv = ref items[i];
                    var k = kv.Key;
                    if (k is null)
                        continue;

                    if (!seen.Add(k))
                        continue; // already seen in an inner frame → inner wins

                    tl.Add(k, kv.Value);
                }
            }
        }
        finally
        {
            seen.Dispose();
        }
    }

    // ---------------- internals ----------------

    private sealed class Frame
    {
        public readonly Frame? Parent;
        public readonly KeyValuePair<string, object?>[] Items;
        public readonly int Count;

        public Frame(Frame? parent, KeyValuePair<string, object?>[] items, int count)
        {
            Parent = parent;
            Items = items;
            Count = count;
        }
    }

    private sealed class Popper : IDisposable
    {
        private Frame? _frame;
        private readonly Frame? _prev;

        public Popper(Frame frame, Frame? prev)
        {
            _frame = frame;
            _prev = prev;
        }

        public void Dispose()
        {
            var f = _frame;
            if (f is null)
                return;
            _frame = null;

            if (ReferenceEquals(_current.Value, f))
                _current.Value = _prev;

            // Clear and return the rented buffer
            if (f.Count > 0)
            {
                Array.Clear(f.Items, 0, f.Count);
            }

            if (f.Items.Length > 0) // prevent returning Array.Empty<T>()
            {
                ArrayPool<KeyValuePair<string, object?>>.Shared.Return(f.Items, clearArray: false);
            }
        }
    }

    /// <summary>
    /// Small pooled set for duplicate-key detection (linear scan). Typical N is 2–8, so this
    /// avoids <see cref="HashSet{T}"/> allocations.
    /// </summary>
    private struct PooledKeySet : IDisposable
    {
        private string?[]? _arr;
        private int _count;

        public PooledKeySet(int initialCapacity)
        {
            _arr = ArrayPool<string?>.Shared.Rent(initialCapacity);
            _count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(string key)
        {
            var a = _arr!;
            var n = _count;

            for (int i = 0; i < n; i++)
            {
                if (string.Equals(a[i], key, StringComparison.Ordinal))
                    return false;
            }

            if (n == a.Length)
            {
                var newArr = ArrayPool<string?>.Shared.Rent(a.Length * 2);
                Array.Copy(a, newArr, a.Length);
                ArrayPool<string?>.Shared.Return(a, clearArray: false);
                _arr = a = newArr;
            }

            a[n] = key;
            _count = n + 1;
            return true;
        }

        public void Dispose()
        {
            var a = _arr;
            if (a is null)
                return;

            Array.Clear(a, 0, _count);
            ArrayPool<string?>.Shared.Return(a, clearArray: false);
            _arr = null;
            _count = 0;
        }
    }

    /// <summary>
    /// Allocation-friendly snapshot view implementing <see cref="IReadOnlyDictionary{TKey,TValue}"/>.
    /// Keys/Values/Count/TryGetValue/ContainsKey are supported; enumeration re-materializes
    /// the view via <see cref="CopyTo(ref TagList)"/>.
    /// </summary>
    private sealed class SnapshotDictionary : IReadOnlyDictionary<string, object?>
    {
        private readonly Frame? _root;
        public SnapshotDictionary(Frame? root) => _root = root;

        public IEnumerable<string> Keys
        {
            get
            {
                using var e = GetEnumerator();
                while (e.MoveNext())
                    yield return e.Current.Key;
            }
        }

        public IEnumerable<object?> Values
        {
            get
            {
                using var e = GetEnumerator();
                while (e.MoveNext())
                    yield return e.Current.Value;
            }
        }

        public int Count
        {
            get
            {
                int c = 0;
                using var e = GetEnumerator();
                while (e.MoveNext())
                    c++;
                return c;
            }
        }

        public bool ContainsKey(string key) => TryGetValue(key, out _);

        public bool TryGetValue(string key, out object? value)
        {
            // inner-wins — return the first match encountered while walking inward to outward
            for (var f = _root; f is not null; f = f.Parent)
            {
                var items = f.Items;
                var cnt = f.Count;
                for (int i = 0; i < cnt; i++)
                {
                    ref readonly var kv = ref items[i];
                    if (kv.Key == key)
                    {
                        value = kv.Value;
                        return true;
                    }
                }
            }
            value = null;
            return false;
        }

        public object? this[string key]
            => TryGetValue(key, out var v) ? v : throw new KeyNotFoundException(key);

        /// <summary>
        /// Enumerates the snapshot as key/value pairs.
        /// </summary>
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            var tl = new TagList();
            TagContext.CopyTo(ref tl);
            return tl.GetEnumerator();  // IEnumerator<KeyValuePair<string, object?>>
        }

        IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator()
            => GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    /// <summary>
    /// Growable (key,value) builder backed by <see cref="ArrayPool{T}"/>; zero GC on steady state.
    /// </summary>
    private struct KvBuilder : IDisposable
    {
        public KeyValuePair<string, object?>[]? Items;
        public int Count;

        public KvBuilder(int initialCapacity)
        {
            Items = ArrayPool<KeyValuePair<string, object?>>.Shared.Rent(initialCapacity);
            Count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in KeyValuePair<string, object?> kv)
        {
            var arr = Items!;
            var n = Count;
            if ((uint)n >= (uint)arr.Length)
            {
                var newArr = ArrayPool<KeyValuePair<string, object?>>.Shared.Rent(arr.Length * 2);
                Array.Copy(arr, newArr, arr.Length);
                ArrayPool<KeyValuePair<string, object?>>.Shared.Return(arr, clearArray: false);
                Items = arr = newArr;
            }
            arr[n] = kv;
            Count = n + 1;
        }

        public void Dispose()
        {
            var arr = Items;
            if (arr is null)
                return;
            Array.Clear(arr, 0, Count);
            ArrayPool<KeyValuePair<string, object?>>.Shared.Return(arr, clearArray: false);
            Items = null;
            Count = 0;
        }
    }
}
