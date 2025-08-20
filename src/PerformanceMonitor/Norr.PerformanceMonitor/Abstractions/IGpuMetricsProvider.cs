// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable
namespace Norr.PerformanceMonitor.Abstractions;

public interface IGpuMetricsProvider
{
    /// GPU kullanım yüzdesi (0..100) – ortalama, mevcut anlık değer
    double? GetUtilizationPercent();


    /// Kullanılan/Toplam GPU bellek baytı
    (long? UsedBytes, long? TotalBytes) GetMemoryBytes();
}
