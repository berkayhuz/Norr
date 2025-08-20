// Copyright (c) Norr
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Configuration;
using Norr.PerformanceMonitor.Exporters.Core;
using Norr.PerformanceMonitor.Exporters.Json;

namespace Norr.PerformanceMonitor.DependencyInjection;

public static class JsonLinesMetricExporterServiceCollectionExtensions
{
    public static IServiceCollection AddJsonLinesMetricExporter(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionPath = "Performance:Exporters:JsonLines")
    {
        services.AddOptions<JsonLinesExporterOptions>()
                .Bind(configuration.GetSection(sectionPath));

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMetricExporter>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<JsonLinesExporterOptions>>().Value;
            var env = sp.GetService<IHostEnvironment>();

            var path = ResolvePath(opt.Path, env?.ContentRootPath);
            return new JsonLinesFileMetricExporter(path, opt.Capacity, opt.MaxBatchSize, opt.DropPolicy, opt.Append);
        }));

        return services;
    }
    public static IServiceCollection AddJsonLinesMetricExporter(
        this IServiceCollection services,
        Action<JsonLinesExporterOptions> configure)
    {
        services.Configure(configure);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMetricExporter>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<JsonLinesExporterOptions>>().Value;
            var env = sp.GetService<IHostEnvironment>();
            var path = ResolvePath(opt.Path, env?.ContentRootPath);
            return new JsonLinesFileMetricExporter(path, opt.Capacity, opt.MaxBatchSize, opt.DropPolicy, opt.Append);
        }));
        return services;
    }
    private static string ResolvePath(string path, string? contentRoot)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path);
        if (Path.IsPathFullyQualified(expanded))
            return expanded;

        var root = contentRoot ?? AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(root, expanded));
    }
}
