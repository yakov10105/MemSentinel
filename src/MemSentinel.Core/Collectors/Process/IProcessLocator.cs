namespace MemSentinel.Core.Collectors.Process;

public interface IProcessLocator
{
    bool IsSupported { get; }
    ValueTask<ProcessInfo?> FindTargetAsync(string processName, CancellationToken ct);
}
