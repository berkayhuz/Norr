// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Norr.PerformanceMonitor.Configuration;

namespace Norr.PerformanceMonitor.DependencyInjection;

public static class RuntimeCountersServiceCollectionExtensions
{
    public static IServiceCollection AddRuntimeCounters(this IServiceCollection services)
    {
        services.AddOptions<RuntimeCountersOptions>();
        services.AddSingleton<IHostedService, SystemRuntimeCountersListener>();
        return services;
    }
}
