// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable

using System;
using Microsoft.Extensions.DependencyInjection;

// Ambiguity fix: Configuration'daki tipi sabitliyoruz
using HttpClientMonitorOptions = Norr.PerformanceMonitor.Configuration.HttpClientMonitorOptions;
using Norr.PerformanceMonitor.Integrations.Http;

namespace Norr.PerformanceMonitor.DependencyInjection;

public static class HttpMonitoringServiceCollectionExtensions
{
    public static IHttpClientBuilder AddMonitoringHandler(
        this IHttpClientBuilder builder,
        Action<HttpClientMonitorOptions>? configure = null)
    {
        builder.Services
               .AddOptions<HttpClientMonitorOptions>()
               .Configure(o => configure?.Invoke(o));

        builder.Services.AddTransient<MonitoringDelegatingHandler>();
        return builder.AddHttpMessageHandler<MonitoringDelegatingHandler>();
    }
}
