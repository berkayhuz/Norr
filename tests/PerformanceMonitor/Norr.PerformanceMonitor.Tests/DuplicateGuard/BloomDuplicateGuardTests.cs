using System;

using FluentAssertions;

using Norr.PerformanceMonitor.Configuration;
using Norr.PerformanceMonitor.Sampling;

namespace Norr.PerformanceMonitor.Tests.DuplicateGuard;
public class BloomDuplicateGuardTests
{
    [Fact]
    public void ShouldEmit_first_time_true_then_false_within_cooldown()
    {
        var guard = new BloomDuplicateGuard(new DuplicateGuardOptions { CoolDown = TimeSpan.FromSeconds(5) });
        var now = DateTime.UtcNow;

        guard.ShouldEmit("foo:Duration", now).Should().BeTrue();
        guard.ShouldEmit("foo:Duration", now.AddSeconds(1)).Should().BeFalse();
    }
}
