# Current Task: 1.1 — Shared Volume Architecture Implementation

**PRD Reference:** Phase 1, Task 1.1
**Goal:** The YAML manifest (EmptyDir at `/tmp`, `shareProcessNamespace: true`) was completed in Task 0.7. This task adds the **code-side verification**: an `IDiagnosticPortLocator` that confirms the .NET runtime has created `dotnet-diagnostic-*.sock` in the shared volume, exposed via a `/ready` endpoint and a startup log.

**Layers touched:** `MemSentinel.Core` (new interface + implementations), `MemSentinel.Agent` (DI wiring, Minimal API `/ready` endpoint, Log messages, Worker startup check)

---

## Acceptance Criteria (DoD from PRD)

- [ ] The shared volume (`emptyDir` at `/tmp`) is mounted for both containers — **already done in Task 0.7** ✅
- [x] Agent code can locate the `dotnet-diagnostic-*.sock` file in `/tmp`
- [x] A "Connection Successful" log is generated upon Pod startup when the socket is found
- [x] `/ready` endpoint returns `200` when socket is found, `503` when not yet available
- [x] `dotnet build` — 0 warnings, 0 errors ✅

---

## Implementation Steps

- [x] **Step 1 — `IDiagnosticPortLocator` interface (Core/Collectors)**
  - New file: `src/MemSentinel.Core/Collectors/IDiagnosticPortLocator.cs`
  - Method: `ValueTask<string?> TryFindSocketPathAsync(CancellationToken ct)`
  - Returns the full socket path if found, `null` otherwise
  - `bool IsSupported { get; }` — false on Windows (mock mode guard)

- [x] **Step 2 — `UnixDiagnosticPortLocator` (Core/Collectors)**
  - New file: `src/MemSentinel.Core/Collectors/UnixDiagnosticPortLocator.cs`
  - Scans `/tmp/dotnet-diagnostic-*.sock` using `Directory.GetFiles("/tmp", "dotnet-diagnostic-*.sock")`
  - Returns the first match, or `null` if none found
  - `IsSupported` returns `OperatingSystem.IsLinux()`

- [x] **Step 3 — `MockDiagnosticPortLocator` (Core/Collectors)**
  - New file: `src/MemSentinel.Core/Collectors/MockDiagnosticPortLocator.cs`
  - Always returns a fake path: `/tmp/dotnet-diagnostic-mock-12345.sock`
  - `IsSupported` returns `true` (allows full pipeline on Windows dev machines)

- [x] **Step 4 — DI registration (Agent/Infrastructure/CoreExtensions.cs)**
  - Register `IDiagnosticPortLocator` alongside `IMemoryProvider`
  - `UnixDiagnosticPortLocator` on Linux, `MockDiagnosticPortLocator` on Windows

- [x] **Step 5 — LoggerMessage entries (Agent/Logging/Log.cs)**
  - `DiagnosticPortFound(ILogger, string socketPath)` — LogLevel.Information
  - `DiagnosticPortNotFound(ILogger)` — LogLevel.Warning

- [x] **Step 6 — Startup check in `Worker.cs`**
  - Inject `IDiagnosticPortLocator`
  - On first successful `ExecuteAsync` iteration, call `TryFindSocketPathAsync`
  - Log `DiagnosticPortFound` or `DiagnosticPortNotFound`
  - Store socket path in a field for use by future tasks (Task 1.3)

- [x] **Step 7 — `/ready` endpoint in `Program.cs`**
  - Add `app.MapGet("/ready", ...)` handler
  - Resolves `IDiagnosticPortLocator` from DI, calls `TryFindSocketPathAsync`
  - Returns `200 { status: "ready", socketPath: "..." }` when found
  - Returns `503 { status: "not_ready", reason: "diagnostic_port_not_found" }` when not

- [x] **Step 8 — Update `deployment.yaml` readiness probe** (optional, non-breaking)
  - Change readinessProbe path from `/health` → `/ready` so K8s gates traffic on actual socket availability

- [x] **Step 9 — `dotnet build`**
  - Run build, confirm 0 warnings, 0 errors ✅

---

## Files Created / Modified

| File | Action |
|---|---|
| `src/MemSentinel.Core/Collectors/IDiagnosticPortLocator.cs` | Create |
| `src/MemSentinel.Core/Collectors/UnixDiagnosticPortLocator.cs` | Create |
| `src/MemSentinel.Core/Collectors/MockDiagnosticPortLocator.cs` | Create |
| `src/MemSentinel.Agent/Infrastructure/CoreExtensions.cs` | Modify — add DI registration |
| `src/MemSentinel.Agent/Logging/Log.cs` | Modify — add 2 LoggerMessage methods |
| `src/MemSentinel.Agent/Worker.cs` | Modify — inject locator, startup check |
| `src/MemSentinel.Agent/Program.cs` | Modify — add `/ready` endpoint |
| `deploy/k8s/deployment.yaml` | Modify — readiness probe path (Step 8) |
