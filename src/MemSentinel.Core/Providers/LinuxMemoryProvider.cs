using System.Buffers;
using MemSentinel.Core.Collectors;

namespace MemSentinel.Core.Providers;

public sealed class LinuxMemoryProvider(int pid) : IMemoryProvider
{
    private readonly string _statusPath = $"/proc/{pid}/status";
    private readonly string _smapsPath = $"/proc/{pid}/smaps_rollup";

    public bool IsAvailable => OperatingSystem.IsLinux() && File.Exists(_statusPath);

    public ValueTask<RssMemoryReading> GetRssMemoryAsync(CancellationToken ct)
    {
        var rssKb = ReadProcField(_statusPath, "VmRSS:"u8);
        var vmSizeKb = ReadProcField(_statusPath, "VmSize:"u8);
        var pssKb = File.Exists(_smapsPath) ? ReadProcField(_smapsPath, "Pss:"u8) : 0;

        return ValueTask.FromResult(new RssMemoryReading(
            RssBytes: rssKb * 1024,
            PssBytes: pssKb * 1024,
            VmSizeBytes: vmSizeKb * 1024,
            CapturedAt: DateTimeOffset.UtcNow));
    }

    public ValueTask<HeapMetadata> GetHeapMetadataAsync(CancellationToken ct) =>
        ValueTask.FromResult(new HeapMetadata(0, 0, 0, 0, 0, DateTimeOffset.UtcNow));

    private static long ReadProcField(string path, ReadOnlySpan<byte> field)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bytesRead = RandomAccess.Read(handle, buffer, fileOffset: 0);
            return ProcFileParser.ParseField(buffer.AsSpan(0, bytesRead), field);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
