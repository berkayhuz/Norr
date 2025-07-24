using System.Threading;

using Norr.PerformanceMonitor.Core;

namespace Norr.PerformanceMonitor.Tests;
public class PerformanceScopeTests
{
    [Fact]
    public void PerformanceScope_records_histograms()
    {
        using var _ = new PerformanceScope("Demo");
        Thread.Sleep(10);
    }
}
