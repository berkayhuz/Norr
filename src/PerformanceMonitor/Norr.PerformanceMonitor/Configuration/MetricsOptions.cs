// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 



namespace Norr.PerformanceMonitor.Configuration;

/// <summary>
/// Global configuration for metric collection and export.
/// </summary>
/// <remarks>
/// <para>
/// These options control how metrics are tagged, whether additional diagnostic
/// information is included, and how temporality is applied for OpenTelemetry exporters.
/// </para>
/// <para>
/// <b>Thread safety:</b> This object is intended for configuration at application
/// startup and is not expected to change at runtime.
/// </para>
/// </remarks>
public sealed class MetricsOptions
{
    /// <summary>
    /// Gets the global set of key/value tags to attach to every metric.
    /// </summary>
    /// <value>
    /// A dictionary of tag names to values. Keys should be lowercase and
    /// follow a consistent naming convention (for example, <c>service.name</c>).
    /// Values can be strings, numbers, booleans, or <see langword="null"/>.
    /// </value>
    /// <remarks>
    /// Use this to set common attributes such as environment, region, or
    /// application version without repeating them for each metric.
    /// </remarks>
    public Dictionary<string, object?> GlobalTags { get; init; } = new();

    /// <summary>
    /// Gets a value indicating whether the managed thread ID should be included
    /// as a tag on each metric.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to include a <c>thread.id</c> tag;
    /// otherwise, <see langword="false"/>.
    /// </value>
    /// <remarks>
    /// Including thread IDs can be useful for low-level diagnostics or
    /// debugging concurrency issues, but may significantly increase the
    /// cardinality of metrics and should be used with caution in production.
    /// </remarks>
    public bool IncludeThreadId { get; init; } = false;

    /// <summary>
    /// Gets the OpenTelemetry reader/exporter temporality preference.
    /// </summary>
    /// <value>
    /// The temporality to request from metric exporters; defaults to
    /// <see cref="MetricsTemporality.Default"/>.
    /// </value>
    /// <remarks>
    /// Some exporters may not support all temporalities; in such cases, the
    /// preference may be ignored.
    /// </remarks>
    public MetricsTemporality Temporality { get; init; } = MetricsTemporality.Default;

    /// <summary>
    /// Gets the tag scrubbing and censorship policy.
    /// </summary>
    /// <value>
    /// A <see cref="ScrubbingOptions"/> instance that defines which tag values
    /// should be sanitized before export.
    /// </value>
    /// <remarks>
    /// The default configuration favors preserving safe tags while redacting
    /// potentially sensitive information such as full URLs, JWTs, or email addresses.
    /// </remarks>
    public ScrubbingOptions Scrub { get; init; } = new();
}
