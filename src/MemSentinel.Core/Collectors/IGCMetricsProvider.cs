using MemSentinel.Core.Providers;

namespace MemSentinel.Core.Collectors;

public interface IGCMetricsProvider
{
    ValueTask<HeapMetadata> GetAsync(CancellationToken ct);
}
