namespace MemSentinel.Core.Analysis;

public sealed class MetricsBuffer(TimeSpan window)
{
    private readonly Queue<MetricSample> _samples = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask AddAsync(MetricSample sample, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _samples.Enqueue(sample);
            var cutoff = DateTimeOffset.UtcNow - window;
            while (_samples.Count > 0 && _samples.Peek().Rss.CapturedAt < cutoff)
                _samples.Dequeue();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<MetricSample>> GetSnapshotAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return [.._samples];
        }
        finally
        {
            _gate.Release();
        }
    }
}
