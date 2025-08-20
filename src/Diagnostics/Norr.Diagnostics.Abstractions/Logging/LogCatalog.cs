// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using System.Net;

using Microsoft.Extensions.Logging;

using Norr.Diagnostics.Abstractions.Catalog.PerformanceMonitor;

namespace Norr.Diagnostics.Abstractions.Logging;

public static class NorrLoggerPackages
{
    public static PmLogger PM(this ILogger logger) => new(logger);

    public readonly struct PmLogger
    {
        private readonly ILogger _l;

        public PmLogger(ILogger l) => _l = l;
        public void StartMonitoring(string target) => NorrLogPM.EmitStartMonitoring(_l, target);
        public void Completed(double elapsedMs) => NorrLogPM.EmitCompleted(_l, elapsedMs);
        public void Error(string reason) => NorrLogPM.EmitError(_l, reason);
        public void InsecureScheme(string scheme) => NorrLogPM.InsecureScheme(_l, scheme);
        public void HostNotAllowed(string host) => NorrLogPM.HostNotAllowed(_l, host);
        public void PortBlocked(int port) => NorrLogPM.PortBlocked(_l, port);
        public void PortNotAllowed(int port) => NorrLogPM.PortNotAllowed(_l, port);
        public void DnsFailure(string host, Exception ex) => NorrLogPM.DnsFailure(_l, host, ex);
        public void PrivateAddress(string addr) => NorrLogPM.PrivateAddress(_l, addr);
        public void LoopbackAddress(string addr) => NorrLogPM.LoopbackAddress(_l, addr);
        public void CircuitOpen(int attempt) => NorrLogPM.CircuitOpen(_l, attempt);
        public void SendSuccess(int attempt) => NorrLogPM.SendSuccess(_l, attempt);
        public void TransientRetry(int attempt, HttpStatusCode code) => NorrLogPM.TransientRetry(_l, attempt, code);
        public void SendTimeout(int attempt) => NorrLogPM.SendTimeout(_l, attempt);
        public void SendFailure(int attempt, Exception ex) => NorrLogPM.SendFailure(_l, attempt, ex);
        public void RetryAttempt(string sink, int attempt, int code, string? reason, long delayMs) => NorrLogPM.RetryAttempt(_l, sink, attempt, code, reason, delayMs);
        public void FlameStart(string file) => NorrLogPM.EmitFlameStart(_l, file);
        public void FlameSaved(string path) => NorrLogPM.EmitFlameSaved(_l, path);
    }
}
