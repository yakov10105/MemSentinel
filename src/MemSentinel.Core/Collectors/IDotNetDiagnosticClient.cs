using MemSentinel.Core.Common;

namespace MemSentinel.Core.Collectors;

public interface IDotNetDiagnosticClient
{
    bool IsSupported { get; }
    ValueTask<Result<DiagnosticConnectionInfo>> PingAsync(CancellationToken ct);
}
