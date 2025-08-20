using System.Text.Json;
using System.Text.Json.Serialization;

using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Core.Metrics;
using Norr.PerformanceMonitor.Exporters.Core;

namespace Norr.PerformanceMonitor.Exporters.Json;

public sealed class JsonLinesFileMetricExporter : IMetricExporter, IAsyncDisposable, IDisposable
{
    private readonly StreamWriter _writer;
    private readonly JsonSerializerOptions _json;
    private readonly object _lock = new();

    public string FilePath
    {
        get;
    }

    private static StreamWriter CreateWriter(string path, bool append)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var fs = new FileStream(
            path,
            append ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous);

        return new StreamWriter(fs) { AutoFlush = false, NewLine = "\n" };
    }

    public JsonLinesFileMetricExporter(string path) : this(path, append: true) { }

    public JsonLinesFileMetricExporter(string path, bool append)
    {
        FilePath = path;
        _writer = CreateWriter(path, append);
        _json = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() }, WriteIndented = false };
    }

    public JsonLinesFileMetricExporter(string path, int capacity, int maxBatchSize, DropPolicy dropPolicy)
        : this(path, append: true) { }

    public JsonLinesFileMetricExporter(string path, int capacity, int maxBatchSize, DropPolicy dropPolicy, bool append)
        : this(path, append) { }

    public void Export(in Metric metric)
    {
        var line = JsonSerializer.Serialize(new
        {
            ts = metric.TimestampUtc,
            name = metric.Name,
            kind = metric.Kind.ToString(),
            value = metric.Value
        }, _json);

        lock (_lock)
        {
            _writer.WriteLine(line);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            _writer.Flush();
        }
        await _writer.DisposeAsync();
    }

    public void Dispose() => _writer.Dispose();
}
