using MemSentinel.Agent.Logging;
using MemSentinel.Contracts.Options;
using MemSentinel.Core.Providers;
using Microsoft.Extensions.Options;

namespace MemSentinel.Agent;

public sealed class Worker(
    IMemoryProvider memoryProvider,
    IOptionsMonitor<SentinelOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    private const int CircuitBreakerThreshold = 3;
    private static readonly TimeSpan CircuitBreakerSleep = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.CurrentValue;
        Log.AgentStarting(logger, opts.PollingIntervalSeconds, opts.StorageProvider);

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
                    await Task.Delay(CircuitBreakerSleep, stoppingToken);
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

        Log.MemoryReading(
            logger,
            rssMb: reading.RssBytes / 1_048_576.0,
            pssMb: reading.PssBytes / 1_048_576.0,
            vmSizeMb: reading.VmSizeBytes / 1_048_576.0);
    }
}
