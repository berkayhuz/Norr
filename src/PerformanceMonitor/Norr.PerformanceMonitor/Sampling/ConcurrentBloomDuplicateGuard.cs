// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Runtime.CompilerServices;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Configuration;

namespace Norr.PerformanceMonitor.Sampling;

/// <summary>
/// Thread-safe, double-buffered Bloom filter for duplicate suppression within a rolling time window.
/// </summary>
/// <remarks>
/// <para>
/// Maintains two <c>long[]</c> bit-sets: <em>active</em> (current window) and <em>cold</em> (next window).
/// When the time bucket changes, the cold set is cleared and atomically swapped to become the new active set.
/// </para>
/// <para>
/// Bits are set atomically using <see cref="Interlocked.Or(ref long, long)"/> without locks,
/// except during rare window rotations where a short lock is taken.
/// </para>
/// <para>
/// No timers are used — the window is checked and rotated on each call to <see cref="ShouldEmit"/>.
/// </para>
/// <b>Thread safety:</b> Fully thread-safe; designed for high-throughput, low-latency scenarios.
/// </remarks>
public sealed class ConcurrentBloomDuplicateGuard : IDuplicateGuard, IDisposable
{
    private readonly TimeSpan _cooldown;
    private readonly int _bitCount;
    private readonly int _mask;
    private readonly int _wordCount;

    private volatile long[] _active;
    private volatile long[] _cold;

    private long _currentBucket;
    private readonly object _rotateLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrentBloomDuplicateGuard"/> class with the specified options.
    /// </summary>
    /// <param name="opt">Duplicate guard configuration, including bit count and cooldown window.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="opt"/> is <see langword="null"/>.</exception>
    public ConcurrentBloomDuplicateGuard(DuplicateGuardOptions opt)
    {
        if (opt is null)
            throw new ArgumentNullException(nameof(opt));
        _cooldown = opt.CoolDown <= TimeSpan.Zero ? TimeSpan.FromSeconds(10) : opt.CoolDown;

        _bitCount = NormalizeBitCount(opt.BitCount);
        _mask = _bitCount - 1;
        _wordCount = _bitCount >> 6; // /64

        _active = new long[_wordCount];
        _cold = new long[_wordCount];

        _currentBucket = GetBucket(DateTime.UtcNow);
    }

    /// <inheritdoc />
    public bool ShouldEmit(string name, DateTime utcNow)
    {
        var bucket = GetBucket(utcNow);
        if (bucket != Volatile.Read(ref _currentBucket))
            RotateTo(bucket);

        ulong h = Hash64(name);
        int b1 = (int)(h & (uint)_mask);
        int b2 = (int)(((h >> 32) ^ (h * 0x9E3779B185EBCA87UL)) & (uint)_mask);

        var a = _active;
        bool hit1 = TestBit(a, b1);
        bool hit2 = TestBit(a, b2);

        if (hit1 && hit2)
            return false;

        SetBit(a, b1);
        SetBit(a, b2);
        return true;
    }

    /// <summary>
    /// Releases any resources used by this instance.
    /// </summary>
    /// <remarks>
    /// This type does not hold unmanaged resources; the method exists to satisfy <see cref="IDisposable"/>.
    /// </remarks>
    public void Dispose()
    {
        // no unmanaged resources to release
    }

    // ---- Internal helpers -------------------------------------------------

    private void RotateTo(long newBucket)
    {
        lock (_rotateLock)
        {
            var cur = _currentBucket;
            if (newBucket == cur)
                return;

            Array.Clear(_cold, 0, _cold.Length);

            var oldActive = _active;
            _active = _cold;
            _cold = oldActive;

            Volatile.Write(ref _currentBucket, newBucket);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TestBit(long[] words, int bitIndex)
    {
        int wordIdx = bitIndex >> 6;
        int bitInWord = bitIndex & 63;
        long word = Volatile.Read(ref words[wordIdx]);
        long mask = 1L << bitInWord;
        return (word & mask) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetBit(long[] words, int bitIndex)
    {
        int wordIdx = bitIndex >> 6;
        int bitInWord = bitIndex & 63;
        long mask = 1L << bitInWord;
        Interlocked.Or(ref words[wordIdx], mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long GetBucket(DateTime utcNow)
        => utcNow.Ticks / _cooldown.Ticks;

    private static int NormalizeBitCount(int requested)
    {
        const int MinBits = 1 << 16;
        if (requested < MinBits)
            requested = MinBits;

        int pow2 = NextPowerOfTwo(requested);
        if ((pow2 & 63) != 0)
            pow2 = (pow2 + 63) & ~63;
        return pow2;
    }

    private static int NextPowerOfTwo(int x)
    {
        unchecked
        {
            x--;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            x++;
            return x > 0 ? x : 1 << 30;
        }
    }

    private static ulong Hash64(string s)
    {
        const ulong FNV_OFFSET = 14695981039346656037UL;
        const ulong FNV_PRIME = 1099511628211UL;

        unchecked
        {
            ulong h = FNV_OFFSET;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= (byte)s[i];
                h *= FNV_PRIME;
                h ^= h >> 32;
                h *= 0x9E3779B185EBCA87UL;
            }

            h ^= h >> 33;
            h *= 0xff51afd7ed558ccdUL;
            h ^= h >> 33;
            h *= 0xc4ceb9fe1a85ec53UL;
            h ^= h >> 33;
            return h;
        }
    }
}
