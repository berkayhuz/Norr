using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.Extensions.Options;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Alerting;
using Norr.PerformanceMonitor.Configuration;

namespace Norr.PerformanceMonitor.Tests.Alerting;
public sealed class FakeAlertSink : IAlertSink
{
    public readonly List<PerfAlert> Alerts = new();
    public Task SendAsync(PerfAlert a, CancellationToken _)
    {
        Alerts.Add(a);
        return Task.CompletedTask;
    }
}

public class AlertTests
{
    [Fact]
    public void Threshold_triggers_alert()
    {
        var sink = new FakeAlertSink();

        var opt = new PerformanceOptions
        {
            Alerts = new AlertOptions { DurationMs = 1 }
        };

        var mon = new PerformanceMonitor.Core.Monitor(
            Options.Create(opt),
            Array.Empty<IMetricExporter>(),
            new[] { sink });

        using (mon.Begin("Slow"))
        {
            Thread.Sleep(5);
        }

        sink.Alerts.Should().ContainSingle(a => a.MetricName == "Slow");
    }
}
