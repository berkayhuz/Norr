// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable
using Microsoft.Extensions.DependencyInjection;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Telemetry.Process;

namespace Norr.PerformanceMonitor.DependencyInjection;

public static class ProcessProbeServiceCollectionExtensions
{
    public static IServiceCollection AddProcessProbe(this IServiceCollection services)
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            services.AddSingleton<IProcessProbe, LinuxProcfsProcessProbe>();
        else
            services.AddSingleton<IProcessProbe, WindowsProcessProbe>();
        return services;
    }
}
