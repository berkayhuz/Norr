// Copyright (c) Norr
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Norr.PerformanceMonitor.Exporters.Core;

namespace Norr.PerformanceMonitor.Configuration;

public sealed class JsonLinesExporterOptions
{
    public string Path { get; set; } = "logs/metrics.ndjson";
    public bool Append { get; set; } = true;
    public int Capacity { get; set; } = 8192;
    public int MaxBatchSize { get; set; } = 256;
    public DropPolicy DropPolicy { get; set; } = DropPolicy.DropOldest;
}
