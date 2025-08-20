// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 



namespace Norr.PerformanceMonitor.Analyzers;

/// <summary>
/// Well-known diagnostic ID constants for all analyzers in
/// <see cref="Norr.PerformanceMonitor.Analyzers"/>.
/// </summary>
/// <remarks>
/// <para>
/// These IDs are used when creating <see cref="Microsoft.CodeAnalysis.DiagnosticDescriptor"/>s
/// so that diagnostics are uniquely identifiable by tools, build systems, and IDEs.
/// </para>
/// <para>
/// <b>Naming convention:</b> Each ID follows the pattern:
/// <c>NORR###X</c> where:
/// <list type="bullet">
///   <item><description><c>NORR</c> — the analyzer package prefix.</description></item>
///   <item><description><c>###</c> — a three-digit sequence number.</description></item>
///   <item><description><c>X</c> — an optional letter suffix indicating a variant.</description></item>
/// </list>
/// </para>
/// <para>
/// These constants are intended for internal reuse across multiple analyzer classes to ensure
/// IDs remain consistent and to avoid typos in multiple files.
/// </para>
/// </remarks>
internal static class DiagnosticIds
{
    /// <summary>
    /// Diagnostic ID for <c>DangerousActivityTagAnalyzer</c> —
    /// reports usage of high-cardinality or sensitive tag keys in <c>Activity.SetTag</c>.
    /// </summary>
    public const string DangerousActivityTag = "NORR001A";

    /// <summary>
    /// Diagnostic ID for an analyzer that reports high-cardinality or sensitive metric labels.
    /// </summary>
    public const string DangerousMetricLabel = "NORR001B";
}
