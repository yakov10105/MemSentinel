using MemSentinel.Agent.Logging;
using MemSentinel.Contracts.Options;
using MemSentinel.Core.Analysis;
using MemSentinel.Core.Collectors;
using MemSentinel.Core.Providers;
using Microsoft.Extensions.Options;

namespace MemSentinel.Agent;

public sealed class Worker(
    IMemoryProvider memoryProvider,
    IDiagnosticPortLocator diagnosticPortLocator,
    IProcessLocator processLocator,
    IDotNetDiagnosticClient diagnosticClient,
    MetricsBuffer metricsBuffer,
    IOptionsMonitor<SentinelOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    private const int CircuitBreakerThreshold = 3;
    private static readonly TimeSpan CircuitBreakerSleep = TimeSpan.FromMinutes(10);

    internal string? SocketPath { get; private set; }
    internal ProcessInfo? TargetProcess { get; private set; }
    internal DiagnosticConnectionInfo? ConnectionInfo { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.CurrentValue;
        Log.AgentStarting(logger, opts.PollingIntervalSeconds, opts.StorageProvider);

        SocketPath = await diagnosticPortLocator.TryFindSocketPathAsync(stoppingToken);

        if (SocketPath is not null)
            Log.DiagnosticPortFound(logger, SocketPath);
        else
            Log.DiagnosticPortNotFound(logger);

        TargetProcess = await processLocator.FindTargetAsync(opts.TargetProcessName, stoppingToken);

        if (TargetProcess is { } proc)
            Log.TargetProcessFound(logger, proc.ProcessName, proc.Pid);
        else
            Log.TargetProcessNotFound(logger, opts.TargetProcessName);

        var pingResult = await diagnosticClient.PingAsync(stoppingToken);
        if (pingResult.IsSuccess && pingResult.Value is { } conn)
        {
            ConnectionInfo = conn;
            Log.DiagnosticClientConnected(logger, conn.Pid, conn.RuntimeVersion);
        }
        else if (!pingResult.IsSuccess && pingResult.Error is { } err)
        {
            Log.DiagnosticClientFailed(logger, new Exception(err.Message), err.Code);
        }

        int consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(stoppingToken);
                consecutiveFailures = 0;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                Log.WatchdogFailure(logger, ex, consecutiveFailures);

                if (consecutiveFailures >= CircuitBreakerThreshold)
                {
                    Log.CircuitBreakerOpen(logger, CircuitBreakerSleep);
                    try { await Task.Delay(CircuitBreakerSleep, stoppingToken); }
                    catch (OperationCanceledException) { return; }
                    consecutiveFailures = 0;
                }
            }

            var interval = TimeSpan.FromSeconds(options.CurrentValue.PollingIntervalSeconds);
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        var reading = await memoryProvider.GetRssMemoryAsync(stoppingToken);
        var heap = await memoryProvider.GetHeapMetadataAsync(stoppingToken);

        Log.MemoryReading(
            logger,
            rssMb: reading.RssBytes / 1_048_576.0,
            pssMb: reading.PssBytes / 1_048_576.0,
            vmSizeMb: reading.VmSizeBytes / 1_048_576.0);

        await metricsBuffer.AddAsync(new MetricSample(reading, heap), stoppingToken);

        var snapshot = await metricsBuffer.GetSnapshotAsync(stoppingToken);
        var velocity = MemoryGrowthAnalyzer.Calculate(snapshot);

        if (velocity.SampleCount >= 2)
        {
            Log.GrowthVelocity(logger, velocity.RssMbPerMinute, velocity.ManagedLeakMbPerMinute, velocity.SampleCount);

            if (velocity.IsLeakSuspected)
                Log.LeakSuspected(logger, velocity.RssMbPerMinute, velocity.ManagedLeakMbPerMinute);
        }
    }
}
