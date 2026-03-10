using System.Buffers;
using System.Buffers.Text;

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
            return ParseField(buffer.AsSpan(0, bytesRead), field);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static long ParseField(ReadOnlySpan<byte> content, ReadOnlySpan<byte> field)
    {
        while (!content.IsEmpty)
        {
            var lineEnd = content.IndexOf((byte)'\n');
            var line = lineEnd >= 0 ? content[..lineEnd] : content;

            if (line.StartsWith(field))
            {
                var value = SkipWhitespace(line[field.Length..]);
                var spaceIdx = value.IndexOf((byte)' ');
                var numberSpan = spaceIdx >= 0 ? value[..spaceIdx] : value;

                return Utf8Parser.TryParse(numberSpan, out long result, out _) ? result : 0;
            }

            if (lineEnd < 0) break;
            content = content[(lineEnd + 1)..];
        }

        return 0;
    }

    private static ReadOnlySpan<byte> SkipWhitespace(ReadOnlySpan<byte> span)
    {
        var i = 0;
        while (i < span.Length && (span[i] == ' ' || span[i] == '\t'))
            i++;
        return span[i..];
    }
}
