namespace MemSentinel.Core.Collectors;

public interface IDiagnosticPortLocator
{
    bool IsSupported { get; }
    ValueTask<string?> TryFindSocketPathAsync(CancellationToken ct);
}
