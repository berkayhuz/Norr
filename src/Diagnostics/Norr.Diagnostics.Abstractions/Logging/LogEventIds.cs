// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 
using System;

using Microsoft.Extensions.Logging;

namespace Norr.Diagnostics.Abstractions.Logging;

/// <summary>Norr EventId biçimi: [PKG:NNNN].</summary>
public readonly record struct NorrEventId(string PackageCode, int Code)
{
    public override string ToString() => $"[{PackageCode}:{Code:D4}]";
    public EventId ToEventId() => new(Code, ToString());
}

/// <summary>Seviye -> taban kod eşlemesi.</summary>
public static class NorrEventCode
{
    // İstediğin gibi güncelleyebilirsin:
    public const int TraceBase = 1000;
    public const int DebugBase = 1100;
    public const int InformationBase = 1200;
    public const int WarningBase = 3000;
    public const int ErrorBase = 2000;
    public const int CriticalBase = 2500;

    public static int BaseOf(LogLevel level) => level switch
    {
        LogLevel.Trace => TraceBase,
        LogLevel.Debug => DebugBase,
        LogLevel.Information => InformationBase,
        LogLevel.Warning => WarningBase,
        LogLevel.Error => ErrorBase,
        LogLevel.Critical => CriticalBase,
        _ => InformationBase
    };
}

/// <summary>EventId üretmek için yardımcılar.</summary>
public static class NorrEvent
{
    /// <param name="offset">1..99 (ya da 1..999) arası yerel kod ofseti</param>
    public static NorrEventId Make(string packageCode, LogLevel level, int offset)
        => new(packageCode, NorrEventCode.BaseOf(level) + offset);
}
