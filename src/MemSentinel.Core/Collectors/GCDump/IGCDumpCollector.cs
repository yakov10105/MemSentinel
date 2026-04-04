using MemSentinel.Core.Common;

namespace MemSentinel.Core.Collectors.GCDump;

public interface IGCDumpCollector
{
    Task<Result<string>> CaptureAsync(string outputPath, CancellationToken ct);
}
