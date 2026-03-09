namespace MemSentinel.Core.Providers;

public interface IMemoryProvider
{
    bool IsAvailable { get; }
    ValueTask<RssMemoryReading> GetRssMemoryAsync(CancellationToken ct);
    ValueTask<HeapMetadata> GetHeapMetadataAsync(CancellationToken ct);
}
