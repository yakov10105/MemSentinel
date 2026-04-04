namespace MemSentinel.Contracts;

public readonly record struct HeapDiffReport(
    DateTimeOffset AnalyzedAt,
    IReadOnlyList<TypeDelta> TopGrowingTypes,
    long TotalObjectDelta,
    long TotalBytesDelta,
    double LohFreePercent);
