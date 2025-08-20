// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Norr.PerformanceMonitor.Abstractions;

namespace Norr.PerformanceMonitor.Integrations.EFCore;

internal sealed class EfCoreDiagnosticListenerHostedService : IHostedService, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    private readonly ILogger<EfCoreDiagnosticListenerHostedService> _log;
    private readonly IMonitor _monitor;
    private IDisposable? _allSub;
    private IDisposable? _efSub;

    public EfCoreDiagnosticListenerHostedService(ILogger<EfCoreDiagnosticListenerHostedService> log, IMonitor monitor)
    {
        _log = log;
        _monitor = monitor;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _allSub = DiagnosticListener.AllListeners.Subscribe(this);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _efSub?.Dispose();
            _allSub?.Dispose();
        }
        catch { }
        return Task.CompletedTask;
    }

    // IObserver<DiagnosticListener>
    public void OnNext(DiagnosticListener value)
    {
        if (value.Name == "Microsoft.EntityFrameworkCore")
            _efSub = value.Subscribe(this);
    }
    public void OnCompleted()
    {
    }
    public void OnError(Exception error)
    {
    }

    // IObserver<KeyValuePair<string, object?>> – EF Core eventleri
    public void OnNext(KeyValuePair<string, object?> kv)
    {
        try
        {
            switch (kv.Key)
            {
                case "Microsoft.EntityFrameworkCore.CommandExecuting":
                    BeginScope(kv.Value);
                    break;
                case "Microsoft.EntityFrameworkCore.CommandExecuted":
                case "Microsoft.EntityFrameworkCore.CommandError":
                    EndScope(kv.Value);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "EFCore DiagnosticListener handling failed");
        }
    }

    private static readonly ConditionalWeakTable<object, IDisposable> _scopes = new();

    private void BeginScope(object? payload)
    {
        if (payload is null)
            return;
        // EF Core CommandEventData
        var opName = "efcore.command";
        var scope = _monitor.Begin(opName);

        // Etiketler – mümkün olduğunca Reflection ile
        TryTag(scope, payload, "CommandText", name: "db.statement");
        TryTag(scope, payload, "Command", name: "db.command");
        TryTag(scope, payload, "ExecuteMethod", name: "db.operation");

        _scopes.Add(payload, scope);
    }

    private void EndScope(object? payload)
    {
        if (payload is null)
            return;
        if (_scopes.TryGetValue(payload, out var scope))
        {
            scope.Dispose();
            _scopes.Remove(payload);
        }
    }

    private static void TryTag(IDisposable scope, object payload, string prop, string name)
    {
        try
        {
            var pi = payload.GetType().GetProperty(prop);
            var val = pi?.GetValue(payload);
            if (val is not null)
            {
                using var _ = Norr.PerformanceMonitor.Telemetry.TagContext.Begin(name, val);
            }
        }
        catch { }
    }
}
