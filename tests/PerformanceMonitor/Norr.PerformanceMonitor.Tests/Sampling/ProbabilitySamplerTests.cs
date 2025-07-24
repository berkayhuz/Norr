using System.Linq;

using FluentAssertions;

using Norr.PerformanceMonitor.Configuration;
using Norr.PerformanceMonitor.Sampling;

namespace Norr.PerformanceMonitor.Tests.Sampling;
public class ProbabilitySamplerTests
{
    [Theory]
    [InlineData(1.0, 10)]
    [InlineData(0.0, 0)]
    public void ShouldSample_probability_behaviour(double prob, int expectedTrue)
    {
        var sampler = new ProbabilitySampler(new SamplingOptions { Probability = prob });
        var result = Enumerable.Range(0, 10).Count(_ => sampler.ShouldSample("A"));

        result.Should().Be(expectedTrue);
    }
    [Fact]
    public void ShouldSample_same_seed_deterministic()
    {
        var opt = new SamplingOptions { Probability = .3, Seed = 42 };
        var s1 = new ProbabilitySampler(opt);
        var s2 = new ProbabilitySampler(opt);

        var seq1 = Enumerable.Range(0, 100).Select(i => s1.ShouldSample($"X{i}"));
        var seq2 = Enumerable.Range(0, 100).Select(i => s2.ShouldSample($"X{i}"));

        seq1.Should().Equal(seq2);
    }
}
