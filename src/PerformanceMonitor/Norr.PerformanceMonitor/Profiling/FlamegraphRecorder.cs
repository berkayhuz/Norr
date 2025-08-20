// Copyright (c) Norr
// Licensed under the MIT license.

#nullable enable 

using System.Diagnostics.Tracing;

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.Diagnostics.Tracing.Stacks.Formats;

namespace Norr.PerformanceMonitor.Profiling;

/// <summary>
/// Captures an in-process EventPipe sampling profile (SampleProfiler) and
/// converts the trace to a <c>.speedscope.json</c> file that can be opened in
/// <see href="https://www.speedscope.app/">speedscope.app</see> or Grafana’s
/// flamegraph panel.
///
/// <para><strong>Typical usage</strong></para>
/// <code>
/// await using (var rec = FlamegraphRecorder.Start("profile.speedscope.json"))
/// {
///     RunHotPath();
/// }
/// // JSON is now ready
/// </code>
/// </summary>
public sealed class FlamegraphRecorder : IAsyncDisposable
{
    private readonly DiagnosticsClient _client;
    private readonly EventPipeSession _session;
    private readonly string _tracePath;
    private readonly string _outputJson;
    private readonly Task _copyTask;

    /// <summary>
    /// Path of the SpeedScope JSON file produced after <see cref="DisposeAsync"/>
    /// completes.
    /// </summary>
    public string OutputPath => _outputJson;

    private FlamegraphRecorder(
        DiagnosticsClient client,
        EventPipeSession session,
        string tracePath,
        string outputJson,
        Task copyTask)
    {
        _client = client;
        _session = session;
        _tracePath = tracePath;
        _outputJson = outputJson;
        _copyTask = copyTask;
    }

    /// <summary>
    /// Begins recording a sampling profile of the current process.
    /// </summary>
    /// <param name="outputSpeedscopeJson">
    /// Destination file (usually ends with <c>.speedscope.json</c>).
    /// </param>
    /// <param name="sampleHz">
    /// Sampling frequency in Hertz; the default 1000 Hz works well for most
    /// scenarios.
    /// </param>
    public static FlamegraphRecorder Start(string outputSpeedscopeJson, int sampleHz = 1000)
    {
        var client = new DiagnosticsClient(Environment.ProcessId);

        var providers = new[]
        {
            new EventPipeProvider(
                "Microsoft-DotNETCore-SampleProfiler",
                EventLevel.Informational)
        };

        // Use a 256-MB circular buffer; rundown disabled for speed.
        var session = client.StartEventPipeSession(
            providers,
            requestRundown: false,
            circularBufferMB: 256);

        var tmpTrace = Path.ChangeExtension(Path.GetTempFileName(), ".nettrace");

        // Background copy of the EventPipe stream into the .nettrace file.
        var copyTask = Task.Run(async () =>
        {
            await using var fs = File.Create(tmpTrace);
            await session.EventStream.CopyToAsync(fs);
        });

        return new FlamegraphRecorder(client, session, tmpTrace, outputSpeedscopeJson, copyTask);
    }

    /// <summary>
    /// Stops recording, waits for the trace buffer to flush, converts the
    /// <c>.nettrace</c> file to SpeedScope JSON and disposes resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _session.Stop();
        await _copyTask; // ensure .nettrace is complete

        // ---- .nettrace → speedscope.json -----------------------------------
        var etlxPath = TraceLog.CreateFromEventPipeDataFile(
            _tracePath, Environment.MachineName);

        using var log = TraceLog.OpenOrConvert(etlxPath);

        var stackSource = new TraceEventStackSource(log.Events);
        SpeedScopeStackSourceWriter.WriteStackViewAsJson(stackSource, _outputJson);
    }
}
