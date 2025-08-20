// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using Norr.PerformanceMonitor.Core;

namespace Norr.PerformanceMonitor.Alerting;

/// <summary> 
/// Immutable payload describing a metric that crossed a configured threshold 
/// and therefore triggered an alert. 
/// </summary> 
/// <param name="MetricName"> 
/// Fully-qualified name of the operation / metric 
/// (e.g. <c>"OrderService.PlaceOrder"</c> or <c>"HTTP GET /products"</c>). 
/// </param> 
/// <param name="Kind"> 
/// The type of measurement that triggered the alert 
/// (duration, CPU-time, allocated bytes, …) — see <see cref="MetricKind"/>. 
/// </param> 
/// <param name="Value"> 
/// The observed metric value that breached the threshold. 
/// </param> /// <param name="Threshold"> 
/// The configured limit that was exceeded. 
/// </param>
public sealed record PerfAlert(
    string MetricName,
    MetricKind Kind,
    double Value,
    double Threshold
);
