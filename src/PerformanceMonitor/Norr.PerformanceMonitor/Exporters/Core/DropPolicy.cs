// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


namespace Norr.PerformanceMonitor.Exporters.Core;

/// <summary>
/// Defines the behavior to apply when a bounded exporter queue is full and a new item
/// cannot be enqueued immediately.
/// </summary>
/// <remarks>
/// The selected policy determines whether the exporter drops items, replaces older ones,
/// or temporarily backs off and retries.  
/// This allows tuning for different telemetry loss-tolerance and latency requirements.
/// </remarks>
public enum DropPolicy
{
    /// <summary>
    /// If enqueue fails because the queue is full, drop the newly attempted item
    /// and increment a drop counter. Older items already in the queue remain unaffected.
    /// </summary>
    DropNewest,

    /// <summary>
    /// If the queue is full, remove the oldest item in the queue to make space
    /// for the new one, ensuring that the most recent data is preserved.
    /// </summary>
    DropOldest,

    /// <summary>
    /// If the queue is full, briefly delay the calling thread (e.g., via spin or yield)
    /// and retry enqueueing until space becomes available or the attempt times out.
    /// </summary>
    BackoffRetry
}
