// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Norr.PerformanceMonitor.OpenTelemetry;

/// <summary>
/// Provides DI extensions that register the Norr OpenTelemetry bridge and configure
/// OpenTelemetry Tracing/Metrics pipelines with safe defaults.
/// - Respects OTEL_* environment variables (e.g., OTEL_EXPORTER_OTLP_ENDPOINT)
/// - Adds common instrumentations (ASP.NET Core, HttpClient, Runtime)
/// - Lights up optional features via reflection (Process, Prometheus, Console, OTLP exporters)
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNorrOpenTelemetryBridge(
        this IServiceCollection services,
        Action<OtelBridgeOptions>? configure = null)
    {
        var opts = new OtelBridgeOptions();
        configure?.Invoke(opts);

        services.AddSingleton(opts);
        services.AddSingleton<OtelPerformanceMonitorBridge>();

        static IEnumerable<KeyValuePair<string, object>> CoerceAttributes(IDictionary<string, object?> src) =>
            src.Where(kv => kv.Value is not null)
               .Select(kv => new KeyValuePair<string, object>(kv.Key, kv.Value!));

        services.AddOpenTelemetry()
            .ConfigureResource(rb =>
            {
                var entry = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

                var serviceName = opts.ServiceName
                                  ?? entry.GetName().Name
                                  ?? "Norr.PerformanceMonitor";

                var serviceVersion = opts.ServiceVersion
                                     ?? entry.GetName().Version?.ToString()
                                     ?? "0.0.0";

                rb.AddService(serviceName: serviceName, serviceVersion: serviceVersion);
                rb.AddTelemetrySdk();

                if (opts.GlobalAttributes.Count > 0)
                    rb.AddAttributes(CoerceAttributes(opts.GlobalAttributes));
            })
            .WithTracing(tb =>
            {
                if (!opts.EnableTracing)
                    return;

                tb.AddAspNetCoreInstrumentation()
                  .AddHttpClientInstrumentation()
                  .AddSource(opts.ActivitySourceName ?? opts.ServiceName ?? "Norr.PerformanceMonitor");

                tb.SetSampler(new TraceIdRatioBasedSampler(Math.Clamp(opts.TraceSamplingRatio, 0, 1)));

                var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    // Optional OTLP exporter (no hard dependency)
                    _OtelOptional.TryAddOtlpExporter(tb);
                }

                if (IsDevelopment())
                {
                    // Optional console exporter (no hard dependency)
                    _OtelOptional.TryAddConsoleExporter(tb);
                }
            })
            .WithMetrics(mb =>
            {
                if (!opts.EnableMetrics)
                    return;

                mb.AddAspNetCoreInstrumentation()
                  .AddHttpClientInstrumentation()
                  .AddRuntimeInstrumentation()
                  .AddMeter(opts.MeterName ?? opts.ServiceName ?? "Norr.PerformanceMonitor");

                // Optional: Process metrics (only if the package is present)
                _OtelOptional.TryAddProcessInstrumentation(mb);

                // Optional: Prometheus scraping endpoint (only if the package is present and enabled via options)
                if (opts.EnablePrometheusScrapingEndpoint)
                    _OtelOptional.TryAddPrometheusExporter(mb);

                var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    // Optional OTLP exporter (no hard dependency)
                    _OtelOptional.TryAddOtlpExporter(mb);
                }

                if (IsDevelopment())
                {
                    // Optional console exporter (no hard dependency)
                    _OtelOptional.TryAddConsoleExporter(mb);
                }
            });

        return services;

        static bool IsDevelopment()
            => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                   ?.Equals("Development", StringComparison.OrdinalIgnoreCase) == true;
    }
}

/// <summary>
/// Reflection-based helpers that light up optional features without hard package dependencies.
/// This allows consumers to opt-in simply by referencing the corresponding packages.
/// </summary>
internal static class _OtelOptional
{
    public static bool TryAddProcessInstrumentation(MeterProviderBuilder builder)
        => TryInvoke(
            builder,
            assemblyName: "OpenTelemetry.Instrumentation.Process",
            typeName: "OpenTelemetry.Instrumentation.Process.ProcessMeterProviderBuilderExtensions",
            methodName: "AddProcessInstrumentation");

    public static bool TryAddPrometheusExporter(MeterProviderBuilder builder)
        => TryInvoke(
            builder,
            assemblyName: "OpenTelemetry.Exporter.Prometheus.AspNetCore",
            typeName: "OpenTelemetry.Exporter.Prometheus.AspNetCore.PrometheusAspNetCoreMeterProviderBuilderExtensions",
            methodName: "AddPrometheusExporter");

    // ------ Console exporter (optional) ------
    public static bool TryAddConsoleExporter(TracerProviderBuilder builder) =>
        TryAddByScanning(builder, "OpenTelemetry.Exporter.Console", "AddConsoleExporter");

    public static bool TryAddConsoleExporter(MeterProviderBuilder builder) =>
        TryAddByScanning(builder, "OpenTelemetry.Exporter.Console", "AddConsoleExporter");

    // ------ OTLP exporter (optional) ------
    public static bool TryAddOtlpExporter(TracerProviderBuilder builder) =>
        TryAddByScanning(builder, "OpenTelemetry.Exporter.OpenTelemetryProtocol", "AddOtlpExporter", allowOptionsDelegate: true);

    public static bool TryAddOtlpExporter(MeterProviderBuilder builder) =>
        TryAddByScanning(builder, "OpenTelemetry.Exporter.OpenTelemetryProtocol", "AddOtlpExporter", allowOptionsDelegate: true);

    /// <summary>
    /// Scans the given assembly for a public static extension method with the given name
    /// that accepts the provided builder (or its base) as the first parameter. If an overload
    /// also accepts an options delegate (Action&lt;TOptions&gt;), a no-op delegate is supplied.
    /// </summary>
    private static bool TryAddByScanning(object builder, string assemblyName, string methodName, bool allowOptionsDelegate = false)
    {
        var asm = LoadAssembly(assemblyName);
        if (asm is null)
            return false;

        var builderType = builder.GetType();

        foreach (var type in asm.GetExportedTypes())
        {
            foreach (var mi in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (!string.Equals(mi.Name, methodName, StringComparison.Ordinal))
                    continue;

                var ps = mi.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(builderType))
                {
                    _ = mi.Invoke(null, new[] { builder });
                    return true;
                }

                if (allowOptionsDelegate && ps.Length == 2 && ps[0].ParameterType.IsAssignableFrom(builderType))
                {
                    // Expecting Action<TOptions>
                    var optionsParamType = ps[1].ParameterType;
                    if (optionsParamType.IsGenericType &&
                        optionsParamType.GetGenericTypeDefinition() == typeof(Action<>))
                    {
                        var tOptions = optionsParamType.GetGenericArguments()[0];
                        var noopDelegate = CreateNoopActionDelegate(tOptions);
                        _ = mi.Invoke(null, new[] { builder, noopDelegate! });
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static Delegate? CreateNoopActionDelegate(Type optionsType)
    {
        // Bind the generic method Noop<T>(T _) to a closed Action<T>
        var method = typeof(_OtelOptional).GetMethod(nameof(Noop), BindingFlags.NonPublic | BindingFlags.Static)!;
        var closed = method.MakeGenericMethod(optionsType);
        return Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(optionsType), closed);
    }

    private static void Noop<T>(T _)
    {
        // intentionally empty
    }

    private static bool TryInvoke(
        MeterProviderBuilder builder,
        string assemblyName,
        string typeName,
        string methodName)
    {
        var asm = LoadAssembly(assemblyName);
        var type = asm?.GetType(typeName, throwOnError: false);
        var mi = type?.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(MeterProviderBuilder) },
            modifiers: null);

        if (mi is null)
            return false;

        _ = mi.Invoke(null, new object[] { builder });
        return true;
    }

    private static Assembly? LoadAssembly(string name)
    {
        try
        {
            return Assembly.Load(new AssemblyName(name));
        }
        catch { return null; }
    }
}
