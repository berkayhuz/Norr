﻿using Microsoft.Extensions.Logging;

namespace Norr.PerformanceMonitor.Profiling;

/// <summary>
/// Facade that starts and stops an in-process <see cref="FlamegraphRecorder"/>
/// session and logs the generated <c>.speedscope.json</c> file.  
/// Typically exposed via minimal-API endpoints:
///
/// ```csharp
/// app.MapPost("/flame/start", (FlamegraphManager m) => { m.Start(); return Results.Ok(); });
/// app.MapPost("/flame/stop",  async (FlamegraphManager m) =>
/// {
///     var file = await m.StopAsync();
///     return file == string.Empty
///         ? Results.BadRequest("No active recording.")
///         : Results.File(file, "application/json");
/// });
/// ```
/// </summary>
public sealed class FlamegraphManager
{
    private FlamegraphRecorder? _rec;
    private readonly ILogger<FlamegraphManager> _log;

    /// <summary>
    /// DI-friendly constructor.
    /// </summary>
    public FlamegraphManager(ILogger<FlamegraphManager> log) => _log = log;

    /// <summary>
    /// Begins a new flamegraph recording.  
    /// If a session is already active it is first disposed (and its file flushed).
    /// </summary>
    public void Start()
    {
        _rec?.DisposeAsync().AsTask().Wait();

        var fileName = $"flame_{DateTime.UtcNow:yyyyMMdd_HHmmss}.speedscope.json";
        var fullPath = Path.Combine(AppContext.BaseDirectory, fileName);

        _rec = FlamegraphRecorder.Start(fullPath);
        _log.LogInformation("Flamegraph recording started → {File}", fileName);
    }

    /// <summary>
    /// Stops the current recording (if any) and returns the path of the generated
    /// SpeedScope JSON file.  
    /// When no recording is active an empty string is returned.
    /// </summary>
    public async Task<string> StopAsync()
    {
        if (_rec is null)
            return string.Empty;

        await _rec.DisposeAsync();
        var path = _rec.OutputPath;
        _rec = null;

        _log.LogInformation("Flamegraph saved → {File}", path);
        return path;
    }
}
