# Task 2.1 — Sliding Window Metrics Engine

**PRD Reference:** Phase 2, Task 2.1
**Branch:** `task/2.1-sliding-window` (cut from `phase/2-watchdog`)
**Layers touched:** `MemSentinel.Contracts` (new option), `MemSentinel.Core` (new Analysis/ folder), `MemSentinel.Agent` (DI, Worker, endpoint, logs)

---

## What This Task Builds

A time-series buffer + growth velocity calculator that the Watchdog uses to decide
whether memory is leaking. Task 2.2 (triggers) consumes the output of this task.

**Data flow:**
```
Worker.DoWorkAsync()
  → GetRssMemoryAsync + GetHeapMetadataAsync
  → MetricsBuffer.AddAsync(MetricSample)
  → MemoryGrowthAnalyzer.Calculate(snapshot)
  → GrowthVelocity { RssMbPerMinute, ManagedLeakMbPerMinute, IsLeakSuspected }
  → Log.GrowthVelocity / Log.LeakSuspected
```

## Key Design Decisions

- **`MetricSample`** — pairs `RssMemoryReading` + `HeapMetadata` for one tick.
- **`MetricsBuffer`** — thread-safe `Queue<MetricSample>` with time-window pruning.
  Guarded by `SemaphoreSlim(1,1)`. Prunes entries older than `MetricsWindowMinutes`
  on every `AddAsync` call. Returns snapshot copies — callers never get live references.
- **`MemoryGrowthAnalyzer`** — `static` class, pure math, no state, no DI.
  Velocity = `(newest - oldest) / windowMinutes`. Leak suspected when
  Gen2 + LOH byte delta is positive (growing tenured heap = not GC pressure).
- **`GrowthVelocity`** — `readonly record struct` result with `Insufficient` sentinel
  for when < 2 samples are available.
- **Window duration is configurable** via new `MetricsWindowMinutes` option (default: 60).
  Passed as `TimeSpan` constructor param to `MetricsBuffer` (keeps Core free of Options).

## Acceptance Criteria

- [ ] Each poll tick records both RSS and heap metadata into the buffer
- [ ] Buffer prunes entries older than the configured window
- [ ] Velocity is calculated and logged every tick (once ≥ 2 samples)
- [ ] `IsLeakSuspected = true` when Gen2 + LOH byte delta > 0
- [ ] `GET /metrics/velocity` returns current velocity as JSON
- [ ] Unit tests cover: velocity math, Insufficient sentinel, pruning, leak detection logic
- [ ] `dotnet build` — 0 warnings, 0 errors
- [ ] `dotnet test` — all passing

---

## Steps

- [ ] **Step 1 — `MetricsWindowMinutes` option (Contracts/Options)**
  - Add `int MetricsWindowMinutes { get; init; } = 60` to `SentinelOptions`

- [ ] **Step 2 — `MetricSample` (Core/Analysis)**
  - `readonly record struct MetricSample(RssMemoryReading Rss, HeapMetadata Heap)`

- [ ] **Step 3 — `GrowthVelocity` (Core/Analysis)**
  - `readonly record struct GrowthVelocity(double RssMbPerMinute, double ManagedLeakMbPerMinute, TimeSpan WindowDuration, int SampleCount, bool IsLeakSuspected)`
  - Static `Insufficient` sentinel for < 2 samples

- [ ] **Step 4 — `MetricsBuffer` (Core/Analysis)**
  - `sealed class MetricsBuffer(TimeSpan window)`
  - Internal `Queue<MetricSample>` + `SemaphoreSlim(1,1)`
  - `ValueTask AddAsync(MetricSample, CancellationToken)` — enqueue + prune old entries
  - `ValueTask<IReadOnlyList<MetricSample>> GetSnapshotAsync(CancellationToken)` — returns copy

- [ ] **Step 5 — `MemoryGrowthAnalyzer` (Core/Analysis)**
  - `static class MemoryGrowthAnalyzer`
  - `GrowthVelocity Calculate(IReadOnlyList<MetricSample> samples)`
  - Velocity = RSS delta / window minutes
  - `IsLeakSuspected` = Gen2Delta + LohDelta > 0

- [ ] **Step 6 — Register `MetricsBuffer` (Agent/Infrastructure/CoreExtensions)**
  - `services.AddSingleton<MetricsBuffer>(sp => new MetricsBuffer(TimeSpan.FromMinutes(opts.MetricsWindowMinutes)))`

- [ ] **Step 7 — Update `Worker.DoWorkAsync` (Agent)**
  - Call both `GetRssMemoryAsync` and `GetHeapMetadataAsync`
  - Push `MetricSample` to `MetricsBuffer`
  - Get snapshot → `MemoryGrowthAnalyzer.Calculate` → log velocity

- [ ] **Step 8 — Log messages (Agent/Logging/Log.cs)**
  - `GrowthVelocity(ILogger, double rssMbPerMin, double managedLeakMbPerMin, int sampleCount)`
  - `LeakSuspected(ILogger, double rssMbPerMin, double managedLeakMbPerMin)` — LogLevel.Warning

- [ ] **Step 9 — `GET /metrics/velocity` endpoint (Agent/Program.cs)**
  - Resolves `MetricsBuffer`, gets snapshot, calls analyzer
  - Returns `{ rssMbPerMinute, managedLeakMbPerMinute, windowDuration, sampleCount, isLeakSuspected }`

- [ ] **Step 10 — Unit tests (UnitTests/Analysis/)**
  - `MemoryGrowthAnalyzerTests`: velocity math, Insufficient on < 2, leak suspected on Gen2 growth, not suspected on Gen0-only
  - `MetricsBufferTests`: pruning removes old entries, snapshot is a copy not live ref

- [ ] **Step 11 — Build, test, update PRD**
  - `dotnet build` → 0 warnings, 0 errors
  - `dotnet test` → all passing
  - Update `docs/prd.md` Task 2.1 → ✅ Done

---

## Files to Create / Modify

| File | Action |
|---|---|
| `src/MemSentinel.Contracts/Options/SentinelOptions.cs` | Modify — add MetricsWindowMinutes |
| `src/MemSentinel.Core/Analysis/MetricSample.cs` | Create |
| `src/MemSentinel.Core/Analysis/GrowthVelocity.cs` | Create |
| `src/MemSentinel.Core/Analysis/MetricsBuffer.cs` | Create |
| `src/MemSentinel.Core/Analysis/MemoryGrowthAnalyzer.cs` | Create |
| `src/MemSentinel.Agent/Infrastructure/CoreExtensions.cs` | Modify — register MetricsBuffer |
| `src/MemSentinel.Agent/Worker.cs` | Modify — use buffer + analyzer |
| `src/MemSentinel.Agent/Logging/Log.cs` | Modify — 2 new log methods |
| `src/MemSentinel.Agent/Program.cs` | Modify — /metrics/velocity endpoint |
| `tests/MemSentinel.UnitTests/Analysis/MemoryGrowthAnalyzerTests.cs` | Create |
| `tests/MemSentinel.UnitTests/Analysis/MetricsBufferTests.cs` | Create |
| `docs/prd.md` | Modify — Task 2.1 ✅ Done |
