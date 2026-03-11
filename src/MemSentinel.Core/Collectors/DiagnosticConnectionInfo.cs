namespace MemSentinel.Core.Collectors;

public readonly record struct DiagnosticConnectionInfo(int Pid, string RuntimeVersion, string CommandLine);
