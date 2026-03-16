namespace MemSentinel.Core.Analysis;

public readonly record struct TriggerResult(
    TriggerReason Reason,
    double CurrentRssMb,
    double LimitMb,
    double RssUsedPercent,
    GrowthVelocity Velocity)
{
    public bool IsTriggered => Reason != TriggerReason.None;

    public static readonly TriggerResult None =
        new(TriggerReason.None, 0, 0, 0, GrowthVelocity.Insufficient);
}
