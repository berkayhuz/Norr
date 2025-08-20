// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

using Norr.Diagnostics.Abstractions.Logging;
using Norr.Diagnostics.Logging.Context;
using Norr.Diagnostics.Logging.Enrichers;

namespace Norr.Diagnostics.Logging.Extensions;

public static class LoggerExtensionsDependency
{
    /// <summary>Console’a Norr biçiminde yazan logger’ı ekler.</summary>
    public static ILoggingBuilder AddNorrConsole(this ILoggingBuilder builder)
    {
        builder.AddConsole(options =>
        {
            options.FormatterName = "norr";
        });
        builder.AddConsoleFormatter<NorrConsoleFormatter, ConsoleFormatterOptions>();
        return builder;
    }

    /// <summary>Servislere accessor ve varsayılan enricher’ları ekler.</summary>
    public static IServiceCollection AddNorrLogging(this IServiceCollection services)
    {
        services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
        services.AddSingleton<ILogEnricher, MachineEnricher>();
        services.AddSingleton<ILogEnricher, VersionEnricher>();
        return services;
    }
}
