using Microsoft.Extensions.Logging;

namespace MemSentinel.Core.Collectors.GCDump;

internal static partial class GCDumpLog
{
    [LoggerMessage(LogLevel.Information, "GCDump started. PID={Pid} OutputPath={OutputPath}")]
    internal static partial void GCDumpStarted(ILogger logger, int pid, string outputPath);

    [LoggerMessage(LogLevel.Information, "GCDump complete. PID={Pid} OutputPath={OutputPath} FileSize={FileSizeBytes}B")]
    internal static partial void GCDumpComplete(ILogger logger, int pid, string outputPath, long fileSizeBytes);

    [LoggerMessage(LogLevel.Error, "GCDump failed. PID={Pid} OutputPath={OutputPath}")]
    internal static partial void GCDumpFailed(ILogger logger, Exception ex, int pid, string outputPath);
}
