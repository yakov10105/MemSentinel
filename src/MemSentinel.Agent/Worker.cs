using MemSentinel.Agent.Logging;
using MemSentinel.Core.Providers;

namespace MemSentinel.Agent;

public sealed class Worker(IMemoryProvider memoryProvider, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var reading = await memoryProvider.GetRssMemoryAsync(stoppingToken);

            Log.MemoryReading(
                logger,
                rssMb: reading.RssBytes / 1_048_576.0,
                pssMb: reading.PssBytes / 1_048_576.0,
                vmSizeMb: reading.VmSizeBytes / 1_048_576.0);

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
