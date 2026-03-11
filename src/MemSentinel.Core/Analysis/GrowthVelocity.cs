namespace MemSentinel.Core.Analysis;

public readonly record struct GrowthVelocity(
    double RssMbPerMinute,
    double ManagedLeakMbPerMinute,
    TimeSpan WindowDuration,
    int SampleCount,
    bool IsLeakSuspected)
{
    public static readonly GrowthVelocity Insufficient =
        new(0, 0, TimeSpan.Zero, 0, false);
}
