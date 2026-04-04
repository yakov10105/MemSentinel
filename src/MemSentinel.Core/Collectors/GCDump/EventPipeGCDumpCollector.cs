using System.Buffers;
using System.Diagnostics.Tracing;
using MemSentinel.Core.Common;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Extensions.Logging;

namespace MemSentinel.Core.Collectors.GCDump;

public sealed class EventPipeGCDumpCollector(int pid, ILogger<EventPipeGCDumpCollector> logger) : IGCDumpCollector
{
    private const long GCHeapDumpKeyword = 0x100000L;
    private const int CopyBufferSize = 65536;

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<Result<string>> CaptureAsync(string outputPath, CancellationToken ct)
    {
        if (!await _gate.WaitAsync(0))
            return Result<string>.Failure(new Error("CAPTURE_BUSY", "A GC dump capture is already in progress."));

        try
        {
            GCDumpLog.GCDumpStarted(logger, pid, outputPath);

            var providers = new[]
            {
                new EventPipeProvider(
                    "Microsoft-Windows-DotNETRuntime",
                    EventLevel.Informational,
                    GCHeapDumpKeyword)
            };

            var client = new DiagnosticsClient(pid);
            using var session = client.StartEventPipeSession(providers, requestRundown: true);

            var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
            try
            {
                await using var fileStream = new FileStream(
                    outputPath, FileMode.Create, FileAccess.Write,
                    FileShare.None, bufferSize: 4096, useAsync: true);

                int bytesRead;
                while ((bytesRead = await session.EventStream.ReadAsync(buffer, ct)) > 0)
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                try { session.Stop(); } catch { }
            }

            var fileSizeBytes = new FileInfo(outputPath).Length;
            GCDumpLog.GCDumpComplete(logger, pid, outputPath, fileSizeBytes);
            return Result<string>.Success(outputPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            GCDumpLog.GCDumpFailed(logger, ex, pid, outputPath);
            return Result<string>.Failure(new Error("CAPTURE_FAILED", ex.Message));
        }
        finally
        {
            _gate.Release();
        }
    }
}
