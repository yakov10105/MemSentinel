using MemSentinel.Core.Common;

namespace MemSentinel.Core.Collectors;

public sealed class MockDotNetDiagnosticClient : IDotNetDiagnosticClient
{
    public bool IsSupported => true;

    public ValueTask<Result<DiagnosticConnectionInfo>> PingAsync(CancellationToken ct) =>
        ValueTask.FromResult(Result<DiagnosticConnectionInfo>.Success(
            new DiagnosticConnectionInfo(Pid: 1, RuntimeVersion: "mock-10.0.0", CommandLine: "dotnet mock")));
}
