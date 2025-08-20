// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 



namespace Norr.PerformanceMonitor.Configuration;

/// <summary>
/// Tag key/value scrubbing policy to reduce cardinality and protect sensitive information.
/// </summary>
/// <remarks>
/// <para>
/// Scrubbing is applied before metrics are exported to sanitize high-risk keys,
/// mask sensitive values, normalize dynamic path segments, and enforce maximum
/// value lengths. This helps prevent sensitive data leakage and keeps metric
/// cardinality under control.
/// </para>
/// <para>
/// The default configuration is <see cref="Default"/> which preserves safe keys
/// while applying standard redaction and normalization rules.
/// </para>
/// </remarks>
public sealed class ScrubbingOptions
{
    /// <summary>
    /// Gets a value indicating whether the scrubbing mechanism is enabled.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to apply scrubbing to tags; otherwise, <see langword="false"/>.
    /// </value>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether known high-risk keys should be dropped entirely.
    /// </summary>
    /// <remarks>
    /// Typical high-risk keys contain full URLs, raw paths, or other high-cardinality values.
    /// </remarks>
    public bool DropBannedKeys { get; init; } = true;

    /// <summary>
    /// Gets the set of banned keys that should be dropped when <see cref="DropBannedKeys"/> is <see langword="true"/>.
    /// </summary>
    /// <value>
    /// A case-insensitive set of tag keys considered unsafe or high-cardinality.
    /// </value>
    /// <remarks>
    /// Defaults include:
    /// <list type="bullet">
    ///   <item><c>http.url</c></item>
    ///   <item><c>http.target</c></item>
    ///   <item><c>request.path</c></item>
    ///   <item><c>url</c></item>
    ///   <item><c>norr.http.request_path</c></item>
    /// </list>
    /// </remarks>
    public ISet<string> BannedKeys
    {
        get; init;
    }
        = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "http.url", "http.target", "request.path", "url", "norr.http.request_path"
        };

    /// <summary>
    /// Gets a value indicating whether values containing patterns similar to PII should be masked.
    /// </summary>
    /// <remarks>
    /// Examples include email addresses, JWT tokens, and UUIDs. Masking replaces them with
    /// <see cref="Mask"/>.
    /// </remarks>
    public bool MaskSensitiveValues { get; init; } = true;

    /// <summary>
    /// Gets the mask string used to replace sensitive values when <see cref="MaskSensitiveValues"/> is <see langword="true"/>.
    /// </summary>
    /// <value>
    /// Defaults to <c>[redacted]</c>.
    /// </value>
    public string Mask { get; init; } = "[redacted]";

    /// <summary>
    /// Gets a value indicating whether numeric, GUID-like, or email-like path segments
    /// should be normalized.
    /// </summary>
    /// <remarks>
    /// Normalization replaces dynamic segments (e.g., IDs) with placeholders to reduce
    /// metric cardinality.
    /// </remarks>
    public bool NormalizePaths { get; init; } = true;

    /// <summary>
    /// Gets the maximum allowed string value length.
    /// </summary>
    /// <remarks>
    /// Values longer than this limit are truncated.
    /// </remarks>
    public int MaxValueLength { get; init; } = 256;

    /// <summary>
    /// Gets a reusable instance of <see cref="ScrubbingOptions"/> with safe defaults applied.
    /// </summary>
    /// <remarks>
    /// This is equivalent to calling <c>new ScrubbingOptions()</c> with no modifications.
    /// </remarks>
    public static ScrubbingOptions Default => new();
}
