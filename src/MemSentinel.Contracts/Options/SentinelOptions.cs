namespace MemSentinel.Contracts.Options;

public sealed class ThresholdOptions
{
    public double RssLimitPercentage { get; init; } = 80.0;
    public double Gen2GrowthLimitMb { get; init; } = 100.0;
}

public sealed class SentinelOptions
{
    public const string SectionName = "Sentinel";

    public string TargetProcessName { get; init; } = "dotnet";
    public int PollingIntervalSeconds { get; init; } = 5;
    public int CoolingPeriodMinutes { get; init; } = 3;
    public string StorageProvider { get; init; } = "Local";
    public ThresholdOptions Thresholds { get; init; } = new();
}
