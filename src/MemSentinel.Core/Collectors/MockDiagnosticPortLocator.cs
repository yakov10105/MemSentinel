namespace MemSentinel.Core.Collectors;

public sealed class MockDiagnosticPortLocator : IDiagnosticPortLocator
{
    private const string FakeSocketPath = "/tmp/dotnet-diagnostic-mock-12345.sock";

    public bool IsSupported => true;

    public ValueTask<string?> TryFindSocketPathAsync(CancellationToken ct) =>
        ValueTask.FromResult<string?>(FakeSocketPath);
}
