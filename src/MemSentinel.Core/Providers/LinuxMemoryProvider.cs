namespace MemSentinel.Core.Providers;

public sealed class LinuxMemoryProvider(int pid) : IMemoryProvider
{
    private readonly string _statusPath = $"/proc/{pid}/status";
    private readonly string _smapsPath = $"/proc/{pid}/smaps_rollup";

    public bool IsAvailable => OperatingSystem.IsLinux() && File.Exists(_statusPath);

    public ValueTask<RssMemoryReading> GetRssMemoryAsync(CancellationToken ct)
    {
        var rssKb = ParseStatusField(_statusPath, "VmRSS:");
        var vmSizeKb = ParseStatusField(_statusPath, "VmSize:");
        var pssKb = ParseSmapsField(_smapsPath, "Pss:");

        return ValueTask.FromResult(new RssMemoryReading(
            RssBytes: rssKb * 1024,
            PssBytes: pssKb * 1024,
            VmSizeBytes: vmSizeKb * 1024,
            CapturedAt: DateTimeOffset.UtcNow));
    }

    public ValueTask<HeapMetadata> GetHeapMetadataAsync(CancellationToken ct) =>
        ValueTask.FromResult(new HeapMetadata(0, 0, 0, 0, 0, DateTimeOffset.UtcNow));

    private static long ParseStatusField(string path, string field)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (!line.StartsWith(field, StringComparison.Ordinal))
                continue;

            var span = line.AsSpan(field.Length).TrimStart();
            var spaceIndex = span.IndexOf(' ');
            var numberSpan = spaceIndex > 0 ? span[..spaceIndex] : span;

            return long.TryParse(numberSpan, out var value) ? value : 0;
        }

        return 0;
    }

    private static long ParseSmapsField(string path, string field)
    {
        if (!File.Exists(path))
            return 0;

        return ParseStatusField(path, field);
    }
}
