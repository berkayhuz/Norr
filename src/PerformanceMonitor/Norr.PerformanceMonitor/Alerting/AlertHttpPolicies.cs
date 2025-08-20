// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using System.Net;

using Microsoft.Extensions.Logging;

using Norr.Diagnostics.Abstractions.Logging;

using Polly;

namespace Norr.PerformanceMonitor.Alerting;

/// <summary>
/// Defines HTTP resilience policies for alert delivery using <see cref="Polly"/>.
/// </summary>
/// <remarks>
/// <para>
/// The generated policy pipeline wraps an HTTP call with:
/// </para>
/// <list type="number">
///   <item><description>
///   A per-attempt timeout to prevent individual calls from hanging indefinitely.
///   </description></item>
///   <item><description>
///   A circuit breaker to temporarily halt calls after sustained failure.
///   </description></item>
///   <item><description>
///   A retry strategy for transient errors, using exponential backoff with jitter.
///   </description></item>
/// </list>
/// <para>
/// This design reduces load on external systems during outages and improves reliability
/// for transient network issues or rate-limiting scenarios.
/// </para>
/// </remarks>
internal static class AlertHttpPolicies
{
    /// <summary>
    /// Creates a resilience policy for HTTP alert sends that applies timeout, circuit breaker,
    /// and retry with exponential backoff and jitter.
    /// </summary>
    /// <param name="sinkName">
    /// Logical name of the alert sink (e.g., <c>"webhook"</c>, <c>"slack"</c>) used in retry logging.
    /// </param>
    /// <returns>
    /// A composed <see cref="IAsyncPolicy{HttpResponseMessage}"/> that should wrap the HTTP send operation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The policy retries on:
    /// <list type="bullet">
    ///   <item><see cref="HttpRequestException"/></item>
    ///   <item><see cref="TaskCanceledException"/> where the operation was not explicitly cancelled</item>
    ///   <item>HTTP 408 (Request Timeout)</item>
    ///   <item>HTTP 429 (Too Many Requests)</item>
    ///   <item>Any <c>5xx</c> server error</item>
    /// </list>
    /// </para>
    /// <para>
    /// For HTTP 429, if the <c>Retry-After</c> header is present and valid, it overrides the default
    /// backoff calculation. Otherwise, the retry delay uses exponential backoff capped at 30 seconds,
    /// with an additional 0–250 ms of random jitter.
    /// </para>
    /// <para>
    /// Retry attempts are logged through <see cref="AlertEventSource.SendRetry"/> with the sink name,
    /// attempt number, status code, reason, and delay in milliseconds.
    /// </para>
    /// <para>
    /// Composition order:
    /// <list type="number">
    ///   <item>Outer: Timeout (10 seconds per attempt)</item>
    ///   <item>Middle: Circuit breaker (opens after 10 consecutive handled failures, breaks for 30 seconds)</item>
    ///   <item>Inner: Wait-and-retry (max 4 retries)</item>
    /// </list>
    /// </para>
    /// </remarks>
    public static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(string sinkName, ILogger logger)
    {
        // Timeout per attempt (10 seconds)
        var timeout = Policy.TimeoutAsync<HttpResponseMessage>(10);

        // Circuit breaker: break after 10 consecutive handled failures for 30 seconds
        var cb = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
            .OrResult(r => r is not null && ((int)r.StatusCode == 408 || (int)r.StatusCode == 429 || (int)r.StatusCode >= 500))
            .CircuitBreakerAsync(handledEventsAllowedBeforeBreaking: 10, durationOfBreak: TimeSpan.FromSeconds(30));

        // Retry policy: up to 4 retries, exponential backoff + jitter, Retry-After support for 429
        var retry = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
            .OrResult(static r =>
            {
                if (r is null)
                    return false;
                var s = (int)r.StatusCode;
                return s == 408 || s == 429 || s >= 500;
            })
            .WaitAndRetryAsync(
                retryCount: 4,
                sleepDurationProvider: static (attempt, outcome, _) =>
                {
                    if (outcome.Result is HttpResponseMessage res &&
                        res.StatusCode == HttpStatusCode.TooManyRequests &&
                        TryGetRetryAfter(res, out var retryAfter) &&
                        retryAfter > TimeSpan.Zero)
                    {
                        return retryAfter;
                    }

                    var baseMs = Math.Min(30_000, (int)(200 * Math.Pow(2, attempt - 1)));
                    var jitter = Random.Shared.Next(0, 250);
                    return TimeSpan.FromMilliseconds(baseMs + jitter);
                },
                onRetryAsync: (outcome, delay, attempt, _) =>
                {
                    int code = outcome.Result?.StatusCode is HttpStatusCode sc ? (int)sc : 0;
                    string? reason = outcome.Result?.ReasonPhrase ?? outcome.Exception?.Message;

                    AlertEventSource.Log.SendRetry(sinkName, attempt, code, reason, (long)delay.TotalMilliseconds); // opsiyonel
                    logger.PM().RetryAttempt(sinkName, attempt, code, reason, (long)delay.TotalMilliseconds);

                    return Task.CompletedTask;
                });

        // Composition order: Timeout → CircuitBreaker → Retry → HTTP call
        return Policy.WrapAsync(timeout, cb, retry);
    }

    /// <summary>
    /// Attempts to extract a positive <c>Retry-After</c> delay from an HTTP response.
    /// </summary>
    /// <param name="res">The HTTP response to inspect.</param>
    /// <param name="delay">
    /// When this method returns, contains the parsed delay if present and valid; otherwise <c>default</c>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a valid positive delay was found; otherwise <see langword="false"/>.
    /// </returns>
    private static bool TryGetRetryAfter(HttpResponseMessage res, out TimeSpan delay)
    {
        delay = default;
        var ra = res.Headers.RetryAfter;
        if (ra is null)
            return false;

        if (ra.Delta is TimeSpan delta)
        {
            delay = delta;
            return delay > TimeSpan.Zero;
        }

        if (ra.Date is DateTimeOffset date && date > DateTimeOffset.UtcNow)
        {
            delay = date - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero;
        }

        return false;
    }
}
