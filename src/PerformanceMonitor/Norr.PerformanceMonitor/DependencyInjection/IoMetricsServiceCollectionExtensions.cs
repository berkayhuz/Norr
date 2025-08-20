// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable
using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Norr.PerformanceMonitor.Telemetry;

/// <summary>
/// OTel’siz kurulum: Şu anda sadece opsiyonel bir EventListener host etmek isterseniz
/// genişletebilmeniz için bir “hook” sağlar.
/// </summary>
public static class IoMetricsServiceCollectionExtensions
{
    /// <summary>
    /// Norr I/O metriklerini etkinleştirir (OpenTelemetry bağımlılığı olmadan).
    /// İsterseniz burada custom EventListener'ınızı DI'ya ekleyebilirsiniz.
    /// </summary>
    public static IServiceCollection AddNorrIoMetrics(this IServiceCollection services)
    {
        // Örn: Custom EventListener eklemek için:
        // services.AddSingleton<IHostedService, IoMetricsEventListenerHost>();

        // Şimdilik no-op: IoMetricsRecorder zaten EventSource’a yazıyor.
        return services;
    }
}
