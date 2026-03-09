---
name: dotnet-architecture
description: >
  .NET architecture and design patterns for MemSentinel.
  Auto-invoke when: designing new services, adding classes or interfaces,
  discussing dependency injection, layer boundaries, project references,
  or Clean Architecture structure. Do NOT load for simple bug fixes,
  test edits, or dashboard (Next.js) changes.
---

# MemSentinel Architecture Rules

## Layer Structure & Dependency Flow

```
MemSentinel.Contracts     <- Shared DTOs, Interfaces, Enums. NO logic.
  Snapshots/              <- IMemorySnapshot, ILeakReport (matches TypeScript interfaces)
  Options/                <- SentinelOptions, WatchdogOptions, StorageOptions

MemSentinel.Core          <- Diagnostic library. References Contracts only.
  Analysis/               <- HeapDiffEngine, RootChainAnalyzer (ClrMD)
  Collectors/             <- ProcessMetricsProvider, DotNetDiagnosticClient
  Providers/              <- IMemoryProvider, LinuxMemoryProvider, MockMemoryProvider
  Common/                 <- Result<T>, Error, shared base types

MemSentinel.Agent         <- Sidecar application. References Core + Contracts.
  BackgroundServices/     <- MemoryWatchdog, DiagnosticOrchestrator
  Infrastructure/         <- Storage adapters (S3, AzureBlob, LocalPV), Notifiers
  Api/                    <- Minimal API endpoints for Dashboard
  Features/               <- Vertical Slice feature handlers

MemSentinel.Tests         <- XUnit. References project under test only.
  Fakes/                  <- MockMemoryProvider, InMemoryStorageProvider
  Fixtures/               <- ClrMD snapshot builders for heap diff testing
```

**Strict dependency rules:**

- `Contracts` → no dependencies
- `Core` → `Contracts` only
- `Agent` → `Core` + `Contracts`
- `Tests` → project under test only, never implementation internals
- Dashboard (Next.js) is a completely separate build — no .NET project references it

## The Environment Abstraction (IMemoryProvider)

This is the most critical architectural decision. The Agent must run on Windows
(Mock mode) and Linux (Real mode) without code changes. Never call `/proc` or
ClrMD directly from handlers — always go through the abstraction:

```csharp
// Core/Providers/IMemoryProvider.cs
public interface IMemoryProvider
{
    ValueTask<RssMemoryReading> GetRssMemoryAsync(CancellationToken ct);
    ValueTask<HeapMetadata> GetHeapMetadataAsync(CancellationToken ct);
    bool IsAvailable { get; }
}

// Core/Providers/LinuxMemoryProvider.cs  — reads /proc/[pid]/status
// Core/Providers/MockMemoryProvider.cs   — returns configurable fake data

// Agent/Program.cs — swap based on environment
builder.Services.AddSingleton<IMemoryProvider>(
    Environment.OSVersion.Platform == PlatformID.Unix
        ? new LinuxMemoryProvider(pid)
        : new MockMemoryProvider());
```

Never reference `LinuxMemoryProvider` directly in handlers or background services.
All diagnostic logic depends only on `IMemoryProvider`.

## Vertical Slice (Feature) Structure

Organize by feature inside `Agent/Features/`. Never organize by technical type:

```
Agent/Features/
  LeakDetection/
    TriggerLeakAnalysisHandler.cs
    TriggerLeakAnalysisRequest.cs
    TriggerLeakAnalysisResponse.cs
  ManualCapture/
    CaptureSnapshotHandler.cs
    CaptureSnapshotRequest.cs
    CaptureSnapshotResponse.cs
  LiveStats/
    GetLiveStatsHandler.cs
    GetLiveStatsRequest.cs
    GetLiveStatsResponse.cs
```

## Handler Pattern (No MediatR)

All feature handlers follow this exact signature:

```csharp
public sealed class CaptureSnapshotHandler(
    ISnapshotCollector collector,
    IStorageProvider storage,
    ILogger<CaptureSnapshotHandler> logger)
{
    public async Task<Result<CaptureSnapshotResponse>> HandleAsync(
        CaptureSnapshotRequest request,
        CancellationToken ct)
    {
        // Never throw. Catch infrastructure exceptions here, return Result.Failure.
    }
}
```

Handlers are dispatched via a `FrozenDictionary` built at startup:

```csharp
// Agent/Infrastructure/Dispatch/HandlerRegistry.cs
public sealed class HandlerRegistry(IServiceProvider sp)
{
    private readonly FrozenDictionary<Type, object> _handlers = new Dictionary<Type, object>
    {
        [typeof(CaptureSnapshotRequest)] = sp.GetRequiredService<CaptureSnapshotHandler>(),
        [typeof(TriggerLeakAnalysisRequest)] = sp.GetRequiredService<TriggerLeakAnalysisHandler>(),
    }.ToFrozenDictionary();

    public THandler Resolve<TRequest, THandler>() =>
        (THandler)_handlers[typeof(TRequest)];
}
```

Register `HandlerRegistry` as Singleton. Register all handlers as Singleton if stateless.

## Result<T> Pattern

Business logic never throws. Define in `Core/Common/`:

```csharp
public readonly record struct Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public Error? Error { get; init; }

    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(Error error) => new() { IsSuccess = false, Error = error };
}

public readonly record struct Error(string Code, string Message);
```

Use at all handler boundaries. Never let `Result<T>` escape into BackgroundService
loops — unwrap and log at the orchestration level.

## ClrMD Architectural Rules

ClrMD requires exclusive attach. These rules are non-negotiable:

- **One active ClrMD session at a time.** Guard with `SemaphoreSlim(1,1)` at the
  `DotNetDiagnosticClient` level. Never allow concurrent heap analysis.
- **Always detach in `finally`.** Runtime objects (`ClrRuntime`, `DataTarget`) must
  be disposed in `finally` blocks — not `using` alone, as exceptions can leave
  the target process suspended.
- **Never store `ClrHeap` across calls.** Heap objects are snapshots — create,
  analyze, dispose within a single method scope.
- **`ArrayPool<byte>` for all diagnostic buffers.** Return in `finally`.
  Never `new byte[]` in analysis loops.
- Wrap all ClrMD operations in try/catch at `HeapDiffEngine` boundary.
  Convert to `Result<T>`. ClrMD throws on corrupt heaps — this is expected.

## BackgroundService & Circuit Breaker Pattern

`MemoryWatchdog` and `DiagnosticOrchestrator` extend `BackgroundService`.
They must never crash the host process:

```csharp
// Pattern for ExecuteAsync in all BackgroundServices
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    int consecutiveFailures = 0;
    const int CircuitBreakerThreshold = 3;
    TimeSpan sleepDuration = TimeSpan.FromMinutes(10);

    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            await DoWorkAsync(stoppingToken);
            consecutiveFailures = 0;
        }
        catch (OperationCanceledException)
        {
            break; // Clean shutdown — do not count as failure
        }
        catch (Exception ex)
        {
            consecutiveFailures++;
            Log.WatchdogFailure(logger, ex, consecutiveFailures);

            if (consecutiveFailures >= CircuitBreakerThreshold)
            {
                Log.CircuitBreakerOpen(logger, sleepDuration);
                await Task.Delay(sleepDuration, stoppingToken);
                consecutiveFailures = 0;
            }
        }

        await Task.Delay(pollingInterval, stoppingToken);
    }
}
```

Register global `UnobservedTaskException` handler in `Program.cs`:

```csharp
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Log.UnobservedTaskException(appLogger, e.Exception);
    e.SetObserved();
};
```

## Dependency Injection Registration

Register per feature/layer using `IServiceCollection` extension methods:

```csharp
// Agent/Infrastructure/Storage/StorageExtensions.cs
public static IServiceCollection AddStorageProvider(
    this IServiceCollection services,
    IConfiguration config)
{
    var provider = config["Sentinel:StorageProvider"];
    return provider switch
    {
        "S3" => services.AddSingleton<IStorageProvider, S3StorageProvider>(),
        "Azure" => services.AddSingleton<IStorageProvider, AzureBlobStorageProvider>(),
        _ => services.AddSingleton<IStorageProvider, LocalPvStorageProvider>()
    };
}
```

Lifetime rules:

- **Singleton:** Stateless services, `HandlerRegistry`, `IMemoryProvider`, storage adapters
- **Scoped:** Per-request handlers wired through Minimal API
- **Transient:** Avoid. Use only for lightweight, truly stateless factories

## Configuration / Options Pattern

```csharp
// Contracts/Options/SentinelOptions.cs
public sealed class SentinelOptions
{
    public string TargetProcessName { get; init; } = "dotnet";
    public int PollingIntervalSeconds { get; init; } = 5;
    public double RssLimitPercentage { get; init; } = 80.0;
    public double Gen2GrowthLimitMb { get; init; } = 100.0;
    public int CoolingPeriodMinutes { get; init; } = 3;
    public string StorageProvider { get; init; } = "Local";
}

// Program.cs
builder.Services.Configure<SentinelOptions>(
    builder.Configuration.GetSection("Sentinel"));
```

Environment variables override `appsettings.json` automatically via .NET configuration
hierarchy. Use `Sentinel__PollingIntervalSeconds=10` format in OpenShift.

## Observability

Serilog with LoggerMessage source generators. Enrich with `PodName` and `Namespace`
from environment variables at startup. One `Log.cs` partial class per project:

```csharp
// BAD
logger.LogInformation($"Heap diff complete: {count} types analyzed");

// GOOD
public static partial class Log
{
    [LoggerMessage(LogLevel.Information, "Heap diff complete: {TypeCount} types analyzed")]
    public static partial void HeapDiffComplete(ILogger logger, int typeCount);

    [LoggerMessage(LogLevel.Warning, "Watchdog failure #{FailureCount}")]
    public static partial void WatchdogFailure(ILogger logger, Exception ex, int failureCount);

    [LoggerMessage(LogLevel.Critical, "Circuit breaker open. Sleeping for {Duration}")]
    public static partial void CircuitBreakerOpen(ILogger logger, TimeSpan duration);
}
```

Expose `/health` and `/ready` Minimal API endpoints. `/ready` returns 503 until
`IMemoryProvider.IsAvailable` is true.

## Prohibited

- No MediatR
- No SignalR (raw WebSockets or SSE if needed)
- No Newtonsoft.Json (System.Text.Json only)
- No Data Annotations on Core or Contracts entities
- No `DateTime.Now` — always `DateTimeOffset.UtcNow`
- No `async void`
- No direct `/proc` calls outside `LinuxMemoryProvider`
- No direct ClrMD usage outside `Core/Analysis/` and `Core/Collectors/`
- No inline comments — self-documenting code + XML `///` for non-obvious public APIs only
- No `new byte[]` in diagnostic loops — `ArrayPool<byte>` only
- No `lock` in async code — `SemaphoreSlim` only
