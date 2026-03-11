namespace MemSentinel.Core.Collectors;

public interface IProcessLocator
{
    bool IsSupported { get; }
    ValueTask<ProcessInfo?> FindTargetAsync(string processName, CancellationToken ct);
}
