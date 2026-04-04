using MemSentinel.Core.Collectors.Process;

namespace MemSentinel.Core.Collectors.Mocks;

public sealed class MockProcessLocator : IProcessLocator
{
    public bool IsSupported => true;

    public ValueTask<ProcessInfo?> FindTargetAsync(string processName, CancellationToken ct) =>
        ValueTask.FromResult<ProcessInfo?>(new ProcessInfo(Pid: 1, ProcessName: processName));
}
