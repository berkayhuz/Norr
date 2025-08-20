// Copyright (c) Norr
// Licensed under the MIT license.

using System.Net;

using Microsoft.Extensions.Logging;

namespace Norr.Diagnostics.Abstractions.Catalog.PerformanceMonitor;
public static class NorrLogPM
{
    private static readonly EventId _e_StartMonitoring = new(1001, "[PM:1001]");
    private static readonly EventId _e_Completed = new(1201, "[PM:1201]");
    private static readonly EventId _e_Error = new(2001, "[PM:2001]");

    private static readonly EventId _e_InsecureScheme = new(2002, "[PM:2002]");
    private static readonly EventId _e_HostNotAllowed = new(2003, "[PM:2003]");
    private static readonly EventId _e_PortBlocked = new(2004, "[PM:2004]");
    private static readonly EventId _e_PortNotAllowed = new(2005, "[PM:2005]");
    private static readonly EventId _e_DnsFailure = new(2006, "[PM:2006]");
    private static readonly EventId _e_PrivateAddress = new(2007, "[PM:2007]");
    private static readonly EventId _e_LoopbackAddress = new(2008, "[PM:2008]");

    private static readonly EventId _e_CircuitOpen = new(2009, "[PM:2009]");
    private static readonly EventId _e_TransientRetry = new(3002, "[PM:3002]");
    private static readonly EventId _e_SendSuccess = new(1202, "[PM:1202]");
    private static readonly EventId _e_SendTimeout = new(20010, "[PM:2010]");
    private static readonly EventId _e_SendFailure = new(20011, "[PM:2011]");

    private static readonly EventId _e_RetryAttempt = new(3003, "[PM:3003]");

    private static readonly EventId _e_FlameStart = new(8001, "[PM:8001]");
    private static readonly EventId _e_FlameSaved = new(8002, "[PM:8002]");

    private static readonly Action<ILogger, string, Exception?> _startMonitoring =
        LoggerMessage.Define<string>(LogLevel.Trace, _e_StartMonitoring, "[PM:1001] Start monitoring: {Target}");

    private static readonly Action<ILogger, double, Exception?> _completed =
        LoggerMessage.Define<double>(LogLevel.Information, _e_Completed, "[PM:1201] Completed in {ElapsedMs} ms");

    private static readonly Action<ILogger, string, Exception?> _error =
        LoggerMessage.Define<string>(LogLevel.Error, _e_Error, "[PM:2001] Error: {Reason}");

    private static readonly Action<ILogger, string, Exception?> _insecureScheme =
    LoggerMessage.Define<string>(LogLevel.Error, _e_InsecureScheme,
        "[PM:2002] Webhook target is not HTTPS: {Scheme}");

    private static readonly Action<ILogger, string, Exception?> _hostNotAllowed =
        LoggerMessage.Define<string>(LogLevel.Error, _e_HostNotAllowed,
            "[PM:2003] Webhook target host '{Host}' is not in the allowed list");

    private static readonly Action<ILogger, int, Exception?> _portBlocked =
        LoggerMessage.Define<int>(LogLevel.Error, _e_PortBlocked,
            "[PM:2004] Webhook target port {Port} is blocked");

    private static readonly Action<ILogger, int, Exception?> _portNotAllowed =
        LoggerMessage.Define<int>(LogLevel.Error, _e_PortNotAllowed,
            "[PM:2005] Webhook target port {Port} is not in the allowed ports list");

    private static readonly Action<ILogger, string, Exception?> _dnsFailure =
        LoggerMessage.Define<string>(LogLevel.Error, _e_DnsFailure,
            "[PM:2006] DNS resolution failed for target '{Host}'");

    private static readonly Action<ILogger, string, Exception?> _privateAddress =
        LoggerMessage.Define<string>(LogLevel.Error, _e_PrivateAddress,
            "[PM:2007] Target resolved to private address: {Address}");

    private static readonly Action<ILogger, string, Exception?> _loopbackAddress =
        LoggerMessage.Define<string>(LogLevel.Error, _e_LoopbackAddress,
            "[PM:2008] Target resolved to loopback address: {Address}");

    private static readonly Action<ILogger, int, Exception?> _circuitOpen =
    LoggerMessage.Define<int>(LogLevel.Error, _e_CircuitOpen, "[PM:2009] Attempt {Attempt} skipped: circuit open");

    private static readonly Action<ILogger, int, HttpStatusCode, Exception?> _transientRetry =
        LoggerMessage.Define<int, HttpStatusCode>(LogLevel.Warning, _e_TransientRetry,
            "[PM:3002] Attempt {Attempt} received transient status {StatusCode}; will retry");

    private static readonly Action<ILogger, int, Exception?> _sendSuccess =
        LoggerMessage.Define<int>(LogLevel.Information, _e_SendSuccess, "[PM:1202] Attempt {Attempt} succeeded");

    private static readonly Action<ILogger, int, Exception?> _sendTimeout =
        LoggerMessage.Define<int>(LogLevel.Error, _e_SendTimeout, "[PM:2010] Attempt {Attempt} timed out");

    private static readonly Action<ILogger, int, Exception?> _sendFailure =
        LoggerMessage.Define<int>(LogLevel.Error, _e_SendFailure, "[PM:2011] Attempt {Attempt} failed with exception");

    private static readonly Action<ILogger, string, int, int, string?, long, Exception?> _retryAttempt =
    LoggerMessage.Define<string, int, int, string?, long>(
        LogLevel.Warning,
        _e_RetryAttempt,
        "[PM:3003] [{Sink}] attempt {Attempt} got status {StatusCode} ({Reason}), retrying after {DelayMs} ms");

    private static readonly Action<ILogger, string, Exception?> _flameStart =
        LoggerMessage.Define<string>(LogLevel.Information, _e_FlameStart, "[PM:8001] Flamegraph recording started → {File}");

    private static readonly Action<ILogger, string, Exception?> _flameSaved =
        LoggerMessage.Define<string>(LogLevel.Information, _e_FlameSaved, "[PM:8002] Flamegraph saved → {File}");

    public static void EmitStartMonitoring(ILogger logger, string target) => _startMonitoring(logger, target, null);
    public static void EmitCompleted(ILogger logger, double elapsedMs) => _completed(logger, elapsedMs, null);
    public static void EmitError(ILogger logger, string reason) => _error(logger, reason, null);
    public static void InsecureScheme(ILogger logger, string scheme) => _insecureScheme(logger, scheme, null);
    public static void HostNotAllowed(ILogger logger, string host) => _hostNotAllowed(logger, host, null);
    public static void PortBlocked(ILogger logger, int port) => _portBlocked(logger, port, null);
    public static void PortNotAllowed(ILogger logger, int port) => _portNotAllowed(logger, port, null);
    public static void DnsFailure(ILogger logger, string host, Exception ex) => _dnsFailure(logger, host, ex);
    public static void PrivateAddress(ILogger logger, string addr) => _privateAddress(logger, addr, null);
    public static void LoopbackAddress(ILogger logger, string addr) => _loopbackAddress(logger, addr, null);
    public static void CircuitOpen(ILogger logger, int attempt) => _circuitOpen(logger, attempt, null);
    public static void TransientRetry(ILogger logger, int attempt, HttpStatusCode code) => _transientRetry(logger, attempt, code, null);
    public static void SendSuccess(ILogger logger, int attempt) => _sendSuccess(logger, attempt, null);
    public static void SendTimeout(ILogger logger, int attempt) => _sendTimeout(logger, attempt, null);
    public static void SendFailure(ILogger logger, int attempt, Exception ex) => _sendFailure(logger, attempt, ex);
    public static void RetryAttempt(ILogger logger, string sink, int attempt, int code, string? reason, long delayMs) =>
    _retryAttempt(logger, sink, attempt, code, reason, delayMs, null);
    public static void EmitFlameStart(ILogger logger, string file) => _flameStart(logger, file, null);
    public static void EmitFlameSaved(ILogger logger, string file) => _flameSaved(logger, file, null);
}
