using Norr.PerformanceMonitor.Abstractions;
using Norr.PerformanceMonitor.Core.Metrics;

namespace Norr.PerformanceMonitor.Exporters;

/// <summary>
/// Stores all exported <see cref="Metric"/> instances in an in-memory buffer.
/// Intended primarily for unit- and integration-tests where you want to assert
/// that a specific metric was emitted without relying on external systems.
/// </summary>
public sealed class InMemoryExporter : IMetricExporter
{
    private readonly List<Metric> _buffer = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public void Export(in Metric metric)
    {
        lock (_lock)
        {
            _buffer.Add(metric);
        }
    }

    /// <summary>
    /// Returns an immutable snapshot of the metrics recorded so far.
    /// </summary>
    public IReadOnlyList<Metric> Snapshot()
    {
        lock (_lock)
        {
            return _buffer.ToArray();
        }
    }

    /// <summary>
    /// Clears the internal buffer—handy between test cases.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _buffer.Clear();
        }
    }
}
