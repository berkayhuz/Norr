// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;                // <-- GetDbConnection() extension burada
using Microsoft.EntityFrameworkCore.Diagnostics;
using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Configuration;
using Norr.PerformanceMonitor.Telemetry;

namespace Norr.PerformanceMonitor.Integrations.EFCore;

public sealed class MonitoringDbCommandInterceptor : DbCommandInterceptor
{
    private readonly IMonitor _monitor;
    private readonly EfCoreMonitorOptions _options;

    public MonitoringDbCommandInterceptor(IMonitor monitor, IOptions<EfCoreMonitorOptions> options)
    {
        _monitor = monitor;
        _options = options.Value;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        => ExecuteSync(command, eventData, () => base.ReaderExecuting(command, eventData, result));

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        => ExecuteAsync(command, eventData, () => base.ReaderExecutingAsync(command, eventData, result, ct));

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        => ExecuteSync(command, eventData, () => base.NonQueryExecuting(command, eventData, result));

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        => ExecuteAsync(command, eventData, () => base.NonQueryExecutingAsync(command, eventData, result, ct));

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        => ExecuteSync(command, eventData, () => base.ScalarExecuting(command, eventData, result));

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken ct = default)
        => ExecuteAsync(command, eventData, () => base.ScalarExecutingAsync(command, eventData, result, ct));

    private InterceptionResult<T> ExecuteSync<T>(
        DbCommand cmd, CommandEventData data, Func<InterceptionResult<T>> next)
    {
        using var scope = _monitor.Begin("efcore.command");

        using var _ctx = TagContext.Begin(new[]
        {
            new KeyValuePair<string, object?>("db.system",   data.Context?.Database.ProviderName),
            new KeyValuePair<string, object?>("db.name",     data.Context?.Database.GetDbConnection().Database),
            new KeyValuePair<string, object?>("db.operation", data.CommandSource.ToString()),
        });

        AttachSqlTags(cmd);
        return next();
    }

    private async ValueTask<InterceptionResult<T>> ExecuteAsync<T>(
        DbCommand cmd, CommandEventData data, Func<ValueTask<InterceptionResult<T>>> next)
    {
        using var scope = _monitor.Begin("efcore.command");

        using var _ctx = TagContext.Begin(new[]
        {
            new KeyValuePair<string, object?>("db.system",   data.Context?.Database.ProviderName),
            new KeyValuePair<string, object?>("db.name",     data.Context?.Database.GetDbConnection().Database),
            new KeyValuePair<string, object?>("db.operation", data.CommandSource.ToString()),
        });

        AttachSqlTags(cmd);
        return await next().ConfigureAwait(false);
    }

    private void AttachSqlTags(DbCommand cmd)
    {
        var sql = cmd.CommandText ?? string.Empty;

        if (_options.ScrubSql)
        {
            // Eski API'yı kullanan kodları kırmamak için shim'i çağırıyoruz:
            sql = TagScrubber.NormalizeAndMask(sql, _options.MaxSqlLength);
        }

        using var _stmt = TagContext.Begin("db.statement", sql);

        // Parametre ad+tip bilgilerini scrublı şekilde çerçeve olarak ekleyelim
        if (cmd.Parameters.Count > 0)
        {
            var kvs = new KeyValuePair<string, object?>[cmd.Parameters.Count];
            for (int i = 0; i < cmd.Parameters.Count; i++)
            {
                var p = cmd.Parameters[i]!;
                // Değeri maskele; sadece tip/size bırak
                var val = $"{p.DbType}{(p.Size > 0 ? $"({p.Size})" : "")}";
                kvs[i] = new KeyValuePair<string, object?>($"db.param.{p.ParameterName}", val);
            }
            using var _params = TagContext.Begin(kvs);
        }

        if (cmd.CommandTimeout > 0)
        {
            using var _timeout = TagContext.Begin("db.timeout.seconds", cmd.CommandTimeout);
        }
    }
}
