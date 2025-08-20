// Copyright (c) Norr
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Norr.PerformanceMonitor.Configuration;
public sealed class HttpClientMonitorOptions
{
    /// <summary>İçerikleri bayt olarak ölçmek pahalıdır; varsayılan kapalı.</summary>
    public bool CaptureRequestAndResponseBytes { get; set; } = false;

    /// <summary>İçerik uzunluğu yoksa en fazla kaç bayta kadar buffer’lanacak.</summary>
    public int ResponseProbeMaxBytes { get; set; } = 64 * 1024; // 64 KB
}
