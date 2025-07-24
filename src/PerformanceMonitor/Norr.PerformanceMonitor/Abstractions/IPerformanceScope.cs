namespace Norr.PerformanceMonitor.Abstractions;

/// <summary>
/// A disposable handle that represents a single performance-measurement window.  
/// Created by <see cref="IMonitor.Begin"/> (typically inside a <c>using</c> block);
/// when it is disposed, the duration, CPU time, memory allocations, etc. are
/// finalised and pushed to the configured exporters and alert sinks.
/// </summary>
public interface IPerformanceScope : IDisposable
{
    /* Marker interface – no additional members required */
}
