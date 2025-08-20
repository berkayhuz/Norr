// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable
namespace Norr.PerformanceMonitor.Abstractions;


public interface ISyscallMetricsProvider
{
    /// Son örnekten beri syscall sayımı (mümkünse). Sağlayıcı desteklemiyorsa null.
    long? GetSyscallDelta();
}
