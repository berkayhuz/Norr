# Norr.PerformanceMonitor.Attribution

🔧 Source generator for automatic performance instrumentation in .NET.  
Just add `[MeasurePerformance]` to your methods — and get profiling for free.

📦 [NuGet Package](https://www.nuget.org/packages/Norr.PerformanceMonitor.Attribution)  
🔗 [GitHub Repository](https://github.com/berkayhuz/Norr)

---

## ✨ What It Does

This package includes:

- 🧠 `[MeasurePerformance]` attribute
- ⚙️ `PerformanceSourceGenerator` that auto-generates wrapper methods
- 🪶 Lightweight, Roslyn-based source generation (no runtime overhead)
- 🔍 Automatically measures:
  - ⏱ Execution duration
  - 🧠 Memory allocation
  - 🔥 CPU usage (if supported)

---

## 🚀 Example

```csharp
using Norr.PerformanceMonitor.Attribution;

public class MyService
{
    [MeasurePerformance]
    public void DoHeavyWork()
    {
        Thread.Sleep(500);
    }
}
```

After build, a partial method like this will be generated:

```csharp
public partial void DoHeavyWork_WithPerf(IPerformanceMonitor monitor)
{
    using var _ = monitor.Begin("MyService.DoHeavyWork");
    DoHeavyWork();
}
```

Use the generated method wherever you want full performance tracing.

---

## 🧩 Usage with Norr.PerformanceMonitor

```csharp
var service = new MyService();
service.DoHeavyWork_WithPerf(monitor);
```

You can then export data via:

- Console
- Prometheus
- OTLP
- Webhooks (Slack, Discord, etc.)

---

## 🛠️ Requirements

- .NET 6 or newer (supports .NET Standard 2.0 for compatibility)
- Roslyn-compatible IDE (Visual Studio 2022+, Rider, etc.)

---

## 📦 Installation

```bash
dotnet add package Norr.PerformanceMonitor.Attribution
```

Make sure your `.csproj` includes:

```xml
<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
```

---

## 📚 Related Packages

- [`Norr.PerformanceMonitor`](https://www.nuget.org/packages/Norr.PerformanceMonitor): Core performance monitoring engine
- [`Norr.PerformanceMonitor.Attribution`](https://www.nuget.org/packages/Norr.PerformanceMonitor.Attribution): Source generator & attributes

---

## 📄 License

MIT License — See [LICENSE](../LICENSE)

---

Built with by [@berkayhuz](https://github.com/berkayhuz)
