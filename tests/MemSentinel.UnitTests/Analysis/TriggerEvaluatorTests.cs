using FluentAssertions;
using MemSentinel.Core.Analysis;
using MemSentinel.Core.Providers;

namespace MemSentinel.UnitTests.Analysis;

public sealed class TriggerEvaluatorTests
{
    private static RssMemoryReading Rss(double mb) =>
        new((long)(mb * 1_048_576), 0, 0, DateTimeOffset.UtcNow);

    private static GrowthVelocity Velocity(double rssMbPerMin, int samples = 10) =>
        new(rssMbPerMin, 0, TimeSpan.FromMinutes(5), samples, rssMbPerMin > 0);

    private static TriggerThresholds Thresholds(
        double limitMb = 512.0,
        double rssPercent = 80.0,
        double velocityMbPerMin = 5.0) =>
        new(limitMb, rssPercent, velocityMbPerMin);

    [Fact]
    public void Evaluate_ReturnsNone_WhenBothThresholdsBelowLimit()
    {
        var result = TriggerEvaluator.Evaluate(Rss(100), Velocity(1.0), Thresholds());

        result.IsTriggered.Should().BeFalse();
        result.Reason.Should().Be(TriggerReason.None);
    }

    [Fact]
    public void Evaluate_ReturnsHardThreshold_WhenRssExceedsLimit()
    {
        // 450MB of 512MB = 87.9% > 80% threshold
        var result = TriggerEvaluator.Evaluate(Rss(450), Velocity(1.0), Thresholds());

        result.IsTriggered.Should().BeTrue();
        result.Reason.Should().Be(TriggerReason.HardThreshold);
        result.CurrentRssMb.Should().BeApproximately(450.0, 0.1);
        result.LimitMb.Should().Be(512.0);
        result.RssUsedPercent.Should().BeApproximately(87.89, 0.1);
    }

    [Fact]
    public void Evaluate_DoesNotTriggerHard_WhenRssBelowLimit()
    {
        // 400MB of 512MB = 78.1% < 80% threshold
        var result = TriggerEvaluator.Evaluate(Rss(400), Velocity(1.0), Thresholds());

        result.Reason.Should().NotBe(TriggerReason.HardThreshold);
        result.Reason.Should().NotBe(TriggerReason.Both);
    }

    [Fact]
    public void Evaluate_ReturnsVelocityThreshold_WhenRateExceedsLimit()
    {
        var result = TriggerEvaluator.Evaluate(Rss(100), Velocity(10.0), Thresholds());

        result.IsTriggered.Should().BeTrue();
        result.Reason.Should().Be(TriggerReason.VelocityThreshold);
        result.Velocity.RssMbPerMinute.Should().Be(10.0);
    }

    [Fact]
    public void Evaluate_DoesNotTriggerVelocity_WhenFewerThanTwoSamples()
    {
        var insufficientVelocity = new GrowthVelocity(10.0, 0, TimeSpan.FromMinutes(1), 1, true);

        var result = TriggerEvaluator.Evaluate(Rss(100), insufficientVelocity, Thresholds());

        result.Reason.Should().NotBe(TriggerReason.VelocityThreshold);
        result.Reason.Should().NotBe(TriggerReason.Both);
    }

    [Fact]
    public void Evaluate_DoesNotTriggerVelocity_WhenRateBelowThreshold()
    {
        var result = TriggerEvaluator.Evaluate(Rss(100), Velocity(4.9), Thresholds());

        result.Reason.Should().NotBe(TriggerReason.VelocityThreshold);
        result.Reason.Should().NotBe(TriggerReason.Both);
    }

    [Fact]
    public void Evaluate_ReturnsBoth_WhenBothThresholdsExceeded()
    {
        // 450MB of 512MB = 87.9% > 80% AND velocity 10 MB/min > 5 MB/min
        var result = TriggerEvaluator.Evaluate(Rss(450), Velocity(10.0), Thresholds());

        result.IsTriggered.Should().BeTrue();
        result.Reason.Should().Be(TriggerReason.Both);
    }

    [Fact]
    public void Evaluate_NoneResult_IsNotTriggered()
    {
        TriggerResult.None.IsTriggered.Should().BeFalse();
        TriggerResult.None.Reason.Should().Be(TriggerReason.None);
    }

    [Fact]
    public void Evaluate_ReturnsNone_WhenVelocityIsInsufficient()
    {
        var result = TriggerEvaluator.Evaluate(Rss(100), GrowthVelocity.Insufficient, Thresholds());

        result.Reason.Should().NotBe(TriggerReason.VelocityThreshold);
    }

    [Fact]
    public void Evaluate_HardThresholdAtBoundary_Triggers()
    {
        // 410MB of 512MB = 80.08% >= 80% threshold
        var result = TriggerEvaluator.Evaluate(Rss(410), Velocity(1.0), Thresholds());

        result.Reason.Should().Be(TriggerReason.HardThreshold);
    }
}
