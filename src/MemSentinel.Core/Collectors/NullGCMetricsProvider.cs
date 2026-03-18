using MemSentinel.Core.Providers;

namespace MemSentinel.Core.Collectors;

public sealed class NullGCMetricsProvider : IGCMetricsProvider
{
    public ValueTask<HeapMetadata> GetAsync(CancellationToken ct) =>
        ValueTask.FromResult(new HeapMetadata(0, 0, 0, 0, 0, DateTimeOffset.UtcNow));
}
