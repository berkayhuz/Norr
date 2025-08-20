// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

namespace Norr.PerformanceMonitor.Core;

/// <summary>
/// Enumerates the kinds of measurements captured by the library.
/// Each <see cref="Metrics.Metric"/> instance indicates which of these it holds
/// so that exporters and dashboards can label units correctly.
/// </summary>
public enum MetricKind
{
    /// <summary>
    /// Total wall-clock time of the operation, in <b>milliseconds</b>.
    /// </summary>
    DurationMs,

    /// <summary>
    /// Managed memory allocated by the current thread during the operation,
    /// expressed in <b>bytes</b>.
    /// </summary>
    AllocBytes,

    /// <summary>
    /// CPU time consumed by the operation (user + kernel) in <b>milliseconds</b>.
    /// </summary>
    CpuMs
}
