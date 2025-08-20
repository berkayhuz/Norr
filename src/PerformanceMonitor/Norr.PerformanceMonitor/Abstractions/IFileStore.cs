// Copyright (c) Norr
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Norr;
using Norr.PerformanceMonitor;
using Norr.PerformanceMonitor.Integrations;

namespace Norr.PerformanceMonitor.Abstractions;

public interface IFileStore
{
    Task WriteAsync(string path, byte[] data, CancellationToken ct = default);
    Task<byte[]> ReadAsync(string path, CancellationToken ct = default);
}
