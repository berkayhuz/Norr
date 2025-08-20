// Copyright (c) Norr
// Licensed under the MIT license.

namespace Norr.PerformanceMonitor.Configuration;

public sealed class RuntimeCountersOptions
{
    /// System.Runtime dinleyicisini aç/kapat
    public bool Enabled { get; set; } = true;


    /// EventCounter update interval (saniye)
    public int RefreshIntervalSeconds { get; set; } = 1;
}
