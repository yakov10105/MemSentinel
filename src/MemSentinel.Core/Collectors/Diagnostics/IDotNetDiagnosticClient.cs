using MemSentinel.Core.Common;

namespace MemSentinel.Core.Collectors.Diagnostics;

public interface IDotNetDiagnosticClient
{
    bool IsSupported { get; }
    ValueTask<Result<DiagnosticConnectionInfo>> PingAsync(CancellationToken ct);
}
