using MemSentinel.Agent.Logging;
using MemSentinel.Contracts;
using System.Threading.Channels;

namespace MemSentinel.Agent.BackgroundServices;

public sealed class DiagnosticOrchestrator(
    ChannelReader<DiagnosticTrigger> triggerReader,
    ILogger<DiagnosticOrchestrator> logger) : BackgroundService
{
    private const int CircuitBreakerThreshold = 3;
    private static readonly TimeSpan CircuitBreakerSleep = TimeSpan.FromMinutes(10);

    private bool _isSessionActive;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int consecutiveFailures = 0;

        try
        {
            await foreach (var trigger in triggerReader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    if (_isSessionActive)
                    {
                        Log.OrchestratorBusy(logger, trigger.Reason.ToString());
                        continue;
                    }

                    _isSessionActive = true;
                    Log.OrchestratorTriggerReceived(logger, trigger.Reason.ToString(), trigger.CurrentRssMb, trigger.VelocityMbPerMinute);

                    // Capture + analysis work added in Task 3.3
                    _isSessionActive = false;
                    consecutiveFailures = 0;
                }
                catch (Exception ex)
                {
                    _isSessionActive = false;
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
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
