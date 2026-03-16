# PRD: MemSentinel – Cloud-Native .NET Memory Diagnostics Sidecar

**Project Name:** MemSentinel
**Version:** 1.0
**Target Environment:** OpenShift / Kubernetes (Linux Containers)
**Primary Stack:** .NET 10 (Agent), React/Next.js (Dashboard), TypeScript

---

## 1. Executive Summary

MemSentinel is a specialized diagnostic sidecar designed to solve the "Invisible Memory Leak" problem in .NET microservices. While traditional monitoring (Prometheus/Grafana) identifies that memory is high, MemSentinel identifies **why** by performing automated, low-overhead heap diffing and "Path to Root" analysis. It automates the collection of artifacts, stores them in persistent volumes/cloud storage, and provides a modern React-based dashboard for real-time and historical analysis.

---

## 2. System Architecture & Workflow

### 2.1 The Sidecar Architecture

MemSentinel runs as a companion container within the same Kubernetes Pod as the target .NET API.

- **Namespace Sharing:** `shareProcessNamespace: true` allows the sidecar to see the API’s PID and `/proc` filesystem.
- **Diagnostic Port:** A shared `EmptyDir` volume mounted at `/tmp` allows the sidecar to connect to the API's Unix Domain Socket (UDS) for `gcdump` and EventPipe streaming.
- **The Watchdog (Agent):** A .NET 10 background service that monitors thresholds and orchestrates the "Capture -> Analyze -> Upload" lifecycle.

### 2.2 Functional Workflow

1.  **Monitor:** The Agent polls RSS (Resident Set Size) from `/proc/[pid]/status` and GC stats via the .NET Diagnostic Port every 5 seconds.
2.  **Trigger:** If memory exceeds a defined threshold (e.g., 85% of the container limit) or exhibits a "Steep Climb" pattern, the workflow initiates.
3.  **Diffing Capture:**
    - **Snapshot A:** Captures an initial `.gcdump`.
    - **Cooling Period:** Waits for a configurable duration (e.g., 3 minutes).
    - **Snapshot B:** Captures a second `.gcdump`.
4.  **Analysis Engine:** Uses ClrMD to compare A and B. It identifies types with the highest "Survival Rate" and "Growth Velocity."
5.  **Persistence:** Binary dumps and JSON analysis reports are pushed to a Persistent Volume (PV) or S3-compatible storage.
6.  **Alerting:** An active "Push" is sent to Slack/Teams/Custom Webhook with a summary of the suspected leaking types.
7.  **Visualization:** The React/Next.js dashboard pulls data from the storage provider to visualize the leak.

---

## 3. Detailed Project Structure

### 3.1 `src/MemSentinel.Agent` (The .NET Sidecar)

- **`BackgroundServices/`**:
  - `MemoryWatchdog.cs`: Threshold logic and polling.
  - `DiagnosticOrchestrator.cs`: Manages the state machine of capturing and diffing.
- **`Infrastructure/`**:
  - `Storage/`: Implementations for S3, Azure Blob, and Local PV.
  - `Notifiers/`: Webhook and Messaging providers.
- **`Api/`**: Minimal API endpoints for the Dashboard to trigger manual GCs or fetch live stats.

### 3.2 `src/MemSentinel.Core` (Diagnostic Library)

- **`Analysis/`**:
  - `HeapDiffEngine.cs`: ClrMD logic to calculate object count deltas.
  - `RootChainAnalyzer.cs`: Logic to find the "Shortest Path to Root" for leaking objects.
- **`Collectors/`**:
  - `ProcessMetricsProvider.cs`: Linux `/proc` parser.
  - `DotNetDiagnosticClient.cs`: Wrapper for `Microsoft.Diagnostics.NETCore.Client`.

### 3.3 `src/MemSentinel.Dashboard` (Next.js + TypeScript)

- **Framework:** Next.js 14/15 (App Router), Tailwind CSS, Lucide Icons.
- **`components/charts/`**: High-performance time-series charts (Recharts/Visx) for memory trends.
- **`components/analysis/`**:
  - `HeapTreeMap`: Visualizes memory distribution by namespace/type.
  - `DiffTable`: Interactive table showing Type | Count Delta | Size Delta | Growth %.
- **`lib/api/`**: Typed clients for the Agent's API and Storage Provider metadata.

---

## 4. Key Functional Requirements

### 4.1 Real-time Observability

- **Managed vs Unmanaged Split:** Dashboard must display a stacked area chart showing Managed Heap (Gen 0, 1, 2, LOH, POH) vs. Native/Unmanaged memory.
- **GC Performance:** Track Pause Time (%) and GC CPU usage to distinguish between "Memory Leaks" and "GC Thrashing."

### 4.2 Automated Leak Analysis

- **Type Delta Report:** The tool must identify the top 10 types by total size increase between snapshots.
- **Ownership Tracking:** For leaking types, provide the "Retention Path" (e.g., Static Field -> ConcurrentDictionary -> MyLeakyObject).
- **LOH Fragmentation:** Report the "Free Space" percentage within the Large Object Heap.

### 4.3 Data Management & Egress

- **Cloud Persistence:** Automated upload of artifacts to S3/Azure with configurable TTL (Time-To-Live) to manage storage costs.
- **Active Push Alerts:** Webhook-based notifications containing:
  - Pod Name & Namespace.
  - Suspected Leak Type.
  - Direct link to the Dashboard's specific analysis session.

---

## 5. Technical Specifications & Constraints

| Component           | Technology / Constraint                                                      |
| ------------------- | ---------------------------------------------------------------------------- |
| **Agent Runtime**   | .NET 10 (Self-contained binary for minimal container size).                  |
| **Diagnostic Libs** | Microsoft.Diagnostics.Runtime (ClrMD), Microsoft.Diagnostics.NETCore.Client. |
| **Dashboard**       | React 18+, TypeScript, Tailwind CSS.                                         |
| **State Mgt**       | React Query (for fetching metrics) / Context API.                            |
| **Storage**         | AWS S3, Azure Blob, Kubernetes PVC.                                          |
| **Agent Footprint** | Must consume < 1.5% CPU and < 100MB RAM during idle monitoring.              |
| **Security**        | Dashboard must support API Key or JWT auth for OpenShift routes.             |

---

## 6. The Dashboard Feature Set

- [ ] **Incident Browser:** A searchable list of all auto-captured leak events across all microservices.
- [ ] **Live Profiler:** A button to "Attach Now" and stream live allocation data to the browser using WebSockets/SSE.
- [ ] **Snapshot Comparison Tool:** Allows users to manually select any two historical snapshots from storage and perform a "Deep Diff."
- [ ] **Export Center:** Ability to download raw `.dump` files for local analysis in Visual Studio or WinDbg.

---

## 7. The Deliverable: Production-Ready Roadmap

This section outlines the granular execution plan required to move MemSentinel from a conceptual PRD to a production-hardened diagnostic suite. The roadmap is divided into five logical phases, each with a strict Definition of Done (DoD).

### Phase 0: The Architecture Shield (Project Foundation)

The goal is to create a multi-project solution that separates the Linux-specific "Diagnostic Logic" from the "Web/Sidecar" plumbing, ensuring the tool can be tested on a Windows dev machine but run on a Linux OpenShift node.

**Task 0.1: Multi-Targeting Solution Structure**
Action: Create a .NET 10 Solution (MemSentinel.sln) with clear separation of concerns.

**Sub-tasks:**

- [x] **MemSentinel.Core:** Class library for the analysis engine (Target .net10.0).
- [x] **MemSentinel.Agent:** Worker Service/Web API for the sidecar process.
- [x] **MemSentinel.Contracts:** Shared DTOs and Interfaces (ensures the React Dashboard and Agent speak the same language).
- [x] **MemSentinel.Tests:** XUnit project specifically for mocking ClrMD snapshots.
      **DoD:** dotnet build succeeds on all projects with zero warnings. ✅ Done

**Task 0.2: The "Abstraction Layer" for Testability**
Action: Since you cannot run Linux /proc commands on a Windows dev machine, you must abstract the environment.

**Sub-tasks:**

- [x] Define `IMemoryProvider`: Methods like `GetRssMemory()` and `GetHeapMetadata()`.
- [x] Create `LinuxMemoryProvider` (reads /proc) and `MockMemoryProvider` (for local development).
- [x] Implement Dependency Injection (DI) in `Program.cs` to swap these based on `OperatingSystem.IsLinux()` (more reliable than `ASPNETCORE_ENVIRONMENT` in sidecar containers).
      **DoD:** The Agent starts on a Windows machine using Mock data without crashing. ✅ Done

**Task 0.3: Centralized Configuration System (The "Policy" Engine)**
Action: Build a robust `SentinelOptions` class mapped to `appsettings.json` and Environment Variables.

**Required Settings:**

- [x] **TargetProcessName:** (Default: dotnet)
- [x] **PollingIntervalSeconds:** (Default: 5)
- [x] **Thresholds:** { RssLimitPercentage: 80, Gen2GrowthLimitMB: 100 }
- [x] **CoolingPeriodMinutes:** Time to wait between Snapshot A and B.
- [x] **StorageProvider:** S3, Azure, or Local.
      **DoD:** Changing an Environment Variable in the terminal overrides the `appsettings.json` value. ✅ Done

**Task 0.4: Logging & Observability (Serilog Implementation)**
Action: Set up structured logging. When a leak happens, logs must be searchable.

**Sub-tasks:**

- [x] Integrate Serilog with the Console sink (formatted as JSON for OpenShift/Splunk/ELK).
- [x] Include "Enrichers" to automatically add `PodName` and `Namespace` to every log line.
      **DoD:** Logs output in valid JSON format to the console. ✅ Done

**Task 0.5: Global Exception Handling & "Self-Preservation"**
Action: The Sidecar must never take down the Pod if it fails.

**Sub-tasks:**

- [x] Implement a global `UnobservedTaskException` handler.
- [x] Create a "Circuit Breaker": If the Agent fails to attach to the API 3 times, it enters a "Sleep" state for 10 minutes to avoid CPU spiking.
      **DoD:** Throwing an exception in a background thread does not crash the main process. ✅ Done

**Task 0.6: Next.js + TypeScript Scaffolding (The Dashboard Base)**
Action: Initialize the frontend with strict typing.

**Sub-tasks:**

- [x] `npx create-next-app@latest` with Tailwind CSS and App Router.
- [x] Define TypeScript Interfaces that match the .NET Contracts (e.g., `IMemorySnapshot`, `ILeakReport`).
- [x] Set up Axios or React Query base hooks for the Agent's API.
      **DoD:** A basic "Health" page in React successfully fetches a "Status: OK" from the .NET Agent. ✅ Done

**Task 0.7: Docker & OpenShift "Manifest Zero"**
Action: Create the multi-stage Dockerfile and the base YAML.

**Sub-tasks:**

- [x] **Dockerfile:** Use `mcr.microsoft.com/dotnet/sdk:10.0` for building and `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` for the final image (to keep it under 100MB).
- [x] **Manifest:** Define the `ServiceAccount` and `RoleBinding` needed for a sidecar to use `SYS_PTRACE`.
      **DoD:** The Docker image builds and is under 150MB. ✅ Done

**Updated Phase 0 DoD Checklist:**

- [x] **Solution Integrity:** All projects are linked; `MemSentinel.Core` has no dependencies on `MemSentinel.Agent` (Clean Architecture).
- [x] **Environment Agnostic:** The code runs on Windows (Mock mode) and Linux (Real mode) without code changes.
- [x] **Type Safety:** TypeScript interfaces perfectly match C# DTOs.
- [x] **Shielding:** The Agent has resource limits defined and a circuit breaker implemented.

### Phase 1: The "Plumbing" & Connectivity (Foundational)

The goal of this phase is to establish the "handshake" between the Sidecar and the Target API in a Kubernetes/OpenShift environment.

- [x] **Task 1.1: Shared Volume Architecture Implementation** ✅ Done
- [x] Configure the Helm chart/Deployment YAML to mount an `EmptyDir` volume at `/tmp` for both containers.
- [x] **Sub-task:** Verify that the .NET runtime successfully creates the `dotnet-diagnostic-*.sock` file in the shared directory.
- [x] **Task 1.2: Process Namespace Integration** ✅ Done
- [x] Implement and test the `shareProcessNamespace: true` flag in the Pod spec.
- [x] **Sub-task:** Create a "Health Check" in the Agent that runs `Process.GetProcesses()` to confirm it can see the API's PID (usually PID 1 or close to it).
- [x] **Task 1.3: Unix Domain Socket (UDS) Client Wrapper** ✅ Done
- [x] Develop the `DotNetDiagnosticClient` using `Microsoft.Diagnostics.NETCore.Client`.
- [x] **Sub-task:** Implement a "Ping" mechanism to ensure the sidecar can attach to the API without permission errors (`SYS_PTRACE` capabilities check).
- [x] **Task 1.4: Linux /proc Parser (Unmanaged Memory)** ✅ Done
- [x] Build a high-performance parser for `/proc/[pid]/status` and `/proc/[pid]/smaps_rollup`.
- [x] **Sub-task:** Extract Resident Set Size (RSS), Proportional Set Size (PSS), and Virtual Memory metrics.

**Phase 1 DoD:**

- [x] Sidecar can successfully identify the API's PID.
- [x] Sidecar can read the API's RSS memory from the Linux kernel.
- [x] A "Connection Successful" log is generated upon Pod startup.

### Phase 2: The "Watchdog" & Trigger Logic (Monitoring)

The goal is to build the autonomous brain that decides when a leak is occurring.

- [x] **Task 2.1: Sliding Window Metrics Engine** ✅ Done
- [x] Implement an in-memory time-series buffer (e.g., last 60 minutes of data) to calculate memory growth velocity.
- [x] **Sub-task:** Distinguish between "Normal Growth" (Gen 0 allocations) and "Suspected Leak" (Gen 2/LOH growth).
- [x] **Task 2.2: Multi-Threshold Trigger System** ✅ Done
- [x] **Hard Threshold:** Trigger at a fixed percentage (e.g., 85% of RAM limit).
- [x] **Velocity Threshold:** Trigger if memory grows by $X\%$ over $Y$ minutes without a corresponding drop.
- [x] **Task 2.3: TestTarget API & Docker Compose Sidecar Simulation** ✅ Done
- [ ] Create `src/MemSentinel.TestTarget/` — a standalone .NET 10 Minimal API with controllable memory leak endpoints.
- [ ] **`POST /leak/managed`** — accumulates many small managed objects (strings, byte arrays, dictionary entries) into static collections on each call, promoting objects to Gen2/LOH and holding them alive across GCs.
- [ ] **`POST /leak/unmanaged`** — creates leaked `HttpClient` instances and `GCHandle`-pinned byte buffers per call to simulate unmanaged/native memory growth.
- [ ] **`POST /leak/reset`** — clears all static collections and releases GC handles, dropping RSS so the velocity signal resets cleanly.
- [ ] **`GET /health`** — returns `200 OK` for Docker Compose health checks.
- [ ] Add `MemSentinel.TestTarget` to `MemSentinel.slnx` for solution-level `dotnet build`.
- [ ] Create `docker-compose.yml` at repo root wiring `target-api` and `agent` as a true sidecar pair:
  - `pid: "service:target-api"` on the agent container (shared PID namespace).
  - Shared `/tmp` volume (EmptyDir equivalent) for UDS socket.
  - Agent thresholds tuned low (`ContainerMemoryLimitMb: 256`, `RssLimitPercentage: 50`, `VelocityThresholdMbPerMinute: 1`) so triggers fire within seconds of calling `/leak/managed`.
- [ ] **Sub-task:** Verify in Docker Compose that the Agent logs `TargetProcessFound` with the TestTarget's real PID, `TriggerFired` after a few `/leak/managed` calls, and that RSS drops are reflected after `/leak/reset`.

**Phase 2 DoD:**

- [x] Agent logs `TriggerFired` when RSS velocity exceeds configured threshold.
- [x] Hard threshold and velocity threshold evaluate independently and combinatorially.
- [x] Docker Compose stack boots and agent attaches to TestTarget as a true sidecar.

### Phase 3: The "Diagnostic Engine" (Capture, Analyze & Store)

The goal is to close the loop from trigger detection to actionable output: capture two heap snapshots via EventPipe/gcdump, diff them with ClrMD, and persist the results to local storage so the Agent's API can serve them.

**Task 3.1: EventPipe Live GC Metrics** ⬜ Pending

Replace the zeroed-out `HeapMetadata` returned by `LinuxMemoryProvider.GetHeapMetadataAsync` with real values streamed from the target process via EventPipe.

**Sub-tasks:**

- [ ] Define `IGCMetricsProvider` in `MemSentinel.Core/Collectors/` with `ValueTask<HeapMetadata> GetAsync(CancellationToken ct)`.
- [ ] Implement `EventPipeGCMetricsProvider` using `DiagnosticsClient.StartEventPipeSession` with `GCKeyword` events (keyword `0x1`, level `Verbose`).
- [ ] Subscribe to `GC/HeapStats` events and extract Gen0/Gen1/Gen2/LOH/POH heap sizes into `HeapMetadata`.
- [ ] Register `EventPipeGCMetricsProvider` as `IGCMetricsProvider` singleton in Agent DI (only when `IsLinux()`; keep `NullGCMetricsProvider` for Windows/Mock mode).
- [ ] Wire `IGCMetricsProvider` into `Worker.DoWorkAsync` — replace `memoryProvider.GetHeapMetadataAsync` call with the new provider.
- [ ] Add `[LoggerMessage]` entries: `GCHeapStats` (Info) logging Gen0/Gen1/Gen2/LOH sizes.

**DoD:** `GrowthVelocity.ManagedLeakMbPerMinute` is non-zero when the TestTarget `/leak/managed` endpoint is called; logs show real Gen2/LOH values.

---

**Task 3.2: DiagnosticTrigger Channel & Worker Wiring** ⬜ Pending

Decouple trigger detection from diagnostic orchestration using `System.Threading.Channels`.

**Sub-tasks:**

- [ ] Define `DiagnosticTrigger` as a `readonly record struct` in `MemSentinel.Contracts/` with fields: `TriggerReason Reason`, `DateTimeOffset TriggeredAt`, `double CurrentRssMb`, `double VelocityMbPerMinute`.
- [ ] Register `Channel<DiagnosticTrigger>` as singleton in `Program.cs`: `BoundedChannelOptions(4)` with `DropOldest`, `SingleReader = true`, `SingleWriter = true`. Register `Writer` and `Reader` separately.
- [ ] Inject `ChannelWriter<DiagnosticTrigger>` into `Worker` — replace `Log.TriggerFired` fire-and-forget with `TryWrite` to the channel.
- [ ] Create `DiagnosticOrchestrator` as a `BackgroundService` in `MemSentinel.Agent/` with `ChannelReader<DiagnosticTrigger>` dependency. Loop with `ReadAllAsync`.
- [ ] `DiagnosticOrchestrator.ExecuteAsync` must implement the circuit breaker pattern (same as `Worker`).
- [ ] Add `[LoggerMessage]` entries: `OrchestratorTriggerReceived` (Info), `OrchestratorBusy` (Warning, when a trigger arrives while a session is in progress).

**DoD:** `Worker` writes to channel on trigger; `DiagnosticOrchestrator` receives and logs it; concurrent triggers are dropped via `DropOldest`; build passes 0 errors.

---

**Task 3.3: GCDump Capture Engine** ⬜ Pending

Implement the gcdump capture mechanism using `Microsoft.Diagnostics.NETCore.Client`.

**Sub-tasks:**

- [ ] Define `IGCDumpCollector` in `MemSentinel.Core/Collectors/` with `Task<Result<string>> CaptureAsync(string outputPath, CancellationToken ct)` — returns the file path of the written `.gcdump` file.
- [ ] Implement `EventPipeGCDumpCollector` using `DiagnosticsClient` EventPipe session with `GCHeapDumpKeyword` (`0x100000`). Write the raw event stream to the output path.
- [ ] Guard with `SemaphoreSlim(1,1)` — only one capture at a time. Return `Result.Failure("CAPTURE_BUSY")` if already running.
- [ ] Wrap all `DiagnosticsClient` calls in try/catch; return `Result.Failure("CAPTURE_FAILED", ex.Message)` on exception.
- [ ] Ensure `ArrayPool<byte>` is used for all intermediate read buffers; return in `finally`.
- [ ] Add `[LoggerMessage]` entries: `GCDumpStarted` (Info), `GCDumpComplete` (Info, include file size bytes), `GCDumpFailed` (Error).

**DoD:** `EventPipeGCDumpCollector.CaptureAsync` produces a valid `.gcdump` file on disk when called against the running TestTarget; `dotnet-gcdump` CLI can open the file without errors.

---

**Task 3.4: HeapDiff Engine** ⬜ Pending

Parse two `.gcdump` files with ClrMD and compute the object count/size delta between them.

**Sub-tasks:**

- [ ] Define `IHeapDiffEngine` in `MemSentinel.Core/Analysis/` with `Task<Result<HeapDiffReport>> DiffAsync(string pathA, string pathB, CancellationToken ct)`.
- [ ] Define `HeapDiffReport` as a `readonly record struct` in `MemSentinel.Contracts/` with: `DateTimeOffset AnalyzedAt`, `IReadOnlyList<TypeDelta> TopGrowingTypes` (top 20 by size delta), `long TotalObjectDelta`, `long TotalBytesDelta`, `double LohFreePercent`.
- [ ] Define `TypeDelta` as a `readonly record struct`: `string TypeName`, `int CountA`, `int CountB`, `long BytesA`, `long BytesB`.
- [ ] Implement `HeapDiffEngine` using `DataTarget.LoadDump` for both paths. Enumerate objects once per dump (single-threaded). Use `string.Intern` for type names. Pre-size accumulation dictionaries with `capacity: 10_000`. Filter objects below 85 bytes (LOH boundary skip for small objects).
- [ ] Parallelize only post-collection aggregation (join the two dictionaries by type name) — never parallelize heap enumeration.
- [ ] Compute `LohFreePercent` from `ClrHeap.GetLohFreeRegions()`.
- [ ] Always dispose `ClrRuntime` and `DataTarget` in `finally` blocks (not just `using`).
- [ ] Add `[LoggerMessage]` entries: `HeapDiffStarted` (Info), `HeapDiffComplete` (Info, TypeCount + TotalBytesDelta), `HeapDiffFailed` (Error).
- [ ] 100% test coverage required on `HeapDiffEngine` — use `FakeSnapshotBuilder` for synthetic gcdump pairs.

**DoD:** After calling `/leak/managed` 10 times on TestTarget, `HeapDiffReport.TopGrowingTypes` contains `System.Byte[]` or `System.String` with a positive `CountB - CountA`; `LohFreePercent` is populated.

---

**Task 3.5: DiagnosticOrchestrator State Machine** ⬜ Pending

Wire the full capture → diff → persist lifecycle into `DiagnosticOrchestrator` as an explicit state machine.

**Sub-tasks:**

- [ ] Define `OrchestratorState` enum: `Idle`, `CapturingA`, `Cooling`, `CapturingB`, `Analyzing`, `Persisting`, `Failed`.
- [ ] Expose `OrchestratorState State { get; private set; }` as an `internal` property (visible via `[InternalsVisibleTo("MemSentinel.UnitTests")]`).
- [ ] Implement `RunDiagnosticCycleAsync(DiagnosticTrigger trigger, CancellationToken ct)`:
  1. `Idle → CapturingA`: Call `IGCDumpCollector.CaptureAsync` → write Snapshot A path.
  2. `CapturingA → Cooling`: `Task.Delay(CoolingPeriodMinutes, ct)` from `SentinelOptions`.
  3. `Cooling → CapturingB`: Call `IGCDumpCollector.CaptureAsync` → write Snapshot B path.
  4. `CapturingB → Analyzing`: Call `IHeapDiffEngine.DiffAsync(pathA, pathB, ct)`.
  5. `Analyzing → Persisting`: Call `IStorageProvider.SaveSessionAsync(session, ct)`.
  6. `Persisting → Idle`: Clean up temp files.
- [ ] On any `Result.Failure` or exception: transition to `Failed`, log with `[LoggerMessage]`, increment failure counter. After 3 consecutive failures, enter circuit breaker sleep.
- [ ] On `OperationCanceledException`: transition to `Idle` cleanly — do not count as failure.
- [ ] Add state transition logging with `[LoggerMessage]` for each transition (one message per transition, Info level).

**DoD:** Full cycle visible in Docker Compose logs: `OrchestratorTriggerReceived → CapturingA → Cooling → CapturingB → Analyzing → Persisting → Idle`. State machine tests cover all 7 states and 3-failure circuit breaker.

---

**Task 3.6: Local Storage + Session Registry + API** ⬜ Pending

Persist diagnostic sessions to the local filesystem and expose them via Minimal API endpoints.

**Sub-tasks:**

- [ ] Define `IStorageProvider` in `MemSentinel.Core/` with: `Task<Result<string>> SaveSessionAsync(DiagnosticSession session, CancellationToken ct)`, `Task<Result<DiagnosticSession>> GetSessionAsync(Guid id, CancellationToken ct)`, `Task<Result<IReadOnlyList<DiagnosticSession>>> ListSessionsAsync(CancellationToken ct)`.
- [ ] Define `DiagnosticSession` in `MemSentinel.Contracts/` with: `Guid Id`, `DateTimeOffset StartedAt`, `DateTimeOffset CompletedAt`, `TriggerReason TriggerReason`, `string SnapshotAPath`, `string SnapshotBPath`, `HeapDiffReport? DiffReport`.
- [ ] Implement `LocalPvStorageProvider`: write `{session.Id}/session.json` (System.Text.Json, `JsonSerializerOptions` from DI) and copy `.gcdump` files into `{session.Id}/` subdirectory. Base path from `SentinelOptions.LocalStoragePath` (default: `/data/memsentinel`).
- [ ] Register `LocalPvStorageProvider` as `IStorageProvider` singleton when `StorageProvider == "Local"` in `StorageExtensions.cs`.
- [ ] Build in-memory `SessionRegistry` (`ConcurrentDictionary<Guid, DiagnosticSession>`, capacity 64) that caches sessions for fast API reads — populated at startup by scanning storage and on each new session write.
- [ ] Add Minimal API endpoints in `MemSentinel.Agent/Api/`:
  - `GET /sessions` — returns `IReadOnlyList<DiagnosticSession>` from `SessionRegistry`.
  - `GET /sessions/{id}` — returns single `DiagnosticSession` or 404.
  - `GET /sessions/{id}/diff` — returns `HeapDiffReport` or 404 if not yet analyzed.
- [ ] Add `[LoggerMessage]` entries: `SessionSaved` (Info, Id + path), `SessionLoadFailed` (Warning).

**DoD:** After a full orchestrator cycle completes, `GET /sessions` returns the session with non-null `DiffReport`; the `.gcdump` files are present in the local volume; `dotnet build` 0 errors.

---

**Phase 3 DoD:**

- [ ] `ManagedLeakMbPerMinute` is non-zero in Docker Compose logs after calling `/leak/managed`.
- [ ] Full diagnostic cycle (`CapturingA → Cooling → CapturingB → Analyzing → Persisting → Idle`) completes without error against TestTarget.
- [ ] `GET /sessions/{id}/diff` returns a `HeapDiffReport` with at least one entry in `TopGrowingTypes`.
- [ ] `HeapDiffEngine` has 100% test coverage via `FakeSnapshotBuilder`.
- [ ] `DiagnosticOrchestrator` state machine transitions are covered by unit tests.
- [ ] `dotnet build` 0 warnings, 0 errors.
