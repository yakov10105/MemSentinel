namespace MemSentinel.Core.Analysis;

public static class MemoryGrowthAnalyzer
{
    public static GrowthVelocity Calculate(IReadOnlyList<MetricSample> samples)
    {
        if (samples.Count < 2)
            return GrowthVelocity.Insufficient;

        var oldest = samples[0];
        var newest = samples[^1];
        var windowMinutes = (newest.Rss.CapturedAt - oldest.Rss.CapturedAt).TotalMinutes;

        if (windowMinutes <= 0)
            return GrowthVelocity.Insufficient;

        var rssDeltaBytes = newest.Rss.RssBytes - oldest.Rss.RssBytes;
        var rssMbPerMinute = rssDeltaBytes / 1_048_576.0 / windowMinutes;

        var gen2Delta = newest.Heap.Gen2Bytes - oldest.Heap.Gen2Bytes;
        var lohDelta = newest.Heap.LohBytes - oldest.Heap.LohBytes;
        var managedLeakMbPerMinute = (gen2Delta + lohDelta) / 1_048_576.0 / windowMinutes;

        return new GrowthVelocity(
            RssMbPerMinute: rssMbPerMinute,
            ManagedLeakMbPerMinute: managedLeakMbPerMinute,
            WindowDuration: newest.Rss.CapturedAt - oldest.Rss.CapturedAt,
            SampleCount: samples.Count,
            IsLeakSuspected: gen2Delta + lohDelta > 0);
    }
}
