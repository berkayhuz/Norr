using System;

using FluentAssertions;

using Microsoft.Extensions.Options;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Configuration;
using Norr.PerformanceMonitor.Core;
using Norr.PerformanceMonitor.Exporters;

namespace Norr.PerformanceMonitor.Tests.Monitor;
public class MonitorTests
{
    [Fact]
    public void Begin_end_records_metric()
    {
        var exporter = new InMemoryExporter();
        var mon = new PerformanceMonitor.Core.Monitor(
            Options.Create(new PerformanceOptions()),
            new[] { exporter },
            Array.Empty<IAlertSink>());

        using (mon.Begin("Unit"))
        {
        }

        exporter.Snapshot()
                .Should()
                .ContainSingle(m => m.Name == "Unit" && m.Kind == MetricKind.DurationMs);
    }
    [Fact]
    public void Sampling_zero_records_nothing()
    {
        var exporter = new InMemoryExporter();
        var mon = new PerformanceMonitor.Core.Monitor(
            Options.Create(new PerformanceOptions { Sampling = new() { Probability = 0 } }),
            new[] { exporter },
            Array.Empty<IAlertSink>());

        using (mon.Begin("Skip"))
        {
        }

        exporter.Snapshot().Should().BeEmpty();
    }
}
