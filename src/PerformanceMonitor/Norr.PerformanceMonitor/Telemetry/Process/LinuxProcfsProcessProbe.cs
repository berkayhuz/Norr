// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices;

using Norr.PerformanceMonitor.Abstractions;

namespace Norr.PerformanceMonitor.Telemetry.Process;

internal sealed class LinuxProcfsProcessProbe : IProcessProbe
{
    private const string FdDir = "/proc/self/fd";
    private const string StatusFile = "/proc/self/status";
    private const string SocketPrefix = "socket:[";

    public int? GetSocketCount()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return null;

        try
        {
            var count = 0;
            foreach (var entry in Directory.EnumerateFileSystemEntries(FdDir))
            {
                string? target;
                try
                {
                    // .NET 8/9: sembolik link hedef metni
                    target = new FileInfo(entry).LinkTarget;
                }
                catch
                {
                    // fd bu arada kapanmış olabilir
                    continue;
                }

                if (!string.IsNullOrEmpty(target) &&
                    target.StartsWith(SocketPrefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }
        catch
        {
            return null;
        }
    }

    public int? GetOpenFileDescriptorCount()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return null;

        try
        {
            var count = 0;
            using var e = Directory.EnumerateFileSystemEntries(FdDir).GetEnumerator();
            while (e.MoveNext())
                count++;
            return count;
        }
        catch
        {
            return null;
        }
    }

    public (long? Voluntary, long? NonVoluntary) GetContextSwitchTotals()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return (null, null);

        try
        {
            long? vol = null, nvol = null;
            foreach (var line in File.ReadLines(StatusFile))
            {
                if (line.StartsWith("voluntary_ctxt_switches:", StringComparison.Ordinal))
                    vol = ParseTailLong(line);
                else if (line.StartsWith("nonvoluntary_ctxt_switches:", StringComparison.Ordinal))
                    nvol = ParseTailLong(line);

                if (vol is not null && nvol is not null)
                    break;
            }
            return (vol, nvol);
        }
        catch
        {
            return (null, null);
        }
    }

    private static long? ParseTailLong(string line)
    {
        var i = line.Length - 1;
        while (i >= 0 && !char.IsDigit(line[i]))
            i--;
        if (i < 0)
            return null;

        var end = i + 1;
        while (i >= 0 && char.IsDigit(line[i]))
            i--;
        var span = line.AsSpan(i + 1, end - (i + 1));
        return long.TryParse(span, out var v) ? v : null;
    }
}
