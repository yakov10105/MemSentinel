namespace MemSentinel.Core.Collectors.Diagnostics;

public interface IDiagnosticPortLocator
{
    bool IsSupported { get; }
    ValueTask<string?> TryFindSocketPathAsync(CancellationToken ct);
}
