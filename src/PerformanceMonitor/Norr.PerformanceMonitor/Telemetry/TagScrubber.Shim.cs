// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable

using Norr.PerformanceMonitor.Configuration;

namespace Norr.PerformanceMonitor.Telemetry;

/// <summary>
/// Eski kodlarda kullanılan TagScrubber.NormalizeAndMask(...) için geri uyumluluk.
/// İçeride mevcut ScrubValue(...)’ı çağırır.
/// </summary>
internal static partial class TagScrubber
{
    /// <summary>
    /// SQL gibi serbest metinlerde e‑posta/JWT maskeler ve uzunluğu sınırlar.
    /// </summary>
    public static string NormalizeAndMask(string input, int maxLength)
        => ScrubValue(input, new ScrubbingOptions
        {
            MaskSensitiveValues = true,
            NormalizePaths = false,   // SQL path değildir
            MaxValueLength = maxLength
        });
}
