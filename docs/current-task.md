# Current Task: 1.3 — Unix Domain Socket (UDS) Client Wrapper

**PRD Reference:** Phase 1, Task 1.3
**Goal:** Wrap `Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient` behind `IDotNetDiagnosticClient`. Implement a "Ping" that calls `GetProcessInfo()` to prove the sidecar can attach to the API's diagnostic port without `SYS_PTRACE` errors. This establishes the exclusive-access pattern (`SemaphoreSlim`) that all future heap-analysis tasks will inherit.

**Branch:** `task/1.3-uds-client` (cut from `phase/1-plumbing`)

**Layers touched:** `MemSentinel.Core` (new interface + implementations, new NuGet), `MemSentinel.Agent` (DI wiring, endpoint, log messages, Worker startup check)

---

## Acceptance Criteria (DoD from PRD)

- [x] `IDotNetDiagnosticClient` wraps `Microsoft.Diagnostics.NETCore.Client`
- [x] `PingAsync()` returns `Result<DiagnosticConnectionInfo>` — never throws
- [x] All `DiagnosticsClient` calls guarded by `SemaphoreSlim(1,1)`
- [x] `SYS_PTRACE` error surfaces as `Result.Failure` with code `"PTRACE_DENIED"`
- [x] Startup log confirms attach success or failure
- [x] `GET /health/diagnostic-port` returns `200` on success, `503` on failure
- [x] `dotnet build` — 0 warnings, 0 errors ✅

---

## Implementation Steps

- [x] **Step 1 — `DiagnosticConnectionInfo` record (Core/Collectors)**
  - New file: `src/MemSentinel.Core/Collectors/DiagnosticConnectionInfo.cs`
  - `readonly record struct DiagnosticConnectionInfo(int Pid, string RuntimeVersion, string CommandLine)`

- [x] **Step 2 — `IDotNetDiagnosticClient` interface (Core/Collectors)**
  - New file: `src/MemSentinel.Core/Collectors/IDotNetDiagnosticClient.cs`
  - `bool IsSupported { get; }`
  - `ValueTask<Result<DiagnosticConnectionInfo>> PingAsync(CancellationToken ct)`

- [x] **Step 3 — Add NuGet package to Core**
  - `dotnet add src/MemSentinel.Core package Microsoft.Diagnostics.NETCore.Client`

- [x] **Step 4 — `DotNetDiagnosticClient` implementation (Core/Collectors)**
  - New file: `src/MemSentinel.Core/Collectors/DotNetDiagnosticClient.cs`
  - Constructor: `(IProcessLocator processLocator, IOptions<SentinelOptions> options)`
  - `SemaphoreSlim _gate = new(1, 1)` — guards all `DiagnosticsClient` calls
  - `PingAsync`: await gate → `Task.Run` wraps `new DiagnosticsClient(pid).GetProcessInfo()` → map to `DiagnosticConnectionInfo` → `Result.Success`
  - Catches `UnauthorizedAccessException` → `Result.Failure("PTRACE_DENIED", ...)`
  - Catches all other exceptions → `Result.Failure("ATTACH_FAILED", ...)`
  - Always releases gate in `finally`
  - `IsSupported` returns `OperatingSystem.IsLinux()`

- [x] **Step 5 — `MockDotNetDiagnosticClient` (Core/Collectors)**
  - New file: `src/MemSentinel.Core/Collectors/MockDotNetDiagnosticClient.cs`
  - Returns `Result.Success(new DiagnosticConnectionInfo(Pid: 1, RuntimeVersion: "mock-10.0", CommandLine: "dotnet mock"))`
  - `IsSupported` returns `true`

- [x] **Step 6 — DI registration (Agent/Infrastructure/CoreExtensions.cs)**
  - Register `IDotNetDiagnosticClient`: `DotNetDiagnosticClient` on Linux, `MockDotNetDiagnosticClient` on Windows
  - `DotNetDiagnosticClient` is Singleton (holds the `SemaphoreSlim`)

- [x] **Step 7 — LoggerMessage entries (Agent/Logging/Log.cs)**
  - `DiagnosticClientConnected(ILogger, int pid, string runtimeVersion)` — LogLevel.Information
  - `DiagnosticClientFailed(ILogger, Exception, string errorCode)` — LogLevel.Warning

- [x] **Step 8 — Startup check in `Worker.cs`**
  - Inject `IDotNetDiagnosticClient`
  - After process-locator check: call `PingAsync()`
  - Log `DiagnosticClientConnected` or `DiagnosticClientFailed`
  - Store `DiagnosticConnectionInfo?` in a field for future tasks

- [x] **Step 9 — `GET /health/diagnostic-port` endpoint (Agent/Program.cs)**
  - Resolves `IDotNetDiagnosticClient`, calls `PingAsync()`
  - Returns `200 { status: "connected", pid, runtimeVersion, commandLine }` on success
  - Returns `503 { status: "failed", errorCode, message }` on failure

- [x] **Step 10 — `dotnet build`**
  - Run build, confirm 0 warnings, 0 errors ✅

---

## Files Created / Modified

| File | Action |
|---|---|
| `src/MemSentinel.Core/Collectors/DiagnosticConnectionInfo.cs` | Create |
| `src/MemSentinel.Core/Collectors/IDotNetDiagnosticClient.cs` | Create |
| `src/MemSentinel.Core/Collectors/DotNetDiagnosticClient.cs` | Create |
| `src/MemSentinel.Core/Collectors/MockDotNetDiagnosticClient.cs` | Create |
| `src/MemSentinel.Core/MemSentinel.Core.csproj` | Modify — add NuGet |
| `src/MemSentinel.Agent/Infrastructure/CoreExtensions.cs` | Modify — add DI registration |
| `src/MemSentinel.Agent/Logging/Log.cs` | Modify — 2 new LoggerMessage methods |
| `src/MemSentinel.Agent/Worker.cs` | Modify — inject client, startup ping |
| `src/MemSentinel.Agent/Program.cs` | Modify — add `/health/diagnostic-port` |
