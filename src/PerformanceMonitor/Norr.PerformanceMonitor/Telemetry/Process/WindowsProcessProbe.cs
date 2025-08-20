// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable
using System.Runtime.InteropServices;

using Norr.PerformanceMonitor.Abstractions;


namespace Norr.PerformanceMonitor.Telemetry.Process;
internal sealed class WindowsProcessProbe : IProcessProbe
{
    public int? GetSocketCount() => null; // TODO: IP Helper API ile genişletilebilir
    public int? GetOpenFileDescriptorCount() => null; // TODO: NtQueryObject/Handle Snapshot
    public (long? Voluntary, long? NonVoluntary) GetContextSwitchTotals() => (null, null); // TODO: ETW/perf
}
