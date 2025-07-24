using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Alerting;
using Norr.PerformanceMonitor.Configuration;
using Norr.PerformanceMonitor.Exporters;
using Norr.PerformanceMonitor.Sampling;

using Monitor = Norr.PerformanceMonitor.Core.Monitor;

namespace Norr.PerformanceMonitor.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> that wire-up all
/// components required by the performance-monitoring library.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core monitor, sampling / duplicate-guard logic, exporters
    /// and alert sinks.  
    /// Call this once in your application’s startup code:
    /// <code>
    /// services.AddPerformanceMonitoring(o =>
    /// {
    ///     o.Sampling.Probability = 0.1;           // 10 % sampling
    ///     o.Exporters = ExporterFlags.Prometheus; // scrape endpoint
    /// });
    /// </code>
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configure">
    /// Optional delegate that customises <see cref="PerformanceOptions"/>.
    /// </param>
    public static IServiceCollection AddPerformanceMonitoring(
        this IServiceCollection services,
        Action<PerformanceOptions>? configure = null)
    {
        // ---------------- Options -------------------------------------------------------------

        if (configure is not null)
            services.Configure(configure);

        // expose sub-sections directly for constructor injection
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PerformanceOptions>>().Value.Sampling);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PerformanceOptions>>().Value.DuplicateGuard);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PerformanceOptions>>().Value.Alerts);

        // ---------------- Alert sinks ---------------------------------------------------------

        services.AddHttpClient(); // shared client factory

        services.AddSingleton<IAlertSink>(sp =>
        {
            var alerts = sp.GetRequiredService<AlertOptions>();
            var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient();

            if (alerts.SlackWebhook is not null)
                return new SlackAlertSink(client, alerts.SlackWebhook);

            if (alerts.WebhookUrl is not null)
                return new WebhookAlertSink(client, alerts.WebhookUrl);

            return new NullAlertSink(); // fallback → no-op
        });

        services.AddSingleton<NullAlertSink>();

        // ---------------- Core pipeline -------------------------------------------------------

        services.AddSingleton<ISampler, ProbabilitySampler>();
        services.AddSingleton<IDuplicateGuard, BloomDuplicateGuard>();
        services.AddSingleton<IMonitor, Monitor>();

        // ---------------- Default exporters ---------------------------------------------------

        // In-memory exporter is handy for unit-tests; register as metric exporter
        services.AddSingleton<InMemoryExporter>();
        services.AddSingleton<IMetricExporter>(sp => sp.GetRequiredService<InMemoryExporter>());

        return services;
    }

    /// <summary>
    /// No-op implementation used when no Slack or generic webhook is configured.
    /// </summary>
    private sealed class NullAlertSink : IAlertSink
    {
        public Task SendAsync(PerfAlert _, CancellationToken __ = default) => Task.CompletedTask;
    }
}



//var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddPerformanceMonitoring(o =>
//{
//    o.Sampling = SamplingRate.Everything;
//    o.SlowCallThresholdMs = 100;
//    o.Exporters = ExporterFlags.Console | ExporterFlags.InMemory;
//});

//var app = builder.Build();
//app.UsePerformanceMonitoring();

//app.MapGet("/", async (_, IPerformanceMonitor pm) =>
//{
//    using var _ = pm.Begin("RootEndpoint");
//    await Task.Delay(Random.Shared.Next(20, 150));
//    return "hi";
//});

//app.Run();


//builder.Services.AddPerformanceMonitoring(o =>
//{
//    o.Alerts = new AlertOptions
//    {
//        DurationMs   = 100,                 // 100 ms’ten uzun her çağrı
//        AllocBytes   = 5_000_000,           // 5 MB+
//        SlackWebhook = new Uri(Environment.GetEnvironmentVariable("SLACK_HOOK")!)
//    };
//o.Exporters = ExporterFlags.Console;    // metrikler hâlâ konsola da düşsün
//});


//builder.Services.AddPerformanceMonitoring(o =>
//{
//    o.Sampling.Probability = 0.05;          // ≈ %5 örnekle
//    o.DuplicateGuard.CoolDown = TimeSpan.FromSeconds(30);
//    o.DuplicateGuard.BitCount = 1 << 18;    // 256 Kbit ≈ 32 KB
//});



//app.MapPost("/flamegraph/start", (FlamegraphManager mgr) =>
//{
//    mgr.Start();
//    return Results.Ok("Recording…");
//});

//app.MapPost("/flamegraph/stop", async (FlamegraphManager mgr) =>
//{
//    var path = await mgr.StopAsync();
//    return Results.File(path, "application/json", Path.GetFileName(path));
//});
