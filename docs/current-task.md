# Task 2.2 — Multi-Threshold Trigger System

**PRD Reference:** Phase 2, Task 2.2
**Branch:** `task/2.2-multi-threshold` (cut from `phase/2-watchdog`)
**Layers touched:** `MemSentinel.Contracts` (options), `MemSentinel.Core` (new Analysis types + evaluator), `MemSentinel.Agent` (Worker, Log), `MemSentinel.UnitTests`

---

## What This Task Builds

A `TriggerEvaluator` that consumes `RssMemoryReading` + `GrowthVelocity` (from Task 2.1)
and evaluates two independent trigger conditions:

1. **Hard Threshold** — current RSS ≥ X% of the container memory limit
2. **Velocity Threshold** — sustained growth rate ≥ Y MB/min (requires ≥ 2 samples)

**Data flow:**
```
DoWorkAsync()
  → TriggerEvaluator.Evaluate(rss, velocity, thresholds)
  → TriggerResult { Reason, CurrentRssMb, LimitMb, RssUsedPercent, Velocity }
  → Log.TriggerFired (if IsTriggered)
```

## Key Design Decisions

- **`TriggerThresholds`** — value type in Core/Analysis (keeps Core free of Contracts dependency)
- **`TriggerReason`** enum — `None | HardThreshold | VelocityThreshold | Both`
- **`TriggerResult`** — `readonly record struct` with `IsTriggered` computed property + `None` sentinel
- **`TriggerEvaluator`** — `static` class, pure math, no state, no DI (mirrors `MemoryGrowthAnalyzer`)
- Worker maps `ThresholdOptions` → `TriggerThresholds` — dependency boundary stays clean

## Acceptance Criteria

- [ ] Hard threshold fires when `currentRssMb / limitMb * 100 >= RssLimitPercentage`
- [ ] Velocity threshold fires when `RssMbPerMinute >= VelocityThresholdMbPerMinute` and `SampleCount >= 2`
- [ ] Both triggers fire simultaneously as `TriggerReason.Both`
- [ ] `TriggerFired` warning logged whenever `IsTriggered`
- [ ] All unit tests pass
- [ ] `dotnet build` — 0 warnings, 0 errors

---

## Steps

- [x] **Step 1 — Extend `ThresholdOptions` (Contracts) + `appsettings.json`**
- [x] **Step 2 — `TriggerReason` enum (Core/Analysis)**
- [x] **Step 3 — `TriggerThresholds` value type (Core/Analysis)**
- [x] **Step 4 — `TriggerResult` value type (Core/Analysis)**
- [x] **Step 5 — `TriggerEvaluator` static class (Core/Analysis)**
- [x] **Step 6 — `Log.TriggerFired` message (Agent/Logging/Log.cs)**
- [x] **Step 7 — Integrate in `Worker.DoWorkAsync` (Agent)**
- [x] **Step 8 — Unit tests (UnitTests/Analysis/TriggerEvaluatorTests.cs)**
- [x] **Step 9 — Build, test, update PRD**

---

## Files to Create / Modify

| File | Action |
|---|---|
| `src/MemSentinel.Contracts/Options/SentinelOptions.cs` | Modify — add 2 threshold options |
| `src/MemSentinel.Agent/appsettings.json` | Modify — add new defaults |
| `src/MemSentinel.Core/Analysis/TriggerReason.cs` | Create |
| `src/MemSentinel.Core/Analysis/TriggerThresholds.cs` | Create |
| `src/MemSentinel.Core/Analysis/TriggerResult.cs` | Create |
| `src/MemSentinel.Core/Analysis/TriggerEvaluator.cs` | Create |
| `src/MemSentinel.Agent/Logging/Log.cs` | Modify — 1 new log method |
| `src/MemSentinel.Agent/Worker.cs` | Modify — integrate evaluator |
| `tests/MemSentinel.UnitTests/Analysis/TriggerEvaluatorTests.cs` | Create |
| `docs/prd.md` | Modify — Task 2.2 ✅ Done |
