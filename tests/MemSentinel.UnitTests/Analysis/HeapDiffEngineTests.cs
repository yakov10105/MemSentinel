using FluentAssertions;
using MemSentinel.Core.Analysis;
using Microsoft.Extensions.Logging.Abstractions;
using static MemSentinel.Core.Analysis.HeapDiffEngine;

namespace MemSentinel.UnitTests.Analysis;

public sealed class HeapDiffEngineTests
{
    private static HeapDiffEngine Build(Func<string, HeapSnapshot> loader) =>
        new(NullLogger<HeapDiffEngine>.Instance, loader);

    private static class FakeSnapshotBuilder
    {
        public static HeapSnapshot Empty() =>
            new(new Dictionary<string, (int, long)>(), LohTotalBytes: 0, LohFreeBytes: 0);

        public static HeapSnapshot With(
            (string type, int count, long bytes)[] types,
            long lohTotal = 0,
            long lohFree = 0)
        {
            var dict = new Dictionary<string, (int, long)>(StringComparer.Ordinal);
            foreach (var (type, count, bytes) in types)
                dict[type] = (count, bytes);
            return new HeapSnapshot(dict, lohTotal, lohFree);
        }
    }

    [Fact]
    public async Task DiffAsync_ReturnsTopGrowingTypes_SortedByBytesDeltaDescending()
    {
        var a = FakeSnapshotBuilder.With([("System.String", 100, 10_000), ("System.Byte[]", 50, 5_000)]);
        var b = FakeSnapshotBuilder.With([("System.String", 200, 30_000), ("System.Byte[]", 60, 7_000)]);

        var engine = Build(path => path == "a" ? a : b);
        var result = await engine.DiffAsync("a", "b", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TopGrowingTypes.Should().HaveCount(2);
        result.Value.TopGrowingTypes[0].TypeName.Should().Be("System.String");
        result.Value.TopGrowingTypes[0].BytesDelta.Should().Be(20_000);
        result.Value.TopGrowingTypes[1].TypeName.Should().Be("System.Byte[]");
        result.Value.TopGrowingTypes[1].BytesDelta.Should().Be(2_000);
    }

    [Fact]
    public async Task DiffAsync_ExcludesTypes_WhenBytesDeltaIsNonPositive()
    {
        var a = FakeSnapshotBuilder.With([("System.String", 100, 10_000), ("System.Int32", 500, 2_000)]);
        var b = FakeSnapshotBuilder.With([("System.String", 200, 20_000), ("System.Int32", 400, 1_600)]);

        var engine = Build(path => path == "a" ? a : b);
        var result = await engine.DiffAsync("a", "b", CancellationToken.None);

        result.Value.TopGrowingTypes.Should().ContainSingle()
            .Which.TypeName.Should().Be("System.String");
    }

    [Fact]
    public async Task DiffAsync_ComputesTotalDeltas_AcrossAllTypes()
    {
        var a = FakeSnapshotBuilder.With([("T1", 10, 1_000), ("T2", 20, 2_000)]);
        var b = FakeSnapshotBuilder.With([("T1", 15, 1_500), ("T2", 18, 1_800)]);

        var engine = Build(path => path == "a" ? a : b);
        var result = await engine.DiffAsync("a", "b", CancellationToken.None);

        result.Value.TotalObjectDelta.Should().Be(3);
        result.Value.TotalBytesDelta.Should().Be(300);
    }

    [Fact]
    public async Task DiffAsync_HandlesTypeNewInB_NotInA()
    {
        var a = FakeSnapshotBuilder.With([("System.String", 10, 1_000)]);
        var b = FakeSnapshotBuilder.With([("System.String", 20, 2_000), ("NewType", 5, 500)]);

        var engine = Build(path => path == "a" ? a : b);
        var result = await engine.DiffAsync("a", "b", CancellationToken.None);

        result.Value.TopGrowingTypes.Should().HaveCount(2);
        result.Value.TopGrowingTypes.Should().Contain(t => t.TypeName == "NewType" && t.CountA == 0);
    }

    [Fact]
    public async Task DiffAsync_LimitsTopGrowingTypes_ToTwenty()
    {
        var typesA = Enumerable.Range(0, 25).Select(i => ($"Type{i}", 1, 100L)).ToArray();
        var typesB = Enumerable.Range(0, 25).Select(i => ($"Type{i}", 2, 200L)).ToArray();

        var engine = Build(path => path == "a"
            ? FakeSnapshotBuilder.With(typesA)
            : FakeSnapshotBuilder.With(typesB));

        var result = await engine.DiffAsync("a", "b", CancellationToken.None);

        result.Value.TopGrowingTypes.Should().HaveCount(20);
    }

    [Fact]
    public async Task DiffAsync_ComputesLohFreePercent_FromBothSnapshots()
    {
        var a = FakeSnapshotBuilder.With([], lohTotal: 1_000_000, lohFree: 200_000);
        var b = FakeSnapshotBuilder.With([], lohTotal: 1_200_000, lohFree: 300_000);

        var engine = Build(path => path == "a" ? a : b);
        var result = await engine.DiffAsync("a", "b", CancellationToken.None);

        result.Value.LohFreePercent.Should().BeApproximately(22.72, precision: 0.01);
    }

    [Fact]
    public async Task DiffAsync_ReturnsFailure_WhenLoaderThrows()
    {
        var engine = Build(_ => throw new InvalidOperationException("corrupt dump"));
        var result = await engine.DiffAsync("a", "b", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.Code.Should().Be("DIFF_FAILED");
    }

    [Fact]
    public async Task DiffAsync_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var engine = Build(_ => FakeSnapshotBuilder.Empty());
        var act = () => engine.DiffAsync("a", "b", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DiffAsync_ReturnsEmptyReport_WhenBothSnapshotsEmpty()
    {
        var engine = Build(_ => FakeSnapshotBuilder.Empty());
        var result = await engine.DiffAsync("a", "b", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TopGrowingTypes.Should().BeEmpty();
        result.Value.TotalObjectDelta.Should().Be(0);
        result.Value.TotalBytesDelta.Should().Be(0);
    }
}
