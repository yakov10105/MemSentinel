namespace MemSentinel.Agent.Logging;

public static partial class Log
{
    [LoggerMessage(LogLevel.Information, "Agent starting. PollingInterval={PollingIntervalSeconds}s StorageProvider={StorageProvider}")]
    public static partial void AgentStarting(ILogger logger, int pollingIntervalSeconds, string storageProvider);

    [LoggerMessage(LogLevel.Information, "RSS={RssMb:F1}MB PSS={PssMb:F1}MB VmSize={VmSizeMb:F1}MB")]
    public static partial void MemoryReading(ILogger logger, double rssMb, double pssMb, double vmSizeMb);

    [LoggerMessage(LogLevel.Warning, "Unobserved task exception")]
    public static partial void UnobservedTaskException(ILogger logger, Exception ex);

    [LoggerMessage(LogLevel.Warning, "Watchdog failure #{FailureCount}")]
    public static partial void WatchdogFailure(ILogger logger, Exception ex, int failureCount);

    [LoggerMessage(LogLevel.Critical, "Circuit breaker open. Sleeping for {Duration}")]
    public static partial void CircuitBreakerOpen(ILogger logger, TimeSpan duration);

    [LoggerMessage(LogLevel.Information, "Diagnostic port found. SocketPath={SocketPath}")]
    public static partial void DiagnosticPortFound(ILogger logger, string socketPath);

    [LoggerMessage(LogLevel.Warning, "Diagnostic port not found. The target process may not have started yet or the shared volume is not mounted.")]
    public static partial void DiagnosticPortNotFound(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Target process visible. ProcessName={ProcessName} PID={Pid}")]
    public static partial void TargetProcessFound(ILogger logger, string processName, int pid);

    [LoggerMessage(LogLevel.Warning, "Target process not visible. ProcessName={ProcessName} — shareProcessNamespace may not be active or the process has not started.")]
    public static partial void TargetProcessNotFound(ILogger logger, string processName);

    [LoggerMessage(LogLevel.Information, "Diagnostic client connected. PID={Pid} RuntimeVersion={RuntimeVersion}")]
    public static partial void DiagnosticClientConnected(ILogger logger, int pid, string runtimeVersion);

    [LoggerMessage(LogLevel.Warning, "Diagnostic client ping failed. ErrorCode={ErrorCode}")]
    public static partial void DiagnosticClientFailed(ILogger logger, Exception ex, string errorCode);

    [LoggerMessage(LogLevel.Information, "Growth velocity: RssMbPerMin={RssMbPerMinute:F3} ManagedLeakMbPerMin={ManagedLeakMbPerMinute:F3} Samples={SampleCount}")]
    public static partial void GrowthVelocity(ILogger logger, double rssMbPerMinute, double managedLeakMbPerMinute, int sampleCount);

    [LoggerMessage(LogLevel.Warning, "Leak suspected: RssMbPerMin={RssMbPerMinute:F3} ManagedLeakMbPerMin={ManagedLeakMbPerMinute:F3}")]
    public static partial void LeakSuspected(ILogger logger, double rssMbPerMinute, double managedLeakMbPerMinute);
}
