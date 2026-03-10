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
}
