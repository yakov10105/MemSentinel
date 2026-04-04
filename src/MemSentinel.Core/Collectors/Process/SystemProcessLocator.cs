namespace MemSentinel.Core.Collectors.Process;

public sealed class SystemProcessLocator : IProcessLocator
{
    public bool IsSupported => true;

    public ValueTask<ProcessInfo?> FindTargetAsync(string processName, CancellationToken ct)
    {
        var processes = System.Diagnostics.Process.GetProcessesByName(processName);
        if (processes.Length == 0)
            return ValueTask.FromResult<ProcessInfo?>(null);

        var target = processes[0];
        return ValueTask.FromResult<ProcessInfo?>(new ProcessInfo(target.Id, target.ProcessName));
    }
}
