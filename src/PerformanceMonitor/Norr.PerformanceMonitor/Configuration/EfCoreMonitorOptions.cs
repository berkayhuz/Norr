// Copyright (c) Norr
// Licensed under the MIT license.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Norr.PerformanceMonitor.Configuration;
public sealed class EfCoreMonitorOptions
{
    /// <summary>SQL/parametre scrubbing’i aç/kapat.</summary>
    public bool ScrubSql { get; set; } = true;

    /// <summary>Uzun SQL’i kısalt (PII ve kardinalite kontrolü).</summary>
    public int MaxSqlLength { get; set; } = 1024;
}
