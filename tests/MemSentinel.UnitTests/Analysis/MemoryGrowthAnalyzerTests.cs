using FluentAssertions;
using MemSentinel.Core.Analysis;
using MemSentinel.Core.Providers;

namespace MemSentinel.UnitTests.Analysis;

public sealed class MemoryGrowthAnalyzerTests
{
    private static MetricSample Sample(long rssBytes, long gen2Bytes = 0, long lohBytes = 0, DateTimeOffset? at = null) =>
        new(
            Rss: new RssMemoryReading(rssBytes, 0, 0, at ?? DateTimeOffset.UtcNow),
            Heap: new HeapMetadata(0, 0, gen2Bytes, lohBytes, 0, DateTimeOffset.UtcNow));

    [Fact]
    public void Calculate_ReturnsInsufficient_WhenFewerThanTwoSamples()
    {
        var result = MemoryGrowthAnalyzer.Calculate([]);
        result.Should().Be(GrowthVelocity.Insufficient);

        var single = MemoryGrowthAnalyzer.Calculate([Sample(100 * 1024 * 1024)]);
        single.Should().Be(GrowthVelocity.Insufficient);
    }

    [Fact]
    public void Calculate_ComputesCorrectRssVelocity()
    {
        var t0 = DateTimeOffset.UtcNow;
        var t1 = t0.AddMinutes(10);

        var samples = new List<MetricSample>
        {
            Sample(100 * 1024 * 1024, at: t0),
            Sample(200 * 1024 * 1024, at: t1)
        };

        var result = MemoryGrowthAnalyzer.Calculate(samples);

        result.RssMbPerMinute.Should().BeApproximately(10.0, precision: 0.001);
        result.SampleCount.Should().Be(2);
        result.WindowDuration.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void Calculate_SetsLeakSuspected_WhenGen2IsGrowing()
    {
        var t0 = DateTimeOffset.UtcNow;
        var t1 = t0.AddMinutes(5);

        var samples = new List<MetricSample>
        {
            Sample(100 * 1024 * 1024, gen2Bytes: 50 * 1024 * 1024, at: t0),
            Sample(120 * 1024 * 1024, gen2Bytes: 80 * 1024 * 1024, at: t1)
        };

        var result = MemoryGrowthAnalyzer.Calculate(samples);

        result.IsLeakSuspected.Should().BeTrue();
        result.ManagedLeakMbPerMinute.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calculate_DoesNotSuspectLeak_WhenOnlyGen0IsGrowing()
    {
        var t0 = DateTimeOffset.UtcNow;
        var t1 = t0.AddMinutes(5);

        var samples = new List<MetricSample>
        {
            Sample(100 * 1024 * 1024, gen2Bytes: 50 * 1024 * 1024, lohBytes: 10 * 1024 * 1024, at: t0),
            Sample(110 * 1024 * 1024, gen2Bytes: 50 * 1024 * 1024, lohBytes: 10 * 1024 * 1024, at: t1)
        };

        var result = MemoryGrowthAnalyzer.Calculate(samples);

        result.IsLeakSuspected.Should().BeFalse();
        result.ManagedLeakMbPerMinute.Should().BeApproximately(0, precision: 0.001);
    }

    [Fact]
    public void Calculate_SetsLeakSuspected_WhenLohIsGrowing()
    {
        var t0 = DateTimeOffset.UtcNow;
        var t1 = t0.AddMinutes(5);

        var samples = new List<MetricSample>
        {
            Sample(100 * 1024 * 1024, lohBytes: 20 * 1024 * 1024, at: t0),
            Sample(130 * 1024 * 1024, lohBytes: 50 * 1024 * 1024, at: t1)
        };

        var result = MemoryGrowthAnalyzer.Calculate(samples);

        result.IsLeakSuspected.Should().BeTrue();
    }
}
