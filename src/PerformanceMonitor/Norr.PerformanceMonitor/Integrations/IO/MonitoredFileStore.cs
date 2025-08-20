#nullable enable
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Telemetry;

namespace Norr.PerformanceMonitor.Integrations.IO;

public sealed class MonitoredFileStore : IFileStore
{
    private readonly IMonitor _monitor;

    public MonitoredFileStore(IMonitor monitor) => _monitor = monitor;

    public async Task WriteAsync(string path, byte[] data, CancellationToken ct = default)
    {
        using var scope = _monitor.Begin("file.write");
        using var _ctx = TagContext.Begin(new[]
        {
            new KeyValuePair<string, object?>("file.op",   "write"),
            new KeyValuePair<string, object?>("file.name", Path.GetFileName(path)),
            new KeyValuePair<string, object?>("file.ext",  Path.GetExtension(path)),
        });

        using var _tag = TagContext.Begin("io.request.bytes", data.LongLength);

        IoMetricsRecorder.RecordRequest(data.LongLength, new TagList
        {
            { "file.op", "write" },
            { "file.ext", Path.GetExtension(path) ?? "" }
        });

        await File.WriteAllBytesAsync(path, data, ct).ConfigureAwait(false);
    }

    public async Task<byte[]> ReadAsync(string path, CancellationToken ct = default)
    {
        using var scope = _monitor.Begin("file.read");
        using var _ctx = TagContext.Begin(new[]
        {
            new KeyValuePair<string, object?>("file.op",   "read"),
            new KeyValuePair<string, object?>("file.name", Path.GetFileName(path)),
            new KeyValuePair<string, object?>("file.ext",  Path.GetExtension(path)),
        });

        var bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);

        using var _tag = TagContext.Begin("io.response.bytes", (long)bytes.LongLength);

        IoMetricsRecorder.RecordResponse(bytes.LongLength, new TagList
        {
            { "file.op", "read" },
            { "file.ext", Path.GetExtension(path) ?? "" }
        });

        return bytes;
    }
}
