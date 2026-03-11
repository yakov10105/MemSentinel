using FluentAssertions;
using MemSentinel.Core.Analysis;
using MemSentinel.Core.Providers;

namespace MemSentinel.UnitTests.Analysis;

public sealed class MetricsBufferTests
{
    private static MetricSample Sample(DateTimeOffset at) =>
        new(
            Rss: new RssMemoryReading(100 * 1024 * 1024, 0, 0, at),
            Heap: new HeapMetadata(0, 0, 0, 0, 0, at));

    [Fact]
    public async Task GetSnapshotAsync_ReturnsAllSamples_WithinWindow()
    {
        var buffer = new MetricsBuffer(TimeSpan.FromMinutes(60));
        var now = DateTimeOffset.UtcNow;

        await buffer.AddAsync(Sample(now.AddMinutes(-10)), CancellationToken.None);
        await buffer.AddAsync(Sample(now.AddMinutes(-5)), CancellationToken.None);
        await buffer.AddAsync(Sample(now), CancellationToken.None);

        var snapshot = await buffer.GetSnapshotAsync(CancellationToken.None);

        snapshot.Should().HaveCount(3);
    }

    [Fact]
    public async Task AddAsync_PrunesEntries_OlderThanWindow()
    {
        var buffer = new MetricsBuffer(TimeSpan.FromMinutes(30));
        var now = DateTimeOffset.UtcNow;

        await buffer.AddAsync(Sample(now.AddMinutes(-60)), CancellationToken.None);
        await buffer.AddAsync(Sample(now.AddMinutes(-45)), CancellationToken.None);
        await buffer.AddAsync(Sample(now.AddMinutes(-10)), CancellationToken.None);
        await buffer.AddAsync(Sample(now), CancellationToken.None);

        var snapshot = await buffer.GetSnapshotAsync(CancellationToken.None);

        snapshot.Should().HaveCount(2);
        snapshot.All(s => s.Rss.CapturedAt >= now.AddMinutes(-30)).Should().BeTrue();
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsIndependentCopy()
    {
        var buffer = new MetricsBuffer(TimeSpan.FromMinutes(60));
        var now = DateTimeOffset.UtcNow;

        await buffer.AddAsync(Sample(now), CancellationToken.None);
        var snapshot1 = await buffer.GetSnapshotAsync(CancellationToken.None);

        await buffer.AddAsync(Sample(now.AddSeconds(5)), CancellationToken.None);
        var snapshot2 = await buffer.GetSnapshotAsync(CancellationToken.None);

        snapshot1.Should().HaveCount(1);
        snapshot2.Should().HaveCount(2);
    }
}
