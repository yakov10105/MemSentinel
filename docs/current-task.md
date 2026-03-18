# Task 3.1 — EventPipe Live GC Metrics

## Goal
Replace zeroed `HeapMetadata` from `LinuxMemoryProvider.GetHeapMetadataAsync` with real
Gen0/Gen1/Gen2/LOH/POH values streamed from the target process via EventPipe.

**DoD:** `GrowthVelocity.ManagedLeakMbPerMinute` is non-zero after calling `/leak/managed`;
logs show real Gen2/LOH values.

---

## Layers Touched
- `MemSentinel.Core` — new interface + two providers
- `MemSentinel.Agent` — DI registration, Worker wiring, Log entry
- `MemSentinel.Core.csproj` — add `Microsoft.Diagnostics.Tracing.TraceEvent` NuGet

---

## Implementation Steps

- [x] **Step 1 — NuGet: add TraceEvent to Core**
  - Add `Microsoft.Diagnostics.Tracing.TraceEvent` to `MemSentinel.Core.csproj`
  - Needed for `EventPipeEventSource` to parse `GC/HeapStats` events
  - Run `dotnet build` — 0 errors

- [x] **Step 2 — Define `IGCMetricsProvider` in `Core/Collectors/`**
  - File: `src/MemSentinel.Core/Collectors/IGCMetricsProvider.cs`
  - Single method: `ValueTask<HeapMetadata> GetAsync(CancellationToken ct)`
  - Run `dotnet build` — 0 errors

- [x] **Step 3 — Implement `NullGCMetricsProvider`**
  - File: `src/MemSentinel.Core/Collectors/NullGCMetricsProvider.cs`
  - Returns `new HeapMetadata(0, 0, 0, 0, 0, DateTimeOffset.UtcNow)` — Windows/Mock fallback
  - Run `dotnet build` — 0 errors

- [x] **Step 4 — Implement `EventPipeGCMetricsProvider`**
  - File: `src/MemSentinel.Core/Collectors/EventPipeGCMetricsProvider.cs`
  - Constructor takes `int pid`; implements `IGCMetricsProvider` + `IAsyncDisposable`
  - Starts a background `Task.Run` loop on first `GetAsync` call (lazy init via `SemaphoreSlim(1,1)`)
  - Session: `DiagnosticsClient.StartEventPipeSession` with provider
    `Microsoft-Windows-DotNETRuntime`, keyword `0x1` (GCKeyword), level `Verbose`
  - Parses `GC/HeapStats` event via `EventPipeEventSource`; extracts
    `GenerationSize0/1/2/3/4` -> `HeapMetadata`
  - Stores latest value in a `volatile HeapSnapshot` wrapper (lock-free read path)
  - `DisposeAsync` cancels the background loop and stops the session
  - Run `dotnet build` — 0 errors

- [x] **Step 5 — Register in DI (`CoreExtensions.cs`)**
  - Add `IGCMetricsProvider` registration after the `IMemoryProvider` block
  - Linux: `EventPipeGCMetricsProvider(pid)` — same `pid` resolution logic as LinuxMemoryProvider
  - Non-Linux: `NullGCMetricsProvider`
  - Register as Singleton
  - Run `dotnet build` — 0 errors

- [x] **Step 6 — Wire into `Worker.DoWorkAsync`**
  - Add `IGCMetricsProvider gcMetricsProvider` to `Worker` primary constructor
  - Replace `await memoryProvider.GetHeapMetadataAsync(stoppingToken)` with
    `await gcMetricsProvider.GetAsync(stoppingToken)`
  - Add `Log.GCHeapStats(...)` call after reading heap (Info level)
  - Run `dotnet build` — 0 errors

- [x] **Step 7 — Add `GCHeapStats` LoggerMessage to `Log.cs`**
  - File: `src/MemSentinel.Agent/Logging/Log.cs`
  - Message: `"GC heap stats: Gen0={Gen0Mb:F2}MB Gen1={Gen1Mb:F2}MB Gen2={Gen2Mb:F2}MB LOH={LohMb:F2}MB POH={PohMb:F2}MB"`
  - Level: `Information`
  - Run `dotnet build` — 0 errors

- [x] **Step 8 — Final build + PRD update**
  - Run `dotnet build` — confirm 0 warnings, 0 errors
  - Update `docs/prd.md` Task 3.1 checkboxes to Done

---

## Acceptance Criteria
- [ ] `IGCMetricsProvider` defined in `Core/Collectors/`
- [ ] `EventPipeGCMetricsProvider` starts EventPipe session with GCKeyword 0x1
- [ ] `GC/HeapStats` event parsed; `HeapMetadata` populated with real Gen0-POH sizes
- [ ] `NullGCMetricsProvider` registered for non-Linux (returns all zeros)
- [ ] `Worker` uses `IGCMetricsProvider` — no longer calls `memoryProvider.GetHeapMetadataAsync`
- [ ] Log entry `GCHeapStats` emitted each poll cycle
- [ ] `dotnet build` 0 warnings, 0 errors
