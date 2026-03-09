---
name: dotnet-tests
description: >
  Testing conventions and patterns for MemSentinel.
  Auto-invoke when: writing or editing test files, discussing test strategy,
  working in test projects, or when the words "test", "mock", "assert",
  "coverage", "fake", or "xUnit" appear.
---

# MemSentinel Testing Rules

## Framework & Libraries

- **Test framework:** xUnit ONLY.
- **Mocking:** NSubstitute for all interface mocking (preferred over Moq).
- **Assertions:** FluentAssertions for all assertions — more readable failure messages.
- **Integration tests:** SQLite in-memory for EF Core. `WebApplicationFactory<T>` for API tests.
- **Benchmarks:** BenchmarkDotNet in `MemSentinel.Benchmarks` — never in unit or integration tests.

> **Why NSubstitute over Moq:** Moq introduced SponsorLink telemetry in 4.20.0.
> NSubstitute has a cleaner API for `async Task` returns and `Result<T>` verification.

## Project Structure

```
tests/
  MemSentinel.UnitTests/
    Analysis/
      HeapDiffEngineTests.cs
      RootChainAnalyzerTests.cs
    Collectors/
      ProcessMetricsProviderTests.cs
    Features/
      LeakDetection/
        TriggerLeakAnalysisHandlerTests.cs
      ManualCapture/
        CaptureSnapshotHandlerTests.cs
    BackgroundServices/
      MemoryWatchdogTests.cs          # threshold logic, circuit breaker
      DiagnosticOrchestratorTests.cs  # state machine transitions
    Fakes/
      FakeMemoryProvider.cs           # implements IMemoryProvider
      FakeStorageProvider.cs          # implements IStorageProvider
      FakeSnapshotBuilder.cs          # builds synthetic ClrMD-equivalent snapshots
      FakeChannel.cs                  # wraps Channel<T> for trigger testing
  MemSentinel.IntegrationTests/
    Infrastructure/
      Storage/
        S3StorageProviderTests.cs
        LocalPvStorageProviderTests.cs
    Api/
      SnapshotEndpointTests.cs
      LiveStatsEndpointTests.cs
  MemSentinel.Benchmarks/
    Analysis/
      HeapDiffEngineBenchmarks.cs
    Collectors/
      ProcParserBenchmarks.cs
```

## Naming Convention

`MethodName_Scenario_ExpectedBehavior`

```csharp
[Fact]
public async Task AnalyzeAsync_WhenSnapshotBHasMoreObjects_ShouldReturnPositiveDelta() { }

[Fact]
public async Task AnalyzeAsync_WhenSnapshotFileMissing_ShouldReturnFailureResult() { }

[Theory]
[InlineData(0.80)]
[InlineData(0.95)]
public void ExceedsThreshold_WithVariousLoadFactors_ShouldReturnCorrectly(double load) { }
```

## AAA Pattern

Every test uses Arrange-Act-Assert with blank-line separation.
No `// Arrange` comments — expressed through whitespace and descriptive variable names:

```csharp
[Fact]
public async Task HandleAsync_WhenMemoryExceedsThreshold_ShouldTriggerCapture()
{
    var collector = Substitute.For<ISnapshotCollector>();
    var storage = Substitute.For<IStorageProvider>();
    collector.CaptureAsync(Arg.Any<CancellationToken>())
             .Returns(Result<SnapshotMetadata>.Success(FakeSnapshotBuilder.Build()));
    var handler = new TriggerLeakAnalysisHandler(collector, storage);
    var metrics = new ProcessMetrics(RssBytes: 900_000_000, LimitBytes: 1_000_000_000);

    var result = await handler.HandleAsync(metrics, CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    await collector.Received(1).CaptureAsync(Arg.Any<CancellationToken>());
}
```

## Fake Providers (IMemoryProvider / IStorageProvider)

Never use real Linux `/proc` in unit tests. Use `FakeMemoryProvider` — a controllable
implementation of `IMemoryProvider` that supports programmatic sequences:

```csharp
// tests/MemSentinel.UnitTests/Fakes/FakeMemoryProvider.cs
public sealed class FakeMemoryProvider : IMemoryProvider
{
    private readonly Queue<RssMemoryReading> _readings = new();
    public bool IsAvailable => true;

    public void EnqueueReading(long rssBytes, long limitBytes) =>
        _readings.Enqueue(new RssMemoryReading(rssBytes, limitBytes, DateTimeOffset.UtcNow));

    public void EnqueueSteepClimb(long startBytes, long limitBytes, int steps, long incrementBytes)
    {
        for (int i = 0; i < steps; i++)
            _readings.Enqueue(new RssMemoryReading(startBytes + (i * incrementBytes), limitBytes, DateTimeOffset.UtcNow));
    }

    public ValueTask<RssMemoryReading> GetRssMemoryAsync(CancellationToken ct) =>
        _readings.TryDequeue(out var reading)
            ? ValueTask.FromResult(reading)
            : throw new InvalidOperationException("FakeMemoryProvider queue is empty — enqueue more readings.");

    public ValueTask<HeapMetadata> GetHeapMetadataAsync(CancellationToken ct) =>
        ValueTask.FromResult(HeapMetadata.Empty);
}
```

Use `EnqueueSteepClimb` to test velocity threshold triggering without ClrMD.

## Fake Snapshot Builder (ClrMD Test Data)

Never use real `.gcdump` files in unit tests. Build synthetic snapshots:

```csharp
// tests/MemSentinel.UnitTests/Fakes/FakeSnapshotBuilder.cs
public static class FakeSnapshotBuilder
{
    public static HeapSnapshot Build(
        int objectCount = 1000,
        string dominantType = "System.String",
        long bytesPerObject = 256)
    {
        var objects = Enumerable.Range(0, objectCount)
            .Select(i => new HeapObject(
                Address: (ulong)(0x1000 + i * bytesPerObject),
                TypeName: i % 10 == 0 ? "System.Byte[]" : dominantType,
                Size: (ulong)bytesPerObject))
            .ToList();

        return new HeapSnapshot(
            CapturedAt: DateTimeOffset.UtcNow,
            Objects: objects,
            TotalManagedBytes: objectCount * bytesPerObject);
    }

    public static (HeapSnapshot A, HeapSnapshot B) BuildDiffPair(
        int baseCount = 1000,
        int leakedCount = 200,
        string leakingType = "MyApp.LeakyCache")
    {
        var snapshotA = Build(objectCount: baseCount);
        var leakedObjects = Enumerable.Range(0, leakedCount)
            .Select(i => new HeapObject(
                Address: (ulong)(0xA000 + i * 512),
                TypeName: leakingType,
                Size: 512))
            .ToList();

        var snapshotB = snapshotA with
        {
            Objects = [.. snapshotA.Objects, .. leakedObjects],
            TotalManagedBytes = snapshotA.TotalManagedBytes + (leakedCount * 512)
        };
        return (snapshotA, snapshotB);
    }
}
```

## Testing Result<T>

Always test both `IsSuccess = true` and `IsSuccess = false` paths. Never test only the happy path:

```csharp
[Fact]
public async Task DiffAsync_WithValidSnapshots_ShouldReturnSuccessWithDelta()
{
    var (snapshotA, snapshotB) = FakeSnapshotBuilder.BuildDiffPair(leakedCount: 200);
    var engine = new HeapDiffEngine(NullLogger<HeapDiffEngine>.Instance);

    var result = await engine.DiffAsync(snapshotA, snapshotB, CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    result.Value!.TotalObjectDelta.Should().Be(200);
    result.Value.TopLeakingTypes.Should().ContainSingle(t => t.TypeName == "MyApp.LeakyCache");
}

[Fact]
public async Task DiffAsync_WhenSnapshotBIsNull_ShouldReturnFailure()
{
    var snapshotA = FakeSnapshotBuilder.Build();
    var engine = new HeapDiffEngine(NullLogger<HeapDiffEngine>.Instance);

    var result = await engine.DiffAsync(snapshotA, null!, CancellationToken.None);

    result.IsSuccess.Should().BeFalse();
    result.Error!.Value.Code.Should().Be("SNAPSHOT_NULL");
}
```

## Testing the Circuit Breaker (Task 0.5)

```csharp
[Fact]
public async Task ExecuteAsync_WhenProviderFailsThreeTimes_ShouldEnterSleepState()
{
    var fakeProvider = new FakeMemoryProvider();
    fakeProvider.EnqueueException(new InvalidOperationException("attach failed"));
    fakeProvider.EnqueueException(new InvalidOperationException("attach failed"));
    fakeProvider.EnqueueException(new InvalidOperationException("attach failed"));

    var watchdog = new MemoryWatchdog(fakeProvider, Options, NullLogger<MemoryWatchdog>.Instance);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

    await watchdog.StartAsync(cts.Token);
    await Task.Delay(500); // allow failure loop to run

    watchdog.State.Should().Be(WatchdogState.CircuitOpen);
}
```

Add `WatchdogState` enum to `MemoryWatchdog` — expose as internal property for testability.
Never expose internal state through public API — use `[InternalsVisibleTo("MemSentinel.UnitTests")]`.

## Testing Channel<T> (Watchdog → Orchestrator)

Test that triggers flow correctly through the channel without coupling to timing:

```csharp
[Fact]
public async Task MemoryWatchdog_WhenThresholdExceeded_ShouldWriteTriggerToChannel()
{
    var channel = Channel.CreateUnbounded<DiagnosticTrigger>();
    var fakeProvider = new FakeMemoryProvider();
    fakeProvider.EnqueueReading(rssBytes: 950_000_000, limitBytes: 1_000_000_000); // 95% — exceeds threshold

    var watchdog = new MemoryWatchdog(fakeProvider, channel.Writer, Options, NullLogger<MemoryWatchdog>.Instance);
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

    await watchdog.StartAsync(cts.Token);
    await Task.Delay(150);

    channel.Reader.TryRead(out var trigger).Should().BeTrue();
    trigger.Reason.Should().Be(TriggerReason.ThresholdExceeded);
}
```

Use `Channel.CreateUnbounded<T>()` in tests (not bounded) to avoid `DropOldest` hiding bugs.

## Lifecycle: IAsyncLifetime over IDisposable

Use `IAsyncLifetime` for async setup/teardown. Never mix sync `IDisposable` with async test setup:

```csharp
public sealed class DiagnosticOrchestratorTests : IAsyncLifetime
{
    private FakeMemoryProvider _provider = null!;
    private DiagnosticOrchestrator _orchestrator = null!;

    public async Task InitializeAsync()
    {
        _provider = new FakeMemoryProvider();
        _orchestrator = new DiagnosticOrchestrator(_provider, /* ... */);
        await _orchestrator.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _orchestrator.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task OrchestratorTest() { /* ... */ }
}
```

## Integration Tests — API Layer

Use `WebApplicationFactory<T>` to test Minimal API endpoints with real DI wiring
but fake providers:

```csharp
public sealed class SnapshotEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetLiveStats_ShouldReturn200_WithValidPayload()
    {
        var client = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IMemoryProvider, FakeMemoryProvider>()))
            .CreateClient();

        var response = await client.GetAsync("/api/stats/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<LiveStatsResponse>();
        stats.Should().NotBeNull();
    }
}
```

## Integration Tests — EF Core

SQLite in-memory with kept-open connection and `IAsyncLifetime`:

```csharp
public sealed class SnapshotRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DiagnosticsDbContext _context = null!;
    private SnapshotRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DiagnosticsDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new DiagnosticsDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new SnapshotRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
```

## Coverage Requirements

- **Minimum 80%:** `MemSentinel.Core`, `MemSentinel.Agent/Features`
- **100% required:**
  - `HeapDiffEngine` (analysis accuracy is core product value)
  - Threshold evaluation in `MemoryWatchdog` (false negatives = missed leaks)
  - Storage upload retry/failure handling
  - Alert dispatch
  - Circuit breaker state transitions

## What to Test

- All `Result<T>` paths — both success and failure
- State transitions in `DiagnosticOrchestrator` state machine
- Threshold boundary conditions (at threshold, below, above, velocity)
- Circuit breaker: 1 failure, 2 failures, 3 failures → sleep, recovery
- `Channel<T>` trigger flow from watchdog to orchestrator
- `IMemoryProvider` swap — Mock mode starts correctly on non-Linux
- Concurrent access on `SnapshotRegistry` and session state

## What NOT to Test

- DTOs and `readonly record struct` with no logic
- Auto-implemented properties
- Trivial primary constructors
- ClrMD, EF Core, or xUnit internals
- Private methods directly — test through public API
- `[InternalsVisibleTo]` test targets are the exception for state inspection only

## Test Isolation Rules

- No shared mutable state between tests
- Fresh fakes/substitutes per test (or per-class constructor for stateless shared setup)
- Tests must not depend on execution order
- No `Thread.Sleep` — use `Task.Delay` with short timeouts or `Channel<T>` completion signals
- No real file system, network, or `/proc` access in unit tests

## Performance Constraints

- Unit tests: **< 100ms each**
- Integration tests: **< 2s each**
- Benchmarks: `MemSentinel.Benchmarks` project only, never in test projects

## Prohibited

- No Moq (use NSubstitute)
- No `async void` test methods
- No `.Result` or `.Wait()` on Tasks
- No `Thread.Sleep`
- No real `/proc`, network, or file system access in unit tests
- No testing private methods directly
- No comments in test code — names must be self-documenting
- No `IDisposable` for async teardown — use `IAsyncLifetime`
- No real `.gcdump` files in unit tests — use `FakeSnapshotBuilder`
- No `Channel.CreateBounded` in tests — use `Unbounded` to avoid hiding bugs
