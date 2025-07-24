# Norr.PerformanceMonitor

**Advanced performance monitoring library for .NET with OpenTelemetry, threshold-based alerting, and flamegraph profiling support.**  
📦 [NuGet Package](https://www.nuget.org/packages/Norr.PerformanceMonitor)  
🔗 [GitHub Repository](https://github.com/berkayhuz/Norr)

---

## 🚀 Overview

`Norr.PerformanceMonitor` is a lightweight and extensible monitoring toolkit designed for production-grade .NET applications.

It captures:

- ⏱️ Execution time
- 🔥 CPU usage
- 🧠 Memory allocations  
…and exports them to console, Prometheus, OTLP, or any alerting destination.

### ✨ Key Features

- ⚡ Low-overhead performance tracking
- 🧵 `using`-based measurement scopes
- 🎯 Attribute-based source generation: [`Norr.PerformanceMonitor.Attribution`](https://www.nuget.org/packages/Norr.PerformanceMonitor.Attribution)
- 🧠 Sampling & deduplication logic (bloom filter)
- 🚨 Slack / webhook alerting on thresholds
- 📊 Flamegraph generation (`.speedscope.json`)
- 🧩 Ready-to-use integrations: ASP.NET Core, MassTransit, MediatR, BackgroundService

---

## 📦 Installation

```bash
dotnet add package Norr.PerformanceMonitor
```

Register the library in your DI container:

```csharp
services.AddPerformanceMonitoring(o =>
{
    o.Sampling.Probability = 0.1;
    o.Alerts.DurationMs    = 500;
    o.Exporters            = ExporterFlags.Console | ExporterFlags.Prometheus;
});
```

ASP.NET Core middleware:

```csharp
app.UsePerformanceMonitoring();
```

---

## ⚡ Quick Start

### 🧪 Measure any method

```csharp
[MeasurePerformance]
public void DoWork()
{
    Thread.Sleep(200);
}
```

> 💡 Requires installing [`Norr.PerformanceMonitor.Attribution`](https://www.nuget.org/packages/Norr.PerformanceMonitor.Attribution)

---

### 🔁 Monitor background workers

```csharp
public sealed class MyWorker : BackgroundServiceWrapper
{
    public MyWorker(IMonitor m) : base(m) { }

    protected override async Task ExecuteCoreAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
            await Task.Delay(1000, stop);
    }
}
```

---

### 📬 MediatR + MassTransit integration

```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
cfg.UseConsumeFilter(typeof(PerformanceFilter<>), provider);
```

---

## 🔥 Flamegraph Export

```csharp
await using var rec = FlamegraphRecorder.Start("hot.speedscope.json");
DoHotStuff();
```

Then open the file at [speedscope.app](https://www.speedscope.app)

---

## ⚙️ Configuration Reference

| Option                        | Description                                          |
|------------------------------|------------------------------------------------------|
| `SamplingOptions.Probability`| Percentage of operations to sample (0.0 - 1.0)       |
| `AlertOptions.DurationMs`    | Alert threshold for wall-clock duration (ms)         |
| `AlertOptions.AllocBytes`    | Alert threshold for memory allocation (bytes)        |
| `ExporterFlags`              | Console, InMemory, Prometheus, OTLP support          |
| `DuplicateGuardOptions`      | Bloom filter size and cooldown (anti-spam)           |

---

## ❤️ Credits

Built and maintained by [@berkayhuz](https://github.com/berkayhuz)  
Part of the [**Norr**](https://github.com/berkayhuz/Norr) .NET ecosystem  
Licensed under [MIT](https://opensource.org/licenses/MIT)