using Microsoft.Extensions.Logging;

namespace MemSentinel.Core.Analysis;

internal static partial class HeapDiffLog
{
    [LoggerMessage(LogLevel.Information, "HeapDiff started. PathA={PathA} PathB={PathB}")]
    internal static partial void HeapDiffStarted(ILogger logger, string pathA, string pathB);

    [LoggerMessage(LogLevel.Information, "HeapDiff complete. TypeCount={TypeCount} TotalBytesDelta={TotalBytesDelta}B")]
    internal static partial void HeapDiffComplete(ILogger logger, int typeCount, long totalBytesDelta);

    [LoggerMessage(LogLevel.Error, "HeapDiff failed. PathA={PathA} PathB={PathB}")]
    internal static partial void HeapDiffFailed(ILogger logger, Exception ex, string pathA, string pathB);
}
