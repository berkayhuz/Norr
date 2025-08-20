# Norr.PerformanceMonitor (Core)

A **.NET 9**, production-grade, low-overhead **performance measurement** library.\
With a single API, it measures **duration (ms)**, **allocated memory (bytes)**, **CPU time (ms)**, and optionally **CPU percentage / cycles**; sends the data to **exporters**; and produces **alerts** when thresholds are exceeded.\
Optional **Prometheus endpoint** and integrations for **ASP.NET Core / MediatR / MassTransit / BackgroundService** are included.

> NuGet: `Norr.PerformanceMonitor` — License: MIT

---

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
  - [Minimal Console/Worker](#minimal-consoleworker)
  - [ASP.NET Core (Minimal API)](#aspnet-core-minimal-api)
- [How It Works (Measured Metrics)](#how-it-works-measured-metrics)
- [Tags and Personal Data Scrubbing](#tags-and-personal-data-scrubbing)
- [Configuration](#configuration)
- [Exporters](#exporters)
- [Alert Sinks](#alert-sinks)
- [Integrations](#integrations)
- [Usage in Tests](#usage-in-tests)
- [Performance and Overhead Notes](#performance-and-overhead-notes)
- [FAQ](#faq)
- [License](#license)

---

## Features

- **Simple API** → Start a scope with `IMonitor.Begin("Operation")`; measurements are automatically recorded when `using` scope exits.
- **Rich metric set** → duration (ms), allocated memory (bytes), CPU time (ms), and **optional** CPU % and CPU cycles.
- **Low overhead** → efficient sampling, duplicate-guard, tag scrubbing, and histogram/summary aggregation.
- **Exporter architecture** → Console + in‑process aggregation by default; add your own `IMetricExporter`.
- **Prometheus** → Built‑in minimal endpoint or compatible with OpenTelemetry Prometheus scraping.
- **Alert system** → Threshold-based alerts to Slack or generic webhook (with security policies).
- **Integrations** → ASP.NET Core middleware/route tagging, MediatR behavior, MassTransit consume filter, and `BackgroundService` wrapper for background jobs.
- **Ambient tags** → Carry tags across a scope via `TagContext` with low GC pressure.
- **Clean architecture** → `Abstractions`, `Core`, `Configuration`, `Exporters`, `Integrations`, `Sampling`, `Telemetry`.

> For full OpenTelemetry support, see **Norr.PerformanceMonitor.OpenTelemetry** (separate package).

---

## Installation

```bash
dotnet add package Norr.PerformanceMonitor
```

Default DI extension registers the core, **in‑process aggregation**, and **console exporter**:

```csharp
// Program.cs
using Norr.PerformanceMonitor.DependencyInjection;

builder.Services.AddPerformanceMonitor();
```

> **Note:** The `Monitor` class uses `IOptions<PerformanceOptions>`. Hosting templates already include Options support. For custom settings, see the [Configuration](#configuration) section.

---

## Quick Start

### Minimal Console/Worker

```csharp
// Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.DependencyInjection;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddPerformanceMonitor(); // Monitor + Aggregation + Console
    })
    .Build();

var monitor = host.Services.GetRequiredService<IMonitor>();

using (monitor.Begin("jobs.ingest"))
{
    // work...
    await Task.Delay(120);
}
```

### ASP.NET Core (Minimal API)

```csharp
// Program.cs
using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.DependencyInjection;
using Norr.PerformanceMonitor.Integrations.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPerformanceMonitor();

var app = builder.Build();

// Optional: Tag route template as http.route
app.UseRouteTagging();

// Optional: Norr’s lightweight Prometheus endpoint
app.UseNorrPrometheusEndpoint("/metrics");

// Alternative: Auto-bind to OpenTelemetry Prometheus if present (reflection-based, no-op otherwise)
app.TryUseOpenTelemetryPrometheusScrapingEndpoint();

app.MapGet("/orders/{id:int}", (int id, IMonitor monitor) =>
{
    using (monitor.Begin("HTTP GET /orders/{id}"))
    {
        return Results.Ok(new { id });
    }
});

app.Run();
```

---

## How It Works (Measured Metrics)

`Monitor` produces the following metrics per scope:

| Name                  | Type          | Description                                     |
| --------------------- | ------------- | ----------------------------------------------- |
| `method.duration.ms`  | **Histogram** | Wall-clock duration (ms)                        |
| `method.alloc.bytes`  | **Histogram** | Allocated managed memory (bytes)                |
| `method.cpu.ms`       | **Histogram** | CPU time (user + kernel, ms)                    |
| `method.cpu.pct`      | **Histogram** | `cpu_ms / elapsed_ms * 100` (single-core scale) |
| `method.cpu.pct_norm` | **Histogram** | CPU % normalized to core count                  |
| `method.cpu.cycles`   | **Histogram** | CPU cycle count (*Windows only*)                |

Metrics are sent to exporters **immediately** and also streamed to **in‑process aggregation**.\
The Prometheus endpoint exposes this aggregated data in **text exposition** format.

---

## Tags and Personal Data Scrubbing

### Ambient TagContext

Carry tags across a whole scope without passing them manually:

```csharp
using Norr.PerformanceMonitor.Telemetry;

using var _ambient = TagContext.Begin(
    ("user.id", userId),
    ("tenant", tenant),
    ("feature", "checkout"));

using (monitor.Begin("OrderService.PlaceOrder"))
{
    // All metrics in this scope will have the above tags
}
```

### Scrubbing (PII/URL/JWT masking)

Before export, all tags are **normalized** and **masked** by `TagScrubber`.\
Default policy masks email addresses, JWTs, full URLs, and truncates overly long values. Customize via `MetricsOptions.Scrub`.

---

## Configuration

The root config is `PerformanceOptions`; configure it via DI:

```csharp
using Norr.PerformanceMonitor.Configuration;

builder.Services.Configure<PerformanceOptions>(o =>
{
    o.Cpu.Mode = CpuMeasureMode.ThreadTime;        // default
    o.Cpu.RecordPercentOfElapsed = true;           // method.cpu.pct
    o.Cpu.RecordPercentNormalizedToCores = false;  // method.cpu.pct_norm
    // o.Cpu.RecordCycles = true; // Windows only

    o.Sampling.Probability = 1.0;                  // 0..1
    o.Sampling.Mode = SamplerMode.Deterministic;   // Deterministic | Random
    // o.Sampling.NameProbabilities["HTTP GET /health"] = 0.0;

    o.DuplicateGuard.CoolDown = TimeSpan.FromSeconds(10);

    o.Metrics.IncludeThreadId = false;
    o.Metrics.GlobalTags["deployment.environment"] = "dev";
    // o.Metrics.Scrub.MaxValueLength = 256;

    o.Alerts.DurationMs = 2_000;   // 2 sec
    o.Alerts.AllocBytes = 10_000_000;
    o.Alerts.CpuMs = 1_000;

    o.Resource.ServiceName = "Shop.Api";
    o.Resource.ServiceVersion = "1.0.0";
});
```

### Key options

- **CPU:** `ThreadTime` (precise) | `ProcessApproximate` (approximate) | `Disabled`
- **Sampling:** Probability, mode, max samples/sec, name-based probabilities
- **DuplicateGuard:** Bloom filter size, cooldown window
- **Metrics:** Global tags, thread ID, scrubbing policy
- **Alerts:** Duration, allocation, CPU thresholds, Slack/webhook targets
- **Resource:** Service name, version, environment

---

## Exporters

Default exporters:

- **AggregationExporter** → Prometheus endpoint + fast in‑process aggregation
- **ConsoleExporter** → Human-readable output for dev/CI

Custom exporter example:

```csharp
using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Core.Metrics;

public sealed class ListExporter : IMetricExporter
{
    public readonly List<Metric> Items = new();
    public void Export(in Metric metric) => Items.Add(metric);
}

services.TryAddEnumerable(ServiceDescriptor.Singleton<IMetricExporter, ListExporter>());
```

### Prometheus endpoints

- **Built‑in endpoint:**

```csharp
app.UseNorrPrometheusEndpoint("/metrics");
```

- **OpenTelemetry Prometheus scraping:**

```csharp
app.TryUseOpenTelemetryPrometheusScrapingEndpoint();
```

---

## Alert Sinks

Alerts (`PerfAlert`) are sent to all registered sinks.

### Slack

```csharp
builder.Services.Configure<PerformanceOptions>(o =>
{
    o.Alerts.SlackWebhook = new Uri("https://hooks.slack.com/services/XXX/YYY/ZZZ");
});

builder.Services.AddHttpClient("norr-alerts");
services.TryAddEnumerable(ServiceDescriptor.Singleton<IAlertSink, SlackAlertSink>());
```

### Generic Webhook

```csharp
builder.Services.Configure<PerformanceOptions>(o =>
{
    o.Alerts.WebhookUrl = new Uri("https://alerts.example.com/ingest");
});

builder.Services.Configure<WebhookSecurityOptions>(sec =>
{
    sec.RequireHttps = true;
    sec.AllowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "alerts.example.com"
    };
    sec.BlockPrivateAddresses = true;
});

builder.Services.AddHttpClient("norr-alerts");
services.TryAddEnumerable(ServiceDescriptor.Singleton<IAlertSink, WebhookAlertSink>());
```

---

## Integrations

### ASP.NET Core

```csharp
app.UseMiddleware<PerformanceMiddleware>();
app.UseRouteTagging();
```

### MediatR

```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
```

### MassTransit

```csharp
busConfigurator.UseConsumeFilter(typeof(PerformanceFilter<>), provider);
```

### BackgroundService

```csharp
public sealed class SyncWorker : BackgroundServiceWrapper
{
    public SyncWorker(IMonitor monitor) : base(monitor) { }
    protected override async Task ExecuteCoreAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(500, stoppingToken);
    }
}
```

---

## Usage in Tests

```csharp
var listExporter = new ListExporter();
services.TryAddEnumerable(ServiceDescriptor.Singleton<IMetricExporter>(listExporter));

var monitor = provider.GetRequiredService<IMonitor>();
using (monitor.Begin("calc.add")) { /* ... */ }

Assert.Contains(listExporter.Items, m => m.Name == "method.duration.ms");
```

---

## Performance and Overhead Notes

- Use **sampling** to reduce load (deterministic mode is stable by name)
- **DuplicateGuard** suppresses duplicate metrics in short windows
- **Tag Scrubbing** removes PII and reduces cardinality
- **ThreadTime** mode is most accurate for CPU; fallback to **ProcessApproximate** if unsupported
- Exporter queues are non-blocking and fail-safe

---

## FAQ

**Does this depend on OpenTelemetry?**\
No. Core package is OTel-independent. Use **Norr.PerformanceMonitor.OpenTelemetry** for OTel integration.

**Can I change metric names/tags?**\
Names are fixed; enrich with scope name, `TagContext`, and `GlobalTags`.

**How do I enable alerts?**\
Configure thresholds in `AlertOptions` and register at least one `IAlertSink`.

**I only want duration measurement.**\
Set `Cpu.Mode = Disabled` and lower sampling.

---

## License

MIT © Norr


---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---

# Norr.PerformanceMonitor — A’dan Z’ye Özellik Ağacı

> .NET 9 için üretim seviyesinde performans ölçüm kütüphanesi. Süre (ms), ayrılan bellek (byte) ve CPU zamanı (ms) ölçer; etiketleme/sansürleme ile telemetry’i zenginleştirir; exporter’lara yollar; eşik aşımlarında uyarı (alert) üretir; Prometheus uç noktası ve ASP.NET Core / MediatR / MassTransit / BackgroundService entegrasyonları sağlar.

---

## A) Abstractions (Genel Sözleşmeler)

* **IMonitor** → Ölçüm başlatır (`Begin(string name)`), `IPerformanceScope` döner.
* **IPerformanceScope** → `using` ile dispose edildiğinde ölçüm tamamlanır.
* **IMetricExporter** → Tekil `Metric` gönderir; non-blocking, exception fırlatmaz.
* **IAlertSink** → `PerfAlert` payload’larını dış sistemlere gönderir (fire-and-forget).
* **ISampler** → Bir operasyonun ölçülüp ölçülmeyeceğine karar verir.
* **IDuplicateGuard** → Kısa pencerede tekrarlar için spam koruması (Bloom benzeri).
* **IThreadCpuTimeProvider** → Thread CPU time / cycles (platform destekli ise).

## B) Architecture (Yapı ve Akış)

* **Scope temelli ölçüm** → `IMonitor.Begin("Operation")` ile stopwatch/CPU/alloc yakalama.
* **Metrik üretimi** → `MetricKind` (DurationMs, AllocBytes, CpuMs) ile veriler oluşturulur.
* **Etiketleme** → `TagContext` (AsyncLocal çerçeveli) + `MetricsOptions.GlobalTags` birleşimi.
* **Scrubbing** → `TagScrubber` ile PII ve yüksek kardinalite değerleri maskelenir/normalize edilir.
* **Sampling & Duplicate Guard** → Overhead ve gürültüyü azaltır.
* **Export** → `IMetricExporter`(lar) aracılığıyla dışa aktarım / toplulaştırma.
* **Alerting** → Eşik aşımında `IAlertSink`’lere uyarı gönderimi; dayanıklılık (retry/timeout).

## C) Configuration (Seçenekler)

* **PerformanceOptions**

  * `Cpu` → **CpuOptions** (ölçüm modu ve yüzdelikler)
  * `Metrics` → **MetricsOptions** (tagging, temporality, scrubbing)
  * `Resource` → **ResourceOptions** (service.name, version, deployment.environment)
  * `Sampling` → **SamplingOptions** (mode, probability, oran limitleme, ad bazlı override)
  * `DuplicateGuard` → **DuplicateGuardOptions** (bit sayısı, cool-down penceresi)
  * `Alerts` → **AlertOptions** (eşikler + hedefler)
  * `Exporters` → **ExporterFlags** (Console, InMemory, Prometheus, OTLP)
  * `Temporality` → **MetricsTemporality** (Default/Cumulative/Delta tercihleri)
* **CpuOptions**

  * `Mode: CpuMeasureMode` → **ThreadTime** (platform destekli kesin ölçüm), …
  * `RecordPercentOfElapsed` → `cpu_ms / elapsed_ms * 100`
  * `RecordPercentNormalizedToCores` → `cpu_ms / (elapsed_ms * CPU core)`
* **MetricsOptions**

  * `GlobalTags` (sabit anahtarlar)
  * `IncludeDiagnostics` (opsiyonel ek tanılar)
  * `Scrub: ScrubbingOptions` (mask/normalize politikaları)
* **ScrubbingOptions**

  * `MaskSensitiveValues` (örn. e‑posta/JWT/UUID) → `Mask` (varsayılan: `[redacted]`)
  * URL normalizasyonu, GUID/sayı/token yer tutucuları, max-length kesme
* **SamplingOptions**

  * `Mode: SamplerMode` (Random/Deterministic/Adaptive…)
  * `Probability` (0..1)
  * `MaxSamplesPerSecond` (+ `RateLimiterBurst`)
  * `NameProbabilities` (adı geçen operasyonlara özel oran)
* **DuplicateGuardOptions**

  * `BitCount` (Bloom bit alanı)
  * `CoolDown` (tekrar bastırma penceresi)
* **AlertOptions**

  * Eşikler: `DurationMs`, `CpuMs`, `AllocBytes`
  * Hedefler: `SlackWebhook`, `WebhookUrl` …
* **WebhookSecurityOptions** *(Configuration.Alerting)*

  * `RequireHttps`, `BlockPrivateAddresses`
  * `AllowedPorts` / `BlockedPorts`

## D) Core (Temel Bileşenler)

* **Monitor** → Ölçüm yaşam döngüsü, exporter ve alert’lere yönelim.
* **PerformanceScope** → Ölçümleri üretir; `MetricsOptions` & `TagContext` birleştirir; `TagScrubber` uygular.
* **MetricKind** → `DurationMs`, `AllocBytes`, `CpuMs`.
* **Metrics.Metric** → (Name, Kind, Value, TimestampUtc) immut. kayıt.
* **Runtime.ThreadCpuTimeProvider** → Platformlar arası CPU time/cycles.

## E) Exporters (Dışa Aktarım)

* **ConsoleExporter** → İnsan okunur tablo satırlarıyla `Console.WriteLine`.
* **AggregationExporter** → `AggregationRegistry`’ye besler (process‑içi toplulaştırma).
* **InMemoryExporter<T>** → Test/demolar için RAM’de tutar (RingBuffer, RollingWindowCounter, metrik sayacı).
* **Core Queue** → `ExporterQueue<T>` & `BoundedChannelQueue` ile arka plan toplu gönderim; `DropPolicy`.
* **Prometheus** *(entegrasyonlar bölümüne bakınız)*
* **OTLP** *(OpenTelemetry ile köprüleme; tercihler `ExporterFlags`/`MetricsTemporality`)*

## F) Aggregation (Toplulaştırma)

* **AggregationRegistry** → İsim → Aggregator eşlemesi (concurrent sözlük).
* **HistogramAggregator** → Sabit bucket sınırları (ms: 1,5,10,25,50,100,250,500,1000,2500,5000,+Inf); count/sum/min/max.
* **SummaryAggregator** → P² algoritması (Jain & Chlamtac, 1985) ile q50/q90/q95/q99; count/sum.

## G) Alerting (Uyarı Mekanizması)

* **PerfAlert** → (MetricName, Kind, Value, Threshold) immut. kayıt.
* **Sinks**

  * **SlackAlertSink** → Incoming Webhook JSON post; güvenli URL doğrulama; retry/timeout/polisy.
  * **WebhookAlertSink** → Genel HTTP hedef; named HttpClient (`"norr-alerts"`), güvenlik opsiyonları.
* **Resilience**

  * **ResiliencePolicies** → Retry (exponential backoff + jitter), per‑try timeout, transient algısı (5xx, 408, 429 varsayılan), circuit‑break benzeri koruma.
  * **AlertHttpPolicies** → Ortak HTTP policy setleri.
  * **Net.UriSafetyGuard** → Hedef URL doğrulamaları (`WebhookSecurityOptions`).
* **Eventing**

  * **AlertEventSource** → ETW/EventSource event’leri: Begin/Success/Retry/Failed/Exception.

## H) Integrations (Çerçeve Entegrasyonları)

* **ASP.NET Core**

  * **PerformanceMiddleware** → Her istek için `Activity` + ortak HTTP tag’ları.
  * **RouteTaggingMiddleware** → Düşük kardinaliteli `http.route` etiketini `TagContext`’e ekler.
  * **PrometheusEndpointExtensions.UseNorrPrometheusEndpoint("/metrics")** → Prometheus text formatı üretir.
  * **PrometheusTextSerializer** → Histogram & Summary’leri text/plain; 0.0.4 formatı.
  * **PrometheusScrapingExtensions** → OTel Prometheus scraping endpoint’ini reflection ile etkinleştirme (varsa).
  * **EndpointAccessor / HttpContextEndpointAccessor** → Farklı TFM’lerde endpoint erişimi (GetEndpoint/IEndpointFeature fallback).
* **MediatR**

  * **PerformanceBehavior\<TReq,TRes>** → İstek/yanıt sürelerini ölçer; `messaging.*` etiketleri ekler.
* **MassTransit**

  * **PerformanceFilter<T>** → Consume sürelerini ölçer; etiketler ekler; Probe desteği.
* **BackgroundService**

  * **BackgroundServiceWrapper** → Çalışma döngüsünü otomatik scope ile sarar; `job.*` etiketleri.

## I) Telemetry (Etiketler & Sansür)

* **TagContext** → `AsyncLocal` tabanlı, ArrayPool ile GC-basıncı düşük çerçeve; push/pop; snapshot görünümü.
* **TagScrubber** → Regex’ler (timeout & NonBacktracking where safe), URL normalizasyonu, token maskeleme, uzunluk kısıtları.

## J) Dependency Injection (Kayıt)

* **AddPerformanceMonitorExtensions** → Varsayılan core kayıt + Aggregation/Console exporter ekleme (TryAddSingleton).
* **ServiceCollectionExtensions** → OTel köprü/probe (varsa), uyarılar ve exporter bayraklarını kolay ayar.

## K) Profiling (Alev Grafiği / Sampling Profiler)

* **FlamegraphRecorder** → EventPipe SampleProfiler yakalama; SpeedScope JSON üretimi.
* **FlamegraphManager** → Başlat/Durdur yardımcıları; dosya yolu log’lama.

## L) Sampling (Örnekleme) & Duplicate Guard (Tekrar Bastırma)

* **SmartSampler** → Deterministic/Random modlar; oran limitleme (token‑bucket, burst); ad bazlı override’lar.
* **ConcurrentBloomDuplicateGuard** → Çift tamponlu Bloom; zaman pencereli bastırma; düşük overhead.

## M) Metrics (Ölçülenler & Semantik)

* **Duration (ms)**, **Allocated Bytes**, **CPU Time (ms)**
* Opsiyonel: CPU % (elapsed’a göre), Core-normalize CPU %, Windows’ta cycle count.
* Zaman damgası: UTC; adlandırma: `Service.Method` / `HTTP VERB /route` gibi düşük kardinaliteli isimler.

## N) Exporters: Prometheus & OTLP

* **Prometheus**

  * Dahili text serileştirici ve middleware ile scrape endpoint.
  * Alternatif: OTel Prometheus Exporter’ı reflection ile etkinleştirme (mevcutsa).
* **OTLP**

  * `ExporterFlags.Otlp` ile tercihen devreye alınır (OpenTelemetry varlığında).

## O) Alerting: Güvenlik & Dayanıklılık Ayrıntıları

* HTTPS zorunluluğu, özel ağ adreslerini engelleme, port beyaz/siyah listeleri.
* Transient hata sınıflandırma (408/429/5xx varsayılan) + retry + per‑try timeout.
* Circuit-break benzeri koruma ile çakılmayı önleme.

## P) Logging & Diagnostics

* **Norr.Diagnostics.Abstractions.Logging** ile entegrasyon (örn. `NorrLoggerPackages`, `LoggerMessage.Define` pattern’i, `PM()` extension’ları).
* Alert/Profiling süreçleri EventSource ve yapılandırılmış loglarla izlenir.

## Q) Kullanım Senaryoları (Ne Yapılır?)

* Uygulama genelinde **operasyon süreleri** ve **CPU/bellek** ölçmek.
* **HTTP istekleri** / **MediatR istekleri** / **MassTransit tüketimleri** / **BackgroundService** işleri için otomatik scope.
* **Prometheus** ile gözlemlenebilirlik; **OTLP** yoluyla APM backend’lerine akış.
* **Eşik tabanlı uyarılar** (Slack / webhook) ve güvenli/hatalara dayanıklı teslim.
* **PII sansürleme** ve **kardinalite kontrolü** ile telemetry hijyeni.
* **Testlerde** InMemory exporter ile metrik doğrulama.

## R) Kurulum & Quick Start

* **NuGet**: `Norr.PerformanceMonitor`
* **DI**: `services.AddPerformanceMonitoring(...);`
* **Minimal kullanım**: `using var scope = monitor.Begin("Operation");` – iş bittiğinde dispose.
* **ASP.NET Core**: `app.UseMiddleware<PerformanceMiddleware>(); app.UseNorrPrometheusEndpoint();`
* **MediatR/MassTransit**: ilgili behavior/filter eklemeleri.

## S) Testlerde Kullanım

* **InMemoryExporter** ile son gönderilen metrikleri doğrulama.
* **AggregationExporter** + PrometheusTextSerializer ile scrape çıktısını snapshot test etme.

## T) Performans & Overhead Notları

* TagContext: ArrayPool + AsyncLocal ile **düşük GC basıncı**.
* Regex’ler: **compiled**, **culture-invariant**, **global timeout**, mümkünse **NonBacktracking**.
* Exporter/AlertSink: **non-blocking**, hata durumunda **log et** ve devam et.
* Sampling/DuplicateGuard ile **yük altında maliyet azaltma**.

## U) Güvenlik & Gizlilik

* **WebhookSecurityOptions** ile hedef doğrulama (HTTPS, private IP engeli, port sınırlamaları).
* **ScrubbingOptions** ile PII masking & URL normalizasyonu.

## V) Namespaces & Klasör Yapısı

* `Norr.PerformanceMonitor.Abstractions`
* `Norr.PerformanceMonitor.Core` (+ `Core.Metrics`, `Core.Metrics.Aggregation`, `Core.Runtime`)
* `Norr.PerformanceMonitor.Configuration` (+ `Configuration.Alerting`)
* `Norr.PerformanceMonitor.Exporters` (+ `Exporters.Core`, `Exporters.InMemory`)
* `Norr.PerformanceMonitor.Alerting` (+ `Alerting.Resilience`, `Alerting.Net`, `Alerting.Slack`, `Alerting.Webhook`)
* `Norr.PerformanceMonitor.Integrations` (+ `AspNetCore`, `MediatR`, `MassTransit`, `Background`)
* `Norr.PerformanceMonitor.Telemetry`
* `Norr.PerformanceMonitor.Sampling`
* `Norr.PerformanceMonitor.Profiling`
* `Norr.PerformanceMonitor.DependencyInjection`

## W) Genişletilebilirlik (Extensibility)

* **Exporter ekleme** → `IMetricExporter` implement edin; DI’ye kaydedin.
* **Alert sink ekleme** → `IAlertSink` implement edin; `AlertOptions` ile devreye alın.
* **Sampler/Guard özelleştirme** → `ISampler`, `IDuplicateGuard` implement edin.
* **CPU sağlayıcı** → `IThreadCpuTimeProvider` ile platforma özel ölçüm.

## X) Bilinen Sınırlar & Notlar

* CPU ölçümü bazı platformlarda desteklenmeyebilir → `CpuOptions.Mode` ile kapatılabilir.
* InMemory exporter **üretim için uygun değil** (bellek sınırlaması yok).
* Regex scrubbing teorik ReDoS’a karşı kısıtlarla sertleştirilmiştir; yine de **yüksek hacimli** serileştirme öncesi seçici uygulanmalı.

## Y) Dosya/Dizin Örnekleri (Kısa Liste)

* **Abstractions**: `IMonitor.cs`, `IPerformanceScope.cs`, `IMetricExporter.cs`, `IAlertSink.cs`, `ISampler.cs`, `IDuplicateGuard.cs`, `IThreadCpuTimeProvider.cs`
* **Core**: `Monitor.cs`, `PerformanceScope.cs`, `MetricKind.cs`, `Metrics/Metric.cs`, `Metrics/Aggregation/*`, `Runtime/ThreadCpuTimeProvider.cs`
* **Configuration**: `PerformanceOptions.cs`, `MetricsOptions.cs`, `CpuOptions.cs`, `SamplingOptions.cs`, `DuplicateGuardOptions.cs`, `ResourceOptions.cs`, `ScrubbingOptions.cs`, `AlertOptions.cs`, `WebhookSecurityOptions.cs`, `ExporterFlags.cs`, `MetricsTemporality.cs`
* **Exporters**: `ConsoleExporter.cs`, `AggregationExporter.cs`, `Core/*`, `InMemory/*`
* **Alerting**: `PerfAlert.cs`, `AlertEventSource.cs`, `AlertHttpPolicies.cs`, `Resilience/ResiliencePolicies.cs`, `Net/UriSafetyGuard.cs`, `Slack/SlackAlertSink.cs`, `Webhook/WebhookAlertSink.cs`
* **Integrations**: `AspNetCore/*` (Middleware, Prometheus, Endpoint accessor), `MediatR/PerformanceBehavior.cs`, `MassTransit/PerformanceFilter.cs`, `Background/BackgroundServiceWrapper.cs`
* **Telemetry**: `TagContext.cs`, `TagScrubber.cs`
* **Sampling**: `SmartSampler.cs`, `ConcurrentBloomDuplicateGuard.cs`
* **Profiling**: `FlamegraphRecorder.cs`, `FlamegraphManager.cs`
* **DI**: `AddPerformanceMonitorExtensions.cs`, `ServiceCollectionExtensions.cs`

## Z) Hızlı SSS (FAQ) – Özet

* **“Yalnızca süre istiyorum.”** → `Cpu.Mode = Disabled`; Sampling oranını düşürün.
* **“Alert’leri nasıl açarım?”** → `AlertOptions` eşikleri ayarlayın; en az bir `IAlertSink` kaydedin.
* **“Etiketleri nasıl beslerim?”** → `TagContext.Push({...})` + `MetricsOptions.GlobalTags` birleşimi; `TagScrubber` otomatik uygulanır.

---

### Ek Notlar

* **README bölümleri**: Features, Installation, Quick Start (Console/Worker + ASP.NET Core), How it Works (Measured Metrics), Tags & Scrubbing, Configuration, Exporters, Alert Sinks, Integrations, Usage in Tests, Performance Notes, FAQ, License.
* **Sürüm/Çerçeve**: .NET 9
* **Lisans**: MIT

---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---
---

# Norr.PerformanceMonitor Documentation Site — Information Architecture

> English‑first navigation and page outline following the Diátaxis structure (Tutorials, How‑to Guides, Concepts, Reference). Suitable for Docusaurus, MkDocs, or VitePress.

---

## Top‑level Navigation

1. **Home** (`/`)
2. **Get Started** (`/get-started/`)
3. **Tutorials** (`/tutorials/`)
4. **How‑to Guides** (`/how-to/`)
5. **Concepts** (`/concepts/`)
6. **Reference** (`/reference/`)
7. **Examples** (`/examples/`)
8. **Troubleshooting** (`/troubleshooting/`)
9. **Release Notes** (`/releases/`)
10. **FAQ** (`/faq/`)
11. **Glossary** (`/glossary/`)

---

## Home (`/`)

* **Hero**: What is Norr.PerformanceMonitor?
* **Key Features**: low‑overhead scopes, CPU/allocs, tagging & scrubbing, exporters, alerts, integrations.
* **Quick links**: Install → Quickstart → Prometheus → Alerts → ASP.NET Core.
* **Compatibility**: .NET 9, MIT License.

---

## Get Started (`/get-started/`)

### Pages

* **Overview**

  * H2: Why Norr?
  * H2: What gets measured (Duration, CPU ms, Allocated bytes)
  * H2: Architecture at a glance (scopes → exporters → alerts)
* **Installation**

  * H2: Install via NuGet
  * H2: Target frameworks / prerequisites
  * H2: Minimal DI setup
* **Quickstart: Console/Worker**

  * H2: Configure services
  * H2: Measure a code block
  * H2: View metrics (Console / In‑Memory)
* **Quickstart: ASP.NET Core**

  * H2: Middlewares (Performance, Route Tagging)
  * H2: Expose Prometheus endpoint
  * H2: Verify with a scrape
* **Quickstart: MediatR & MassTransit**

  * H2: Add behaviors/filters
  * H2: Measure request/consumer latency
  * H2: Enrich with messaging tags
* **Configuration Overview**

  * H2: PerformanceOptions map
  * H2: Sensible defaults
  * H2: Production checklist

---

## Tutorials (`/tutorials/`) — Step‑by‑step

* **Add monitoring to an existing API**

  * H2: Install packages
  * H2: Register monitor & middlewares
  * H2: Confirm metrics with Prometheus
* **Set up Slack alerts for slow endpoints**

  * H2: Configure AlertOptions
  * H2: Secure webhook (HTTPS + private ranges)
  * H2: Test retries & circuit protection
* **Expose Prometheus metrics**

  * H2: Add endpoint
  * H2: Scrape with Prometheus
  * H2: Graph in Grafana
* **Write a custom exporter**

  * H2: Implement IMetricExporter
  * H2: Register in DI
  * H2: Validate in tests
* **Use TagContext & Scrubbing safely**

  * H2: Push/pop tags
  * H2: Scrubbing rules & PII masking
  * H2: Cardinality best practices
* **Integration testing with InMemory exporter**

  * H2: Arrange exporter
  * H2: Invoke code under test
  * H2: Assert emitted metrics
* **Collect flamegraphs from production**

  * H2: Start/stop recorder
  * H2: Download SpeedScope JSON
  * H2: Interpret hotspots

---

## How‑to Guides (`/how-to/`) — Task‑oriented

* **Measure a code path with scopes**

  * H2: `IMonitor.Begin()` patterns
  * H2: Naming conventions
* **Attach global and per‑request tags**

  * H2: `MetricsOptions.GlobalTags`
  * H2: `TagContext` in ASP.NET
* **Mask PII and normalize values**

  * H2: `ScrubbingOptions`
  * H2: Regex timeouts & NonBacktracking
* **Tune sampling for high‑traffic services**

  * H2: `SamplerMode` & `Probability`
  * H2: Rate limiting & name overrides
* **Suppress duplicates with Bloom guard**

  * H2: `DuplicateGuardOptions`
  * H2: Choosing bit counts & windows
* **Enable CPU time measurement**

  * H2: `CpuOptions.Mode`
  * H2: Percent of elapsed / core‑normalized
* **Enable OpenTelemetry / OTLP export**

  * H2: Exporter flags
  * H2: Temporality choices
* **Create a custom `IAlertSink`**

  * H2: Payload contract
  * H2: Resilience policies
* **Harden webhook delivery**

  * H2: `WebhookSecurityOptions`
  * H2: Allowed/blocked ports & private IPs
* **Configure retries, timeouts, circuit break**

  * H2: `ResiliencePolicies`
  * H2: Transient detection (408/429/5xx)
* **ASP.NET Core middleware setup**

  * H2: `PerformanceMiddleware`
  * H2: `RouteTaggingMiddleware`
* **Background jobs**

  * H2: `BackgroundServiceWrapper`
  * H2: Job tagging
* **MediatR**

  * H2: `PerformanceBehavior`
  * H2: Request/response tags
* **MassTransit**

  * H2: `PerformanceFilter`
  * H2: Consume metrics & Probe
* **Prometheus scraping options**

  * H2: Built‑in endpoint
  * H2: OTel Prometheus exporter bridge
* **Diagnostics & logging with NorrLogPM**

  * H2: Event IDs & levels
  * H2: Recommended log templates

---

## Concepts (`/concepts/`) — Explanations

* **Measurement model**

  * H2: Scopes, metrics, timestamps
  * H2: `MetricKind` (DurationMs, AllocBytes, CpuMs)
  * H2: Temporality (Delta/Cumulative)
* **Resources & Tags**

  * H2: Service identity (name, version, env)
  * H2: Tag merging precedence
* **Scrubbing model**

  * H2: PII categories
  * H2: Regex hardening & limits
* **Sampling strategies**

  * H2: Random / Deterministic / Adaptive
  * H2: Overhead vs fidelity trade‑offs
* **Duplicate guard**

  * H2: Bloom filters in practice
  * H2: Cool‑down windows
* **Export pipeline**

  * H2: Queues, backpressure, drop policies
  * H2: Console, Aggregation, In‑Memory, OTLP
* **Alerting model**

  * H2: Thresholds & dimensions
  * H2: Sinks & resilience
* **Profiling**

  * H2: EventPipe SampleProfiler
  * H2: SpeedScope format
* **Performance & overhead**

  * H2: GC pressure control
  * H2: Regex compilation & timeouts
* **Security & privacy**

  * H2: Webhook security
  * H2: Data minimization

---

## Reference (`/reference/`) — API & Options

### Configuration

* **PerformanceOptions**
* **CpuOptions**
* **MetricsOptions**
* **ScrubbingOptions**
* **SamplingOptions**
* **DuplicateGuardOptions**
* **ResourceOptions**
* **AlertOptions**
* **WebhookSecurityOptions**
* **ExporterFlags**
* **MetricsTemporality**

### Abstractions

* **IMonitor**
* **IPerformanceScope**
* **IMetricExporter**
* **IAlertSink**
* **ISampler**
* **IDuplicateGuard**
* **IThreadCpuTimeProvider**

### Core Types

* **Monitor**
* **PerformanceScope**
* **MetricKind**
* **Metrics.Metric**

### Exporters

* **ConsoleExporter**
* **AggregationExporter**
* **InMemoryExporter**
* **PrometheusTextSerializer**

### Alerting

* **PerfAlert**
* **AlertEventSource**
* **ResiliencePolicies**
* **AlertHttpPolicies**
* **UriSafetyGuard**
* **SlackAlertSink**
* **WebhookAlertSink**

### Integrations

* **ASP.NET Core**

  * PerformanceMiddleware
  * RouteTaggingMiddleware
  * PrometheusEndpointExtensions
  * EndpointAccessor / HttpContextEndpointAccessor
* **MediatR**

  * PerformanceBehavior
* **MassTransit**

  * PerformanceFilter
* **Background**

  * BackgroundServiceWrapper

### Telemetry

* **TagContext**
* **TagScrubber**

### Sampling

* **SmartSampler**
* **ConcurrentBloomDuplicateGuard**

### Profiling

* **FlamegraphRecorder**
* **FlamegraphManager**

### Dependency Injection

* **AddPerformanceMonitorExtensions**
* **ServiceCollectionExtensions**

### Diagnostics & Logging

* **NorrLogPM** (event IDs, templates)
* **EventSource IDs**

---

## Examples (`/examples/`)

* Minimal API (ASP.NET Core)
* Worker Service
* MediatR CQRS
* MassTransit Consumer
* Slack Alerts
* Custom Exporter

---

## Troubleshooting (`/troubleshooting/`)

* No metrics emitted
* High cardinality warnings
* Scrubber masked my tags too much
* Alerts not delivered / retries exhausted
* Prometheus scrape errors
* CPU metrics always zero
* Tests flake due to retries

---

## Release Notes (`/releases/`)

* Version history and changes

## FAQ (`/faq/`)

* Short, canonical answers

## Glossary (`/glossary/`)

* Common terms used throughout the docs

---

## Appendix

### Suggested page skeleton (template)

* **Title**
* **Summary (2–3 sentences)**
* **Prerequisites**
* **Steps / API / Concept**
* **Examples**
* **Pitfalls & tips**
* **Related links**

### Sidebar ordering tips

* Keep Tutorials short and linear; push details to How‑to/Concepts
* Put Prometheus & Alerts high‑visibility in How‑to
* Keep Reference exhaustive but flat; one page per type/options class

