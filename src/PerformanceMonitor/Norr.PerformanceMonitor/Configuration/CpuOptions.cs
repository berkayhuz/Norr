// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

namespace Norr.PerformanceMonitor.Configuration;

/// <summary>CPU ölçümüne ilişkin mod ve davranışlar.</summary>
public enum CpuMeasureMode
{
    /// <summary>
    /// Thread bazlı kesin ölçüm. Windows: GetThreadTimes; Linux/macOS: clock_gettime(CLOCK_THREAD_CPUTIME_ID).
    /// Destek yoksa ölçüm kapatılır.
    /// </summary>
    ThreadTime = 0,

    /// <summary>
    /// Yaklaşık ölçüm. Process.TotalProcessorTime farkını kullanır.
    /// Aynı anda birden fazla scope varsa değer paylaştırılmaya çalışılır (yaklaşık).
    /// </summary>
    ProcessApproximate = 1,

    /// <summary>CPU metriğini tamamen devre dışı bırak.</summary>
    Disabled = 2
}

/// <summary>CPU ölçümüne ait tüm ayarlar.</summary>
public sealed class CpuOptions
{
    /// <summary>Ölçüm modu. Varsayılan: <see cref="CpuMeasureMode.ThreadTime"/>.</summary>
    public CpuMeasureMode Mode { get; set; } = CpuMeasureMode.ThreadTime;

    /// <summary>
    /// Windows’ta destekleniyorsa thread cycle sayısını da yayınla (zamana çevrilmez).
    /// Metric adı: <c>method.cpu.cycles</c>
    /// </summary>
    public bool RecordCycles { get; set; } = false;

    /// <summary>
    /// <c>cpu_ms / elapsed_ms * 100</c> yüzdesini de yayınla.
    /// Metric adı: <c>method.cpu.pct</c>
    /// </summary>
    public bool RecordPercentOfElapsed { get; set; } = true;

    /// <summary>
    /// Yüzdeyi çekirdek sayısına normalize et (çok çekirdekte yoğun işleri okumak için).
    /// <c>(cpu_ms / (elapsed_ms * Environment.ProcessorCount)) * 100</c>
    /// Metric adı: <c>method.cpu.pct_norm</c>
    /// </summary>
    public bool RecordPercentNormalizedToCores { get; set; } = false;
}
