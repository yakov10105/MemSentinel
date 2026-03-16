namespace MemSentinel.Core.Analysis;

public readonly record struct TriggerThresholds(
    double ContainerMemoryLimitMb,
    double RssLimitPercentage,
    double VelocityThresholdMbPerMinute);
