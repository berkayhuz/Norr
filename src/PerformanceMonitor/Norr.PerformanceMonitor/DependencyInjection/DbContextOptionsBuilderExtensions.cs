// Copyright (c) Norr
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Norr.PerformanceMonitor.Configuration;
using Norr.PerformanceMonitor.Integrations.EFCore;

namespace Norr.PerformanceMonitor.DependencyInjection;
public static class EfCoreMonitoringExtensions
{
    public static void AddEfCoreMonitoring(this IServiceCollection services,
        Action<EfCoreMonitorOptions>? configure = null)
    {
        services.AddOptions<EfCoreMonitorOptions>()
                .Configure(o => configure?.Invoke(o));
        services.AddSingleton<MonitoringDbCommandInterceptor>();
    }

    /// <summary>DbContextOptionsBuilder tarafında çağırın.</summary>
    public static DbContextOptionsBuilder UsePerformanceMonitoring(
        this DbContextOptionsBuilder builder, IServiceProvider sp)
    {
        var interceptor = sp.GetRequiredService<MonitoringDbCommandInterceptor>();
        return builder.AddInterceptors(interceptor);
    }
}
