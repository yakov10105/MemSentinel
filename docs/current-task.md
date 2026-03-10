# Current Task: 0.5 — Global Exception Handling & "Self-Preservation"

**PRD Reference:** Phase 0, Task 0.5
**Goal:** The sidecar must never crash the Pod. Wrap the polling loop in a circuit breaker that sleeps 10 minutes after 3 consecutive failures.
**Layer(s) touched:** Agent only

---

## Files Modified

| File | Action |
|---|---|
| `Agent/Logging/Log.cs` | Added `WatchdogFailure` and `CircuitBreakerOpen` log methods |
| `Agent/Worker.cs` | Refactored `ExecuteAsync` with try/catch circuit breaker; extracted `DoWorkAsync` |

## What Was Already Done (from Task 0.3)

- `TaskScheduler.UnobservedTaskException` handler — `Program.cs` ✅

---

## Steps

- [x] **Step 1 — `Logging/Log.cs`**
  - `WatchdogFailure(ILogger, Exception, int failureCount)` — Warning
  - `CircuitBreakerOpen(ILogger, TimeSpan duration)` — Critical

- [x] **Step 2 — `Worker.cs`**
  - `consecutiveFailures` counter + `CircuitBreakerThreshold = 3` + `CircuitBreakerSleep = 10min`
  - `DoWorkAsync` extracted as private method
  - `OperationCanceledException` → clean break (not counted as failure)
  - 3 consecutive exceptions → `CircuitBreakerOpen` log + 10-minute `Task.Delay` + counter reset
  - Polling delay remains outside try block

- [x] **Step 3 — `dotnet build`**
  - 0 warnings, 0 errors ✅

---

## Acceptance Criteria (DoD from PRD)

- Exception in polling loop does not crash the host process ✅
- After 3 consecutive failures: `CircuitBreakerOpen` logged + worker sleeps 10 minutes ✅
- `dotnet build` — 0 warnings, 0 errors ✅
