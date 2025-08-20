**Norr.PerformanceMonitor** is a library that can be easily integrated into ASP.NET Core applications to collect performance metrics and export them to systems like Prometheus.

## 1. Installation

Install via NuGet:

```bash
dotnet add package Norr.PerformanceMonitor
```

This package includes the core performance measurement infrastructure, metric collectors (`Monitor`, `PerformanceScope`), and basic exporters.

## 2. Program.cs Integration (Minimal Example)

Add the following integration to your `Program.cs` file in your ASP.NET Core application:

```csharp
using Microsoft.AspNetCore.Routing;
using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.DependencyInjection;
using Norr.PerformanceMonitor.Integrations.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPerformanceMonitor();
builder.Services.AddControllers();

var app = builder.Build();

app.UseRouting();
app.UseRouteTagging();
app.UseMiddleware<PerformanceMiddleware>();

app.Use(async (ctx, next) =>
{
    var monitor = ctx.RequestServices.GetRequiredService<IMonitor>();
    var route = (ctx.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
                ?? ctx.Request.Path.Value ?? "/";
    using (monitor.Begin($"http {ctx.Request.Method} {route}"))
        await next();
});

app.MapControllers();
app.UseNorrPrometheusEndpoint("/metrics");

app.Run();
```

With this setup, your application will start exposing metrics in Prometheus format at the `/metrics` endpoint.

## 3. First Run and Metrics Check

Run the application:

```bash
dotnet run
```

Make a request to an endpoint:

```bash
curl https://xxxx:yyyy/orders/42/details
```

Then check the metrics:

```bash
curl https://xxxx:yyyy/metrics
```

Example output:

```text
# TYPE http_GET_orders__id__details summary
http_GET_orders__id__details_count 5
http_GET_orders__id__details_sum 400
```

These values show the total number of calls and duration/memory measurements for the given endpoint.

## 4. Optional Configuration (appsettings.json)

Add `PerformanceOptions` to your `appsettings.json`:

```json
{
  "PerformanceOptions": {
    "Metrics": {
      "Temporality": "Cumulative",
      "EnableDuplicateGuard": true
    },
    "Exporters": {
      "Console": true,
      "Prometheus": true
    },
    "Scrubbing": {
      "MaskSensitiveValues": true,
      "NormalizePaths": true,
      "Mask": "***",
      "DropBannedKeys": false,
      "BannedKeys": [ "user.email", "http.authorization" ]
    }
  }
}
```

And add this to your `Program.cs`:

```csharp
builder.Services.Configure<PerformanceOptions>(
    builder.Configuration.GetSection("PerformanceOptions"));
```

With these settings:

* **Console exporter** is enabled → prints a summary log at startup.
* **Prometheus exporter** remains enabled.
* **Scrubbing** masks sensitive data and normalizes path parameters.

## 5. Conclusion

With these steps, **Norr.PerformanceMonitor** is integrated into your project. The first metrics appear at the `/metrics` endpoint. Optional settings allow you to easily manage exporter and scrubbing behavior.
