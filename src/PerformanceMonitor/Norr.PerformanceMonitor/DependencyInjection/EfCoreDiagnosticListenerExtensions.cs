// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Norr.PerformanceMonitor.Integrations.EFCore;

namespace Norr.PerformanceMonitor.DependencyInjection;

public static class EfCoreDiagnosticListenerExtensions
{
    public static IServiceCollection AddEfCoreDiagnosticListener(this IServiceCollection services)
    {
        services.AddSingleton<IHostedService, EfCoreDiagnosticListenerHostedService>();
        return services;
    }
}
