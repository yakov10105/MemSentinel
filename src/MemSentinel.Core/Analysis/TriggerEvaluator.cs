using MemSentinel.Contracts;
using MemSentinel.Core.Providers;

namespace MemSentinel.Core.Analysis;

public static class TriggerEvaluator
{
    public static TriggerResult Evaluate(
        RssMemoryReading rss,
        GrowthVelocity velocity,
        TriggerThresholds thresholds)
    {
        var currentRssMb = rss.RssBytes / 1_048_576.0;
        var limitMb = thresholds.ContainerMemoryLimitMb;
        var usedPercent = limitMb > 0 ? currentRssMb / limitMb * 100.0 : 0.0;

        var hardTriggered = usedPercent >= thresholds.RssLimitPercentage;
        var velocityTriggered = velocity.SampleCount >= 2
            && velocity.RssMbPerMinute >= thresholds.VelocityThresholdMbPerMinute;

        var reason = (hardTriggered, velocityTriggered) switch
        {
            (true, true) => TriggerReason.Both,
            (true, false) => TriggerReason.HardThreshold,
            (false, true) => TriggerReason.VelocityThreshold,
            _ => TriggerReason.None
        };

        if (reason == TriggerReason.None)
            return TriggerResult.None;

        return new TriggerResult(reason, currentRssMb, limitMb, usedPercent, velocity);
    }
}
