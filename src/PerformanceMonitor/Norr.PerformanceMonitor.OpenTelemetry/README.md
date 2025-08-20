# Norr.PerformanceMonitor

**Advanced performance monitoring library for .NET with OpenTelemetry, threshold-based alerting, and flamegraph profiling support.**  
📦 [NuGet Package](https://www.nuget.org/packages/Norr.PerformanceMonitor)  
🔗 [GitHub Repository](https://github.com/berkayhuz/Norr)

---

## 🚀 Overview

`Norr.PerformanceMonitor` is a lightweight and extensible monitoring toolkit designed for production-grade .NET applications.

It captures:

- ⏱️ Execution time
- 🧠 CPU usage
- 💾 Memory allocations  
…and exports them to Console, Prometheus, OTLP, or any alerting destination.

---

### ✨ Key Features

- ⚡ **Minimal-code performance tracking** – measure execution with a simple `using` block or source-generated attributes.
- 🧠 **Three metrics out-of-the-box**: `DurationMs`, `CpuMs`, and `AllocBytes`.
- 🧵 **Built-in integrations** for ASP.NET Core, MediatR, MassTransit, and `BackgroundService`.
- 🚨 **Threshold-based alerting** via Slack or custom webhooks.
- 📈 **Flamegraph profiling** with Speedscope export.
- 📊 **Prometheus & OTLP exporter support** – no adapter needed.
- 🧪 **Snapshot-based testability** – great for regression-proof metric assertions.
- 🔒 **Zero-overhead design** – safe for production environments.
- 🧩 **Observability + Performance + Alerting** combined in one simple library.
- 🧰 No custom exporter or setup boilerplate required – just plug & play.

---

## 📦 Installation

```bash
dotnet add package Norr.PerformanceMonitor
```

Register the library in your DI container:

```csharp
services.AddPerformanceMonitoring(o =>
{
    o = new PerformanceOptions
    {
        Sampling = new SamplingOptions
        {
            Probability = 0.1
        },
        Alerts = new AlertOptions
        {
            DurationMs = 500,
            CpuMs = 100,
            AllocBytes = 1_000_000
        },
        Exporters = ExporterFlags.Console | ExporterFlags.Prometheus
    };
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

| Option                        | Description                                           |
|-------------------------------|-------------------------------------------------------|
| `SamplingOptions.Probability` | Percentage of operations to sample (0.0 - 1.0)        |
| `AlertOptions.DurationMs`     | Alert threshold for wall-clock duration (ms)          |
| `AlertOptions.AllocBytes`     | Alert threshold for memory allocation (bytes)         |
| `AlertOptions.CpuMs`          | Alert threshold for CPU usage (ms)                    |
| `ExporterFlags`               | Console, InMemory, Prometheus, OTLP support           |
| `DuplicateGuardOptions`       | Bloom filter size and cooldown (anti-spam)            |

---

## 🧠 Why Norr.PerformanceMonitor?

Unlike traditional metric libraries, `Norr.PerformanceMonitor` combines:

- **🧩 Unified Observability Stack** → performance + profiling + alerts
- **🧪 Testable Snapshots** → assert metrics directly in unit tests
- **🔥 Flamegraph Tooling** → profile background or critical paths
- **⚙️ Minimal Setup** → no custom exporters, no YAML config, no vendor lock-in

> Whether you're building APIs, message consumers, or background daemons – Norr is a drop-in performance brain for your .NET service.

---

## 🧾 Credits

Built and maintained by [@berkayhuz](https://github.com/berkayhuz)  
Part of the [**Norr**](https://github.com/berkayhuz/Norr) .NET ecosystem  
Licensed under [MIT](https://opensource.org/licenses/MIT)
