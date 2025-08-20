// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Norr.PerformanceMonitor.OpenTelemetry;

/// <summary>
/// Bridge that exposes <see cref="ActivitySource"/> and <see cref="Meter"/> with
/// lazy, thread-safe instrument creation for durations, counts, and payload sizes.
/// </summary>
public sealed class OtelPerformanceMonitorBridge
{
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;

    private readonly ConcurrentDictionary<string, Histogram<double>> _durations = new();
    private readonly ConcurrentDictionary<string, Counter<long>> _counters = new();
    private readonly ConcurrentDictionary<string, Histogram<long>> _payloads = new();

    private readonly OtelBridgeOptions _options;

    /// <summary>
    /// Gets the name of the underlying <see cref="ActivitySource"/>.
    /// </summary>
    public string ActivitySourceName => _activitySource.Name;

    /// <summary>
    /// Gets the name of the underlying <see cref="Meter"/>.
    /// </summary>
    public string MeterName => _meter.Name;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtelPerformanceMonitorBridge"/> class.
    /// </summary>
    /// <param name="options">
    /// Bridge options controlling service metadata, instrument names, and behavior flags.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    public OtelPerformanceMonitorBridge(OtelBridgeOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var serviceName = options.ServiceName ?? asm.GetName().Name ?? "Norr.PerformanceMonitor";
        var activityName = options.ActivitySourceName ?? serviceName;
        var meterName = options.MeterName ?? serviceName;

        _activitySource = new ActivitySource(activityName);
        _meter = new Meter(meterName, options.ServiceVersion ?? asm.GetName().Version?.ToString() ?? "0.0.0");
    }

    /// <summary>
    /// Starts a new performance scope for an operation, optionally attaching category, component,
    /// and custom attributes as <see cref="Activity"/> tags. Also prepares duration/counter instruments.
    /// </summary>
    /// <param name="operationName">Logical operation name (e.g., route, handler, job).</param>
    /// <param name="category">Optional high-level category (e.g., <c>http</c>, <c>db</c>).</param>
    /// <param name="component">Optional component/subsystem (e.g., <c>norr.web</c>).</param>
    /// <param name="attributes">Optional additional tags applied to the <see cref="Activity"/>.</param>
    /// <returns>
    /// An <see cref="OtelPerformanceScope"/> that records duration (seconds) and count on dispose,
    /// and stops the started <see cref="Activity"/> if tracing is enabled.
    /// </returns>
    public OtelPerformanceScope Begin(
        string operationName,
        string? category = null,
        string? component = null,
        IEnumerable<KeyValuePair<string, object?>>? attributes = null)
    {
        var tags = new ActivityTagsCollection
        {
            { ActivityTags.OperationName, operationName }
        };
        if (!string.IsNullOrWhiteSpace(category))
            tags.Add(ActivityTags.Category, category);
        if (!string.IsNullOrWhiteSpace(component))
            tags.Add(ActivityTags.Component, component);

        // Global attributes
        foreach (var kv in _options.GlobalAttributes)
            tags[kv.Key] = kv.Value;

        // Custom attributes
        if (attributes is not null)
            foreach (var kv in attributes)
                tags[kv.Key] = kv.Value;

        var activity = _options.EnableTracing
            ? _activitySource.StartActivity(operationName, ActivityKind.Internal, default(ActivityContext), tags)
            : null;

        var duration = _options.EnableMetrics
            ? _durations.GetOrAdd(_options.DurationHistogramName, n => _meter.CreateHistogram<double>(n, unit: "s", description: "Operation duration in seconds"))
            : null;

        var counter = _options.EnableMetrics
            ? _counters.GetOrAdd(_options.OperationCounterName, n => _meter.CreateCounter<long>(n, unit: "{operation}", description: "Operation count"))
            : null;

        return new OtelPerformanceScope(activity, duration, counter);
    }

    /// <summary>
    /// Records a success flag on the given <see cref="Activity"/> (or <see cref="Activity.Current"/> if not provided).
    /// </summary>
    /// <param name="activity">Target activity; if <c>null</c>, uses <see cref="Activity.Current"/>.</param>
    /// <param name="success">Value to record; defaults to <c>true</c>.</param>
    public void RecordSuccess(Activity? activity = null, bool success = true)
    {
        (activity ?? Activity.Current)?.SetTag(ActivityTags.Success, success);
    }

    /// <summary>
    /// Records the exception details on the specified <see cref="Activity"/> (or <see cref="Activity.Current"/>),
    /// sets activity status to <see cref="ActivityStatusCode.Error"/>, and adds an <c>exception</c> event.
    /// No-ops when <see cref="OtelBridgeOptions.RecordExceptions"/> is <c>false</c>.
    /// </summary>
    /// <param name="ex">The exception to record.</param>
    /// <param name="activity">Target activity; if <c>null</c>, uses <see cref="Activity.Current"/>.</param>
    public void RecordException(Exception ex, Activity? activity = null)
    {
        if (!_options.RecordExceptions)
            return;

        var a = activity ?? Activity.Current;
        if (a is null)
            return;

        a.SetStatus(ActivityStatusCode.Error, ex.Message);
        a.SetTag(ActivityTags.ExceptionType, ex.GetType().FullName);
        a.SetTag(ActivityTags.ExceptionMessage, ex.Message);
        a.SetTag(ActivityTags.ExceptionStack, ex.StackTrace);
        a.AddEvent(new ActivityEvent("exception"));
    }

    /// <summary>
    /// Gets (or lazily creates) the shared histogram for payload sizes in bytes.
    /// </summary>
    /// <returns>A <see cref="Histogram{T}"/> of type <see cref="long"/> with unit <c>By</c>.</returns>
    public Histogram<long> GetPayloadHistogram()
        => _payloads.GetOrAdd(_options.PayloadBytesHistogramName!, n => _meter.CreateHistogram<long>(n, unit: "By", description: "Payload size in bytes"));
}
