using System.Diagnostics.Tracing;
using MemSentinel.Core.Providers;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace MemSentinel.Core.Collectors.GCMetrics;

public sealed class EventPipeGCMetricsProvider(int pid) : IGCMetricsProvider, IAsyncDisposable
{
    private static readonly EventPipeProvider[] Providers =
    [
        new EventPipeProvider(
            "Microsoft-Windows-DotNETRuntime",
            EventLevel.Verbose,
            (long)ClrTraceEventParser.Keywords.GC)
    ];

    private sealed class HeapSnapshot(HeapMetadata value)
    {
        public HeapMetadata Value => value;
    }

    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private volatile HeapSnapshot _latest = new(new HeapMetadata(0, 0, 0, 0, 0, DateTimeOffset.UtcNow));
    private Task? _backgroundLoop;
    private bool _started;

    public async ValueTask<HeapMetadata> GetAsync(CancellationToken ct)
    {
        if (!_started)
            await EnsureStartedAsync(ct);

        return _latest.Value;
    }

    private async ValueTask EnsureStartedAsync(CancellationToken ct)
    {
        await _startGate.WaitAsync(ct);
        try
        {
            if (_started)
                return;

            _backgroundLoop = Task.Run(() => RunEventLoopAsync(_cts.Token), _cts.Token);
            _started = true;
        }
        finally
        {
            _startGate.Release();
        }
    }

    private async Task RunEventLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            EventPipeSession? session = null;
            try
            {
                var client = new DiagnosticsClient(pid);
                session = client.StartEventPipeSession(Providers, requestRundown: false);

                using var source = new EventPipeEventSource(session.EventStream);

                source.Clr.GCHeapStats += e =>
                {
                    _latest = new HeapSnapshot(new HeapMetadata(
                        Gen0Bytes: (long)e.GenerationSize0,
                        Gen1Bytes: (long)e.GenerationSize1,
                        Gen2Bytes: (long)e.GenerationSize2,
                        LohBytes: (long)e.GenerationSize3,
                        PohBytes: (long)e.GenerationSize4,
                        CapturedAt: DateTimeOffset.UtcNow));
                };

                await Task.Run(() => source.Process(), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
                catch (OperationCanceledException) { break; }
            }
            finally
            {
                try { session?.Stop(); } catch { }
                session?.Dispose();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        if (_backgroundLoop is not null)
        {
            try { await _backgroundLoop; }
            catch (OperationCanceledException) { }
        }
        _cts.Dispose();
        _startGate.Dispose();
    }
}
