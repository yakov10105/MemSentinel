using MemSentinel.Core.Providers;

namespace MemSentinel.Core.Collectors.GCMetrics;

public interface IGCMetricsProvider
{
    ValueTask<HeapMetadata> GetAsync(CancellationToken ct);
}
