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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.CurrentValue;
        Log.AgentStarting(logger, opts.PollingIntervalSeconds, opts.StorageProvider);

        while (!stoppingToken.IsCancellationRequested)
        {
            var reading = await memoryProvider.GetRssMemoryAsync(stoppingToken);

            Log.MemoryReading(
                logger,
                rssMb: reading.RssBytes / 1_048_576.0,
                pssMb: reading.PssBytes / 1_048_576.0,
                vmSizeMb: reading.VmSizeBytes / 1_048_576.0);

            var interval = TimeSpan.FromSeconds(options.CurrentValue.PollingIntervalSeconds);
            await Task.Delay(interval, stoppingToken);
        }
    }
}
