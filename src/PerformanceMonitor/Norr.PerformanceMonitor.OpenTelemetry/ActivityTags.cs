// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

namespace Norr.PerformanceMonitor.OpenTelemetry;

/// <summary>
/// Well-known OpenTelemetry tag keys used by Norr Performance Monitor for Activities and Metrics.
/// Use these constants to ensure consistent semantic naming across traces and measurements.
/// </summary>
public static class ActivityTags
{
    /// <summary>
    /// Operation name associated with the Activity or measurement
    /// (e.g., HTTP route, handler, job, or logical operation).
    /// </summary>
    public const string OperationName = "norr.operation.name";

    /// <summary>
    /// High-level category of the operation (e.g., <c>http</c>, <c>db</c>, <c>messaging</c>, <c>cache</c>).
    /// </summary>
    public const string Category = "norr.category";

    /// <summary>
    /// Component or subsystem producing the telemetry (e.g., <c>norr.web</c>, <c>norr.worker</c>, <c>norr.render</c>).
    /// </summary>
    public const string Component = "norr.component";

    /// <summary>
    /// Success flag for the operation. Expected values: <c>true</c> or <c>false</c>.
    /// </summary>
    public const string Success = "norr.success";

    /// <summary>
    /// Exception type name recorded when an error occurs (e.g., <c>System.InvalidOperationException</c>).
    /// </summary>
    public const string ExceptionType = "norr.exception.type";

    /// <summary>
    /// Exception message recorded when an error occurs.
    /// </summary>
    public const string ExceptionMessage = "norr.exception.message";

    /// <summary>
    /// Exception stack trace recorded when an error occurs.
    /// </summary>
    public const string ExceptionStack = "norr.exception.stack";

    /// <summary>
    /// Size of the payload in bytes, when applicable (e.g., request or response length).
    /// </summary>
    public const string PayloadBytes = "norr.payload.bytes";

    /// <summary>
    /// Creates an enumerable of tag key/value pairs, convenient for passing into Activity or metric recording APIs.
    /// </summary>
    /// <param name="pairs">
    /// Arbitrary list of tag tuples where <c>Key</c> is the tag name and <c>Value</c> is the associated value.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> of <see cref="KeyValuePair{TKey, TValue}"/> representing the provided tags.
    /// </returns>
    /// <example>
    /// <code>
    /// using var activity = activitySource.StartActivity("ProcessOrder");
    /// if (activity is not null)
    /// {
    ///     foreach (var tag in ActivityTags.From(
    ///         (ActivityTags.OperationName, "ProcessOrder"),
    ///         (ActivityTags.Component, "norr.web"),
    ///         (ActivityTags.Success, true)))
    ///     {
    ///         activity.SetTag(tag.Key, tag.Value);
    ///     }
    /// }
    /// </code>
    /// </example>
    public static IEnumerable<KeyValuePair<string, object?>> From(params (string Key, object? Value)[] pairs)
    {
        foreach (var (k, v) in pairs)
        {
            yield return new KeyValuePair<string, object?>(k, v);
        }
    }
}
