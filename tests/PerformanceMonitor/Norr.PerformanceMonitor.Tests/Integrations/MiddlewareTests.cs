using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Norr.PerformanceMonitor.Configuration;
using Norr.PerformanceMonitor.DependencyInjection;
using Norr.PerformanceMonitor.Exporters;
using Norr.PerformanceMonitor.Integrations.AspNetCore;
using Norr.PerformanceMonitor.Profiling;

namespace Norr.PerformanceMonitor.Tests.Integrations;

public class MiddlewareTests
{
    private static IHost BuildHost() =>
        new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddPerformanceMonitoring(o =>
                        o.Exporters = ExporterFlags.InMemory);
                });
                web.Configure(app =>
                {
                    app.UsePerformanceMonitoring();
                    app.Run(async ctx => await ctx.Response.WriteAsync("ok"));
                });
            })
            .Start();

    [Fact]
    public async Task Pipeline_returns_200_and_records_metric()
    {
        using var host = BuildHost();
        var client = host.GetTestClient();

        var response = await client.GetAsync("/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var exporter = host.Services.GetRequiredService<InMemoryExporter>();
        exporter.Snapshot().Should().Contain(m => m.Name == "HTTP GET /");
    }

    [Fact]
    public async Task Flamegraph_recorder_creates_file()
    {
        var file = Path.Combine(Path.GetTempPath(),
                       $"flame_{Guid.NewGuid()}.speedscope.json");

        await using (var rec = FlamegraphRecorder.Start(file))
        {
            Thread.Sleep(200);
        }
        File.Exists(file).Should().BeTrue();
    }
}
