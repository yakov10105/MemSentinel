using MemSentinel.Core.Providers;

namespace MemSentinel.Core.Analysis;

public readonly record struct MetricSample(RssMemoryReading Rss, HeapMetadata Heap);
