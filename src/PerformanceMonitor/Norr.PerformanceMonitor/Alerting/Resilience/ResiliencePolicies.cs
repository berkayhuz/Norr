// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 


using System.Net;

using Microsoft.Extensions.Logging;

using Norr.Diagnostics.Abstractions.Logging;

namespace Norr.PerformanceMonitor.Alerting.Resilience;

/// <summary>
/// Provides resilience strategies for HTTP alert delivery, including retry with exponential backoff,
/// timeout handling, transient failure detection, and circuit breaking.
/// </summary>
/// <remarks>
/// This class is intended for internal use by Norr Performance Monitor alert sinks
/// to improve reliability of webhook and external service calls.
/// </remarks>
internal static class ResiliencePolicies
{
    private static readonly TimeSpan _openToHalfOpen = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Sends an HTTP request with resilience features such as retries, per-attempt timeouts,
    /// transient error detection, exponential backoff with jitter, and a simple circuit breaker.
    /// </summary>
    /// <param name="sender">
    /// A delegate that performs the actual send and returns an <see cref="HttpResponseMessage"/>.
    /// The provided <see cref="CancellationToken"/> must be honored for per-attempt timeout.
    /// </param>
    /// <param name="maxRetries">
    /// Maximum number of retry attempts after the initial send. Defaults to <c>3</c>.
    /// </param>
    /// <param name="perTryTimeout">
    /// Optional per-attempt timeout. Defaults to <c>5</c> seconds.
    /// </param>
    /// <param name="isTransient">
    /// Optional predicate to determine whether a response represents a transient failure
    /// eligible for retry. Defaults to treating <see cref="HttpStatusCode.RequestTimeout"/>,
    /// <see cref="HttpStatusCode.TooManyRequests"/>, and any <c>5xx</c> status code as transient.
    /// </param>
    /// <param name="ct">
    /// Overall operation <see cref="CancellationToken"/> that will be linked with per-attempt timeouts.
    /// </param>
    /// <returns>
    /// The first successful <see cref="HttpResponseMessage"/>, or <see langword="null"/> if
    /// all retries failed or the circuit was open.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A <b>circuit breaker</b> opens after a threshold number of consecutive failures,
    /// preventing further calls until a cooldown interval passes. Once the cooldown expires,
    /// the breaker transitions to half-open and allows one trial call.
    /// </para>
    /// <para>
    /// Retry delays follow exponential backoff (100ms, 200ms, 400ms, …) capped at 2 seconds,
    /// with up to 100ms of random jitter to reduce thundering herd effects.
    /// </para>
    /// </remarks>
    public static async Task<HttpResponseMessage?> SendWithResilienceAsync(
     Func<CancellationToken, Task<HttpResponseMessage>> sender,
     int maxRetries = 3,
     TimeSpan? perTryTimeout = null,
     Func<HttpResponseMessage, bool>? isTransient = null,
     CancellationToken ct = default,
     ILogger? logger = null)
    {
        perTryTimeout ??= TimeSpan.FromSeconds(5);
        isTransient ??= static (resp) =>
            resp.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            or >= HttpStatusCode.InternalServerError;

        var breaker = new CircuitBreaker(5, _openToHalfOpen);

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (!breaker.AllowRequest())
            {
                logger?.PM().CircuitOpen(attempt);
                throw new HttpRequestException("Circuit open; skipping send.");
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(perTryTimeout.Value);

            try
            {
                var response = await sender(cts.Token).ConfigureAwait(false);
                if (!isTransient(response))
                {
                    logger?.PM().SendSuccess(attempt);
                    breaker.OnSuccess();
                    return response;
                }

                logger?.PM().TransientRetry(attempt, response.StatusCode);
                breaker.OnFailure();
                response.Dispose();
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                logger?.PM().SendTimeout(attempt);
                breaker.OnFailure();
            }
            catch (Exception ex)
            {
                logger?.PM().SendFailure(attempt, ex);
                breaker.OnFailure();
            }

            var delayMs = (int)Math.Min(2000, 100 * Math.Pow(2, attempt));
            delayMs += Random.Shared.Next(0, 100);
            await Task.Delay(delayMs, ct).ConfigureAwait(false);

            breaker.TryHalfOpen();
        }

        return null;
    }


    /// <summary>
    /// A simple internal circuit breaker for resilience control.
    /// </summary>
    private sealed class CircuitBreaker
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _openToHalfOpen;
        private int _failures;
        private State _state = State.Closed;
        private DateTimeOffset _openedAt;

        public CircuitBreaker(int failureThreshold, TimeSpan openToHalfOpen)
        {
            _failureThreshold = failureThreshold;
            _openToHalfOpen = openToHalfOpen;
        }

        public bool AllowRequest() =>
            _state switch
            {
                State.Open => DateTimeOffset.UtcNow - _openedAt > _openToHalfOpen,
                _ => true
            };

        public void OnFailure()
        {
            if (_state == State.Open)
                return;
            if (++_failures >= _failureThreshold)
            {
                _state = State.Open;
                _openedAt = DateTimeOffset.UtcNow;
            }
        }

        public void OnSuccess()
        {
            _failures = 0;
            _state = State.Closed;
        }

        public void TryHalfOpen()
        {
            if (_state == State.Open && DateTimeOffset.UtcNow - _openedAt > _openToHalfOpen)
                _state = State.HalfOpen;
        }

        private enum State
        {
            Closed,
            Open,
            HalfOpen
        }
    }
}
