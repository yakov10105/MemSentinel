# Current Task: 1.2 — Process Namespace Integration

**PRD Reference:** Phase 1, Task 1.2
**Goal:** `shareProcessNamespace: true` is already in `deployment.yaml` (Task 0.7). This task adds the code-side verification: an `IProcessLocator` abstraction that discovers the target .NET API's PID via `Process.GetProcesses()`, exposed via a `/health/processes` endpoint and a startup log. Also cleans up the duplicate process-discovery logic in `CoreExtensions.cs`.

**Branch:** `task/1.2-process-namespace` (cut from `phase/1-plumbing`)

**Layers touched:** `MemSentinel.Core` (new interface + implementations), `MemSentinel.Agent` (DI wiring, endpoint, log messages, Worker startup check)

---

## Acceptance Criteria (DoD from PRD)

- [ ] `shareProcessNamespace: true` in Pod spec confirmed working — **already done in 0.7** ✅
- [x] Sidecar can successfully identify the API's PID via `Process.GetProcesses()`
- [x] A log is generated on startup reporting whether the target process is visible
- [x] `/health/processes` endpoint returns PID + process name when visible, 503 when not
- [x] `dotnet build` — 0 warnings, 0 errors ✅

---

## Implementation Steps

- [x] **Step 1 — `ProcessInfo` record (Core/Collectors)**
  - New file: `src/MemSentinel.Core/Collectors/ProcessInfo.cs`
  - `readonly record struct ProcessInfo(int Pid, string ProcessName)`

- [x] **Step 2 — `IProcessLocator` interface (Core/Collectors)**
  - New file: `src/MemSentinel.Core/Collectors/IProcessLocator.cs`
  - `bool IsSupported { get; }`
  - `ValueTask<ProcessInfo?> FindTargetAsync(string processName, CancellationToken ct)`

- [x] **Step 3 — `SystemProcessLocator` (Core/Collectors)**
  - New file: `src/MemSentinel.Core/Collectors/SystemProcessLocator.cs`
  - Uses `Process.GetProcessesByName(processName)` — works on Linux with `shareProcessNamespace`
  - Returns the first match as `ProcessInfo`, or `null` if none found
  - `IsSupported` returns `true` (works on both OS; real value only on Linux with shared namespace)

- [x] **Step 4 — `MockProcessLocator` (Core/Collectors)**
  - New file: `src/MemSentinel.Core/Collectors/MockProcessLocator.cs`
  - Always returns `ProcessInfo(Pid: 1, ProcessName: "dotnet")`
  - `IsSupported` returns `true`

- [x] **Step 5 — DI registration + refactor (Agent/Infrastructure/CoreExtensions.cs)**
  - Register `IProcessLocator`: `SystemProcessLocator` on Linux, `MockProcessLocator` on Windows
  - Refactor `IMemoryProvider` factory to resolve `IProcessLocator` from `sp` instead of duplicating `Process.GetProcessesByName` inline

- [x] **Step 6 — LoggerMessage entries (Agent/Logging/Log.cs)**
  - `TargetProcessFound(ILogger, int pid, string processName)` — LogLevel.Information
  - `TargetProcessNotFound(ILogger, string processName)` — LogLevel.Warning

- [x] **Step 7 — Startup check in `Worker.cs`**
  - Inject `IProcessLocator` and `SentinelOptions`
  - After the existing `DiagnosticPortLocator` check: call `FindTargetAsync(opts.TargetProcessName)`
  - Log `TargetProcessFound` or `TargetProcessNotFound`
  - Store `ProcessInfo?` in a field for use by future tasks (Task 1.3, 1.4)

- [x] **Step 8 — `/health/processes` endpoint (Agent/Program.cs)**
  - `GET /health/processes` — resolves `IProcessLocator` + `SentinelOptions`
  - Returns `200 { status: "visible", pid, processName }` when found
  - Returns `503 { status: "not_visible", reason: "target_process_not_found", processName }` when not

- [x] **Step 9 — `dotnet build`**
  - Run build, confirm 0 warnings, 0 errors ✅

---

## Files Created / Modified

| File | Action |
|---|---|
| `src/MemSentinel.Core/Collectors/ProcessInfo.cs` | Create |
| `src/MemSentinel.Core/Collectors/IProcessLocator.cs` | Create |
| `src/MemSentinel.Core/Collectors/SystemProcessLocator.cs` | Create |
| `src/MemSentinel.Core/Collectors/MockProcessLocator.cs` | Create |
| `src/MemSentinel.Agent/Infrastructure/CoreExtensions.cs` | Modify — register IProcessLocator, refactor IMemoryProvider factory |
| `src/MemSentinel.Agent/Logging/Log.cs` | Modify — add 2 LoggerMessage methods |
| `src/MemSentinel.Agent/Worker.cs` | Modify — inject IProcessLocator, startup check |
| `src/MemSentinel.Agent/Program.cs` | Modify — add `/health/processes` endpoint |
