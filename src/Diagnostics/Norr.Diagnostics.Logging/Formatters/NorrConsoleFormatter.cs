// Copyright (c) Norr
// Licensed under the MIT license.
#nullable enable 
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Norr.Diagnostics.Logging;

public sealed class NorrConsoleFormatter : ConsoleFormatter
{
    public NorrConsoleFormatter() : base("norr") { }

    public override void Write<TState>(in LogEntry<TState> logEntry,
                                       IExternalScopeProvider? scopeProvider,
                                       TextWriter textWriter)
    {
        var ts = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var lvl = logEntry.LogLevel.ToString().ToUpperInvariant();
        var evt = logEntry.EventId.Name ?? $"[{logEntry.EventId.Id:0000}]";

        var sb = new StringBuilder();
        if (scopeProvider is not null)
        {
            scopeProvider.ForEachScope<StringBuilder>((object? s, StringBuilder b) =>
            {
                switch (s)
                {
                    case IEnumerable<KeyValuePair<string, object?>> kvs:
                        foreach (var kv in kvs)
                            b.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
                        break;
                    default:
                        b.Append(' ').Append(s);
                        break;
                }
            }, sb);
        }
        var scope = sb.ToString();

        textWriter.WriteLine($"{ts} {lvl} {evt}{scope} {logEntry.State}");
        if (logEntry.Exception is Exception ex)
            textWriter.WriteLine(ex);
    }
}
