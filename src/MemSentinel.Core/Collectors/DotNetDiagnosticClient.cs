using MemSentinel.Core.Common;
using Microsoft.Diagnostics.NETCore.Client;

namespace MemSentinel.Core.Collectors;

public sealed class DotNetDiagnosticClient(
    IProcessLocator processLocator,
    string processName) : IDotNetDiagnosticClient
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsSupported => OperatingSystem.IsLinux();

    public async ValueTask<Result<DiagnosticConnectionInfo>> PingAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var processInfo = await processLocator.FindTargetAsync(processName, ct);
            if (processInfo is not { } target)
                return Result<DiagnosticConnectionInfo>.Failure(new Error("PROCESS_NOT_FOUND", $"Process '{processName}' not visible."));

            var env = await Task.Run(() =>
            {
                var client = new DiagnosticsClient(target.Pid);
                return client.GetProcessEnvironment();
            }, ct);

            env.TryGetValue("DOTNET_VERSION", out var runtimeVersion);

            return Result<DiagnosticConnectionInfo>.Success(new DiagnosticConnectionInfo(
                Pid: target.Pid,
                RuntimeVersion: runtimeVersion ?? "unknown",
                CommandLine: string.Empty));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<DiagnosticConnectionInfo>.Failure(new Error("PTRACE_DENIED", ex.Message));
        }
        catch (Exception ex)
        {
            return Result<DiagnosticConnectionInfo>.Failure(new Error("ATTACH_FAILED", ex.Message));
        }
        finally
        {
            _gate.Release();
        }
    }
}
