// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 



namespace Norr.PerformanceMonitor.Configuration;

/// <summary>
/// Specifies the preferred aggregation temporality for OpenTelemetry metric exporters.
/// </summary>
/// <remarks>
/// <para>
/// In OpenTelemetry, <em>temporality</em> describes how metric values are aggregated
/// over time when exported:
/// </para>
/// <list type="bullet">
///   <item>
///     <term>Cumulative</term>
///     <description>Each export reports the total value since the start of the process or instrument.</description>
///   </item>
///   <item>
///     <term>Delta</term>
///     <description>Each export reports only the change since the last collection interval.</description>
///   </item>
/// </list>
/// <para>
/// Not all exporters support all temporalities. If an exporter does not support the
/// selected temporality, it may fall back to a default or ignore the preference.
/// </para>
/// </remarks>
public enum MetricsTemporality
{
    /// <summary>
    /// Use the exporter’s default temporality.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Prefer <b>cumulative</b> temporality: each exported value represents
    /// the total recorded since the start of the process or instrument.
    /// </summary>
    Cumulative = 1,

    /// <summary>
    /// Prefer <b>delta</b> temporality: each exported value represents only
    /// the change since the previous collection/export cycle.
    /// </summary>
    Delta = 2
}
