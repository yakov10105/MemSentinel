namespace MemSentinel.Core.Providers;

public sealed class MockMemoryOptions
{
    public long BaseRssBytes { get; init; } = 150 * 1024 * 1024;
    public long GrowthPerCallBytes { get; init; } = 512 * 1024;
    public long BaseGen2Bytes { get; init; } = 80 * 1024 * 1024;
}

public sealed class MockMemoryProvider(MockMemoryOptions? options = null) : IMemoryProvider
{
    private readonly MockMemoryOptions _options = options ?? new MockMemoryOptions();
    private long _callCount;

    public bool IsAvailable => true;

    public ValueTask<RssMemoryReading> GetRssMemoryAsync(CancellationToken ct)
    {
        var count = Interlocked.Increment(ref _callCount);
        var rss = _options.BaseRssBytes + count * _options.GrowthPerCallBytes;

        return ValueTask.FromResult(new RssMemoryReading(
            RssBytes: rss,
            PssBytes: (long)(rss * 0.9),
            VmSizeBytes: rss * 2,
            CapturedAt: DateTimeOffset.UtcNow));
    }

    public ValueTask<HeapMetadata> GetHeapMetadataAsync(CancellationToken ct)
    {
        var count = Interlocked.Read(ref _callCount);
        var gen2 = _options.BaseGen2Bytes + count * (_options.GrowthPerCallBytes / 2);

        return ValueTask.FromResult(new HeapMetadata(
            Gen0Bytes: 4 * 1024 * 1024,
            Gen1Bytes: 12 * 1024 * 1024,
            Gen2Bytes: gen2,
            LohBytes: 8 * 1024 * 1024,
            PohBytes: 2 * 1024 * 1024,
            CapturedAt: DateTimeOffset.UtcNow));
    }
}
