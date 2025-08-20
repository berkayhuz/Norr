// Copyright (c) Norr
// Licensed under the MIT license.
#nullable enable 
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Extensions.Logging;

namespace Norr.Diagnostics.Logging.SerilogBridge;

public static class SerilogLoggerFactoryExtensions
{
    public static ILoggingBuilder UseNorrSerilog(this ILoggingBuilder builder, Serilog.ILogger? serilog = null, bool dispose = true)
    {
        Log.Logger = serilog ?? new LoggerConfiguration().CreateLogger();
        builder.ClearProviders();
        builder.AddProvider(new SerilogLoggerProvider(Log.Logger, dispose));
        return builder;
    }
}

