namespace MemSentinel.Core.Collectors.Diagnostics;

public readonly record struct DiagnosticConnectionInfo(int Pid, string RuntimeVersion, string CommandLine);
