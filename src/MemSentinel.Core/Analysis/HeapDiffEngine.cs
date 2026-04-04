using MemSentinel.Contracts;
using MemSentinel.Core.Common;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Extensions.Logging;

namespace MemSentinel.Core.Analysis;

public sealed class HeapDiffEngine : IHeapDiffEngine
{
    internal readonly record struct HeapSnapshot(
        Dictionary<string, (int Count, long Bytes)> Types,
        long LohTotalBytes,
        long LohFreeBytes);

    private readonly ILogger<HeapDiffEngine> _logger;
    private readonly Func<string, HeapSnapshot> _loadDump;

    public HeapDiffEngine(ILogger<HeapDiffEngine> logger)
        : this(logger, LoadFromDisk) { }

    internal HeapDiffEngine(ILogger<HeapDiffEngine> logger, Func<string, HeapSnapshot> loadDump)
    {
        _logger = logger;
        _loadDump = loadDump;
    }

    public async Task<Result<HeapDiffReport>> DiffAsync(string pathA, string pathB, CancellationToken ct)
    {
        HeapDiffLog.HeapDiffStarted(_logger, pathA, pathB);
        try
        {
            var (snapshotA, snapshotB) = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var a = _loadDump(pathA);
                ct.ThrowIfCancellationRequested();
                var b = _loadDump(pathB);
                return (a, b);
            }, ct);

            var report = BuildReport(snapshotA, snapshotB);
            HeapDiffLog.HeapDiffComplete(_logger, report.TopGrowingTypes.Count, report.TotalBytesDelta);
            return Result<HeapDiffReport>.Success(report);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            HeapDiffLog.HeapDiffFailed(_logger, ex, pathA, pathB);
            return Result<HeapDiffReport>.Failure(new Error("DIFF_FAILED", ex.Message));
        }
    }

    private static HeapDiffReport BuildReport(HeapSnapshot a, HeapSnapshot b)
    {
        var allTypes = new HashSet<string>(a.Types.Keys, StringComparer.Ordinal);
        allTypes.UnionWith(b.Types.Keys);

        var topGrowing = new List<TypeDelta>(capacity: 20);
        long totalObjectDelta = 0, totalBytesDelta = 0;

        foreach (var typeName in allTypes)
        {
            a.Types.TryGetValue(typeName, out var ta);
            b.Types.TryGetValue(typeName, out var tb);

            totalObjectDelta += tb.Count - ta.Count;
            totalBytesDelta += tb.Bytes - ta.Bytes;

            if (tb.Bytes > ta.Bytes)
                topGrowing.Add(new TypeDelta(typeName, ta.Count, tb.Count, ta.Bytes, tb.Bytes));
        }

        topGrowing.Sort(static (x, y) => y.BytesDelta.CompareTo(x.BytesDelta));

        var lohFreePercent = a.LohTotalBytes > 0 || b.LohTotalBytes > 0
            ? (double)(a.LohFreeBytes + b.LohFreeBytes) / Math.Max(a.LohTotalBytes + b.LohTotalBytes, 1) * 100.0
            : 0.0;

        return new HeapDiffReport(
            AnalyzedAt: DateTimeOffset.UtcNow,
            TopGrowingTypes: topGrowing.Take(20).ToList(),
            TotalObjectDelta: totalObjectDelta,
            TotalBytesDelta: totalBytesDelta,
            LohFreePercent: lohFreePercent);
    }

    private static HeapSnapshot LoadFromDisk(string path)
    {
        var types = new Dictionary<string, (int Count, long Bytes)>(capacity: 10_000, StringComparer.Ordinal);
        long lohTotal = 0, lohFree = 0;

        DataTarget? target = null;
        ClrRuntime? runtime = null;
        try
        {
            target = DataTarget.LoadDump(path);
            runtime = target.ClrVersions[0].CreateRuntime();
            var heap = runtime.Heap;

            var lohSegments = heap.Segments
                .Where(s => s.Kind == GCSegmentKind.Large)
                .Select(s => (Start: s.FirstObjectAddress, End: s.CommittedMemory.End))
                .ToList();

            lohTotal = heap.Segments
                .Where(s => s.Kind == GCSegmentKind.Large)
                .Sum(s => (long)s.Length);

            foreach (var obj in heap.EnumerateObjects())
            {
                if (obj.Size < 85)
                    continue;

                var inLoh = lohSegments.Any(seg => obj.Address >= seg.Start && obj.Address < seg.End);

                if (obj.IsFree)
                {
                    if (inLoh) lohFree += (long)obj.Size;
                    continue;
                }

                var typeName = string.Intern(obj.Type?.Name ?? "<unknown>");
                types.TryGetValue(typeName, out var current);
                types[typeName] = (current.Count + 1, current.Bytes + (long)obj.Size);
            }
        }
        finally
        {
            runtime?.Dispose();
            target?.Dispose();
        }

        return new HeapSnapshot(types, lohTotal, lohFree);
    }
}
