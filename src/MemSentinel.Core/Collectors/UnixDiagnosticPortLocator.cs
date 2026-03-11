namespace MemSentinel.Core.Collectors;

public sealed class UnixDiagnosticPortLocator : IDiagnosticPortLocator
{
    private const string SocketDirectory = "/tmp";
    private const string SocketPattern = "dotnet-diagnostic-*.sock";

    public bool IsSupported => OperatingSystem.IsLinux();

    public ValueTask<string?> TryFindSocketPathAsync(CancellationToken ct)
    {
        var matches = Directory.GetFiles(SocketDirectory, SocketPattern);
        var path = matches.Length > 0 ? matches[0] : null;
        return ValueTask.FromResult(path);
    }
}
