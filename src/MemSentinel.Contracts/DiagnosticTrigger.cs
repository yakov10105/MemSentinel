namespace MemSentinel.Contracts;

public readonly record struct DiagnosticTrigger(
    TriggerReason Reason,
    DateTimeOffset TriggeredAt,
    double CurrentRssMb,
    double VelocityMbPerMinute);
