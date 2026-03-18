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

- **Namespace Sharing:** `shareProcessNamespace: true` allows the sidecar to see the API's PID and `/proc` filesystem.
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

**Task 3.1: EventPipe Live GC Metrics** ✅ Done

Replace the zeroed-out `HeapMetadata` returned by `LinuxMemoryProvider.GetHeapMetadataAsync` with real values streamed from the target process via EventPipe.

**Sub-tasks:**

- [x] Define `IGCMetricsProvider` in `MemSentinel.Core/Collectors/` with `ValueTask<HeapMetadata> GetAsync(CancellationToken ct)`.
- [x] Implement `EventPipeGCMetricsProvider` using `DiagnosticsClient.StartEventPipeSession` with `GCKeyword` events (keyword `0x1`, level `Verbose`).
- [x] Subscribe to `GC/HeapStats` events and extract Gen0/Gen1/Gen2/LOH/POH heap sizes into `HeapMetadata`.
- [x] Register `EventPipeGCMetricsProvider` as `IGCMetricsProvider` singleton in Agent DI (only when `IsLinux()`; keep `NullGCMetricsProvider` for Windows/Mock mode).
- [x] Wire `IGCMetricsProvider` into `Worker.DoWorkAsync` — replace `memoryProvider.GetHeapMetadataAsync` call with the new provider.
- [x] Add `[LoggerMessage]` entries: `GCHeapStats` (Info) logging Gen0/Gen1/Gen2/LOH sizes.

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

---

### Phase 4: The Dashboard (React / Next.js UI)

The goal of this phase is to build the full React/Next.js dashboard that surfaces diagnostic data captured by the Agent. All tasks in this phase use a mock API layer so they can be developed and tested independently of Phase 3. The mock layer is toggled via `NEXT_PUBLIC_USE_MOCKS=true` and must be swappable for real Agent calls without changing component code.

---

**Task 4.1: App Shell, Routing & Mock API Layer** ⬜ Pending

Establish the application skeleton that every subsequent task builds on: layout, navigation, routing, typed API client, and the mock data infrastructure.

**Sub-tasks:**

- [ ] 1. Scaffold the persistent layout in `src/MemSentinel.Dashboard/app/layout.tsx`: left sidebar with nav links (Overview, Incidents, Compare, Profiler, Export), top header bar with a Pod / Namespace selector (static dropdown in mock mode).
- [ ] 2. Configure Next.js App Router routes: `/` (Overview), `/incidents`, `/incidents/[id]`, `/compare`, `/profiler`, `/export` — each route renders a page-level `<Suspense>` boundary with a skeleton fallback.
- [ ] 3. Define `ApiClient` in `lib/api/client.ts`: Axios instance with `baseURL` from `NEXT_PUBLIC_AGENT_URL`, 10 s timeout, response interceptor that normalises errors into `{ status, message }` shape — no raw Axios errors leak into components.
- [ ] 4. Create typed mock fixtures in `lib/api/mocks/`: `mockSessions.ts` (array of 12 `DiagnosticSession` with varied `TriggerReason` and `StartedAt`), `mockHeapDiffReport.ts` (20 `TypeDelta` entries with realistic names and byte deltas), `mockMemorySnapshot.ts` (60 data points at 5 s intervals).
- [ ] 5. Build `useMockFlag()` utility: reads `NEXT_PUBLIC_USE_MOCKS` at module initialisation and exports a boolean constant — all API hooks import this and branch on it before making a real fetch.
- [ ] 6. Set up TanStack React Query: `QueryClientProvider` in `layout.tsx`, `defaultOptions: { queries: { staleTime: 5_000, retry: 2, refetchOnWindowFocus: false } }`.
- [ ] 7. Install and configure Recharts — add it to `package.json`, verify SSR compatibility by wrapping chart components in a `dynamic(() => import(...), { ssr: false })` boundary.
- [ ] 8. Define global Tailwind design tokens in `tailwind.config.ts`: severity palette (`healthy: green-500`, `warning: amber-400`, `critical: red-600`), chart colour sequence for Gen0/Gen1/Gen2/LOH/POH, consistent spacing scale.

**DoD:** `npm run build` succeeds with 0 TypeScript errors; navigating to all six routes in `NEXT_PUBLIC_USE_MOCKS=true` mode renders a layout shell with no runtime errors; React Query DevTools visible in development mode.

---

**Task 4.2: Real-Time Memory Overview Page** ⬜ Pending

Build the live monitoring view: stacked area chart for managed vs. unmanaged memory, GC pause time chart, stat cards, and auto-refreshing data hooks.

**Sub-tasks:**

- [ ] 1. Define `MemorySnapshot` TypeScript interface in `lib/api/types.ts` matching the .NET `MemorySnapshot` contract: `{ timestamp: string; rssMb: number; heapMetadata: { gen0Mb, gen1Mb, gen2Mb, lohMb, pohMb, nativeMb, gcPausePercent } }`.
- [ ] 2. Build `useMemoryMetrics(intervalMs: number)` hook: React Query with `refetchInterval: intervalMs`; in mock mode returns a sliding window over `mockMemorySnapshot` (advances one point per refetch); in real mode calls `GET /metrics/live`.
- [ ] 3. Build `<ManagedVsUnmanagedChart />`: Recharts `AreaChart` with stacked series Gen0, Gen1, Gen2, LOH, POH (using the severity palette's chart sequence) and a separate `Area` for Native/Unmanaged in a muted colour; X-axis shows relative time (`-5m`, `-4m`, ..., `now`); Y-axis in MB with auto-domain.
- [ ] 4. Build `<GCPauseTimeChart />`: Recharts `LineChart` for `gcPausePercent` over the same time window; horizontal `ReferenceLine` at `y=10` labelled "Warning" in amber; line turns red when any point exceeds 10%.
- [ ] 5. Build `<MemoryStatsBar />`: four stat cards — Current RSS, Gen2 Size, LOH Size, GC Pause % — each showing current value, a small sparkline of the last 10 points, and a coloured delta badge (`+12 MB` / `-3 MB`).
- [ ] 6. Build `<ThresholdStatusBadge />`: pill component reading current RSS vs. configured limit; renders `Healthy` (green), `Warning` (amber, >70%), or `Critical` (red, >85%); displayed in the top header bar.
- [ ] 7. Add a "Pause / Resume" toggle button to the Overview page: when paused, `refetchInterval` is set to `false` and the button label changes to "Resume"; charts freeze on the last received data point.
- [ ] 8. Assemble `/` page: `<MemoryStatsBar />` at top full-width, `<ManagedVsUnmanagedChart />` and `<GCPauseTimeChart />` in a two-column grid below, `<ThresholdStatusBadge />` injected into the shared header via a React context slot.

**DoD:** Overview page auto-refreshes at 5 s in mock mode; pausing and resuming works without remounting charts; `<ManagedVsUnmanagedChart />` renders all five managed series stacked correctly; no TypeScript errors.

---

**Task 4.3: Incident Browser** ⬜ Pending

Build a searchable, filterable, sortable, and paginated list of all captured diagnostic sessions, acting as the primary entry point for historical analysis.

**Sub-tasks:**

- [ ] 1. Build `useSessions()` hook: React Query fetching `GET /sessions` (or `mockSessions` in mock mode); returns `DiagnosticSession[]` sorted by `StartedAt` descending; `refetchInterval: 30_000`.
- [ ] 2. Build `<SessionStatusBadge />`: maps `DiagnosticSession` state — `Analyzing` (blue spinner), `Complete` (green), `Failed` (red) — to a colour-coded pill; derives state from presence of `DiffReport` and `CompletedAt`.
- [ ] 3. Build `<IncidentRow />`: single `<tr>` showing truncated `Id` (first 8 chars with copy-to-clipboard icon), `StartedAt` formatted as relative time (`2 hours ago`), `TriggerReason`, top growing type name (first entry of `DiffReport.TopGrowingTypes` or `—` if not yet analyzed), `TotalBytesDelta` formatted as `+14.2 MB`, and `<SessionStatusBadge />`; entire row is clickable and navigates to `/incidents/[id]`.
- [ ] 4. Build `<IncidentBrowser />` table: `<thead>` with sortable column headers for `StartedAt` and `TotalBytesDelta` (chevron icons toggle ascending/descending); client-side sort state held in `useState`; renders `<IncidentRow />` per visible session.
- [ ] 5. Add debounced search bar (300 ms) above the table: filters rows by matching the search string against `TriggerReason` or top growing type name (case-insensitive).
- [ ] 6. Add filter pills below the search bar: `All | Hard Threshold | Velocity | Manual` — maps to `TriggerReason` enum values; active pill is highlighted; combining search and filter narrows results with AND logic.
- [ ] 7. Add pagination: 20 rows per page; `<PaginationControls />` component with First, Previous, Next, Last buttons and a `Page X of Y` indicator; pagination resets to page 1 when search or filter changes.
- [ ] 8. Add empty state: when the filtered result set is empty render a centred message "No incidents match your filters." with a "Clear filters" link; when `useSessions()` returns an empty array render "No incidents captured yet. Start the Agent to begin monitoring."
- [ ] 9. Assemble `/incidents` page: skeleton table (5 ghost rows) while `useSessions()` is loading; error banner if the query fails; `<IncidentBrowser />` once data is ready.

**DoD:** Incident Browser renders 12 mock sessions; search, filter pills, column sort, and pagination all function correctly on mock data; clicking a row navigates to `/incidents/[id]`; no TypeScript errors.

---

**Task 4.4: Session Detail & Heap Diff Viewer** ⬜ Pending

Build the per-session analysis page: a metadata card, LOH fragmentation gauge, and tabbed views for the diff table, treemap, and retention path placeholder.

**Sub-tasks:**

- [ ] 1. Build `useSession(id: string)` hook: React Query fetching `GET /sessions/{id}`; returns `DiagnosticSession`; in mock mode finds session by `id` in `mockSessions`.
- [ ] 2. Build `useHeapDiff(id: string)` hook: React Query fetching `GET /sessions/{id}/diff`; returns `HeapDiffReport`; query is enabled only when `session.DiffReport !== null`; in mock mode returns `mockHeapDiffReport`.
- [ ] 3. Build `<DiffTable />`: sortable table with columns `Type Name | Count A | Count B | Delta Count | Bytes A | Bytes B | Delta Bytes | Growth %`; default sort by `Delta Bytes` descending; rows where `Growth % > 100` are highlighted amber, `> 500` are highlighted red; column headers are clickable to toggle sort direction.
- [ ] 4. Build `<HeapTreeMap />`: Recharts `Treemap` where each cell is a `TypeDelta`; cell area proportional to `BytesB`; cell fill colour maps `Growth %` to a red-scale (low growth: light, `> 500%`: deep red); hovering a cell shows a tooltip with all `TypeDelta` fields formatted as `+14 MB (+320%)`.
- [ ] 5. Build `<LohFragmentationGauge />`: horizontal progress bar showing `LohFreePercent`; fill colour is green below 20%, amber 20–40%, red above 40%; label reads `LOH Free Space: 34%`; renders a `—` placeholder when `LohFreePercent` is null.
- [ ] 6. Build `<SessionMetaCard />`: detail card showing `Session ID` (full, copyable), `Started At`, `Completed At`, `Duration`, `Trigger Reason`, `Snapshot A Path`, `Snapshot B Path`; paths displayed in monospace and truncated with a tooltip showing the full path.
- [ ] 7. Build `<RetentionPathPanel />`: placeholder panel with a muted info banner reading "Retention path analysis (Root Chain Analyzer) will be available in Phase 5." — component accepts a `retentionPaths` prop typed as `RetentionPath[] | null` so it is ready to display real data without interface changes.
- [ ] 8. Assemble `/incidents/[id]` page: `<SessionMetaCard />` full-width at top; `<LohFragmentationGauge />` below it; then a `<Tabs>` component with three tabs — **Diff Table** (`<DiffTable />`), **Treemap** (`<HeapTreeMap />`), **Retention Paths** (`<RetentionPathPanel />`); skeleton loaders for each hook independently; 404 page when `useSession` returns null.

**DoD:** Session detail page renders correctly for all 12 mock sessions; `<DiffTable />` sort works on all columns; `<HeapTreeMap />` renders 20 cells with correct area proportions; tab switching has no layout shift; no TypeScript errors.

---

**Task 4.5: Snapshot Comparison Tool** ⬜ Pending

Allow the user to manually select any two historical sessions and view a joined diff comparing their `HeapDiffReport` entries side by side.

**Sub-tasks:**

- [ ] 1. Build `useCompareSession(idA: string | null, idB: string | null)` hook: calls `GET /sessions/{id}/diff` for both IDs in parallel via `Promise.all`; result is `{ reportA: HeapDiffReport | null; reportB: HeapDiffReport | null }`; query is disabled while either ID is null; in mock mode returns two differently seeded slices of `mockHeapDiffReport`.
- [ ] 2. Build `<SnapshotSelector />`: a `<select>` dropdown populated from `useSessions()`; each option displays `StartedAt (relative) — TriggerReason`; emits selected `session.Id` via `onChange`; shows a loading skeleton while sessions are fetching; includes a "None selected" empty option.
- [ ] 3. Build `<ComparisonDiffTable />`: outer-joins the two `TypeDelta[]` arrays by `TypeName` (types present in only one session show zeroed values from the other); columns are `Type Name | Delta Bytes (A) | Delta Bytes (B) | Net Change (A to B)`; rows where B grew more than A are highlighted green, rows where A grew more are highlighted amber; sortable by `Net Change`.
- [ ] 4. Build `<ComparisonSummaryBar />`: shows total bytes delta for session A, total bytes delta for session B, the net difference (B minus A), and a verdict badge — `Worse` (red, B > A), `Better` (green, B < A), `Same` (grey, within 5%); also shows the time gap between the two sessions' `StartedAt`.
- [ ] 5. Add "Swap" button between the two `<SnapshotSelector />` dropdowns: swaps `idA` and `idB` in state, causing `useCompareSession` to refetch with swapped IDs; the `<ComparisonSummaryBar />` verdict inverts accordingly.
- [ ] 6. Add "Clear" button that resets both selectors to null, hides `<ComparisonDiffTable />` and `<ComparisonSummaryBar />`, and shows the empty state.
- [ ] 7. Assemble `/compare` page: two `<SnapshotSelector />` dropdowns side by side with Swap and Clear buttons; `<ComparisonSummaryBar />` below when both sessions are selected; `<ComparisonDiffTable />` below that; empty state "Select two snapshots above to compare them." when either selector is unset.

**DoD:** Selecting two mock sessions populates `<ComparisonSummaryBar />` and `<ComparisonDiffTable />`; swap correctly reverses A and B; types present in only one session appear with zeroed values from the other; no TypeScript errors.

---

**Task 4.6: Live Profiler (SSE Streaming)** ⬜ Pending

Build the real-time allocation profiler: an SSE stream from the Agent, a circular event buffer, a live allocation rate chart, and a virtualised event log — all fully operable in mock mode without a live Agent connection.

**Sub-tasks:**

- [ ] 1. Define `AllocationEvent` TypeScript interface in `lib/api/types.ts`: `{ timestamp: string; typeName: string; sizeBytes: number; gen: 0 | 1 | 2 | 'LOH' | 'POH' }`.
- [ ] 2. Create `lib/api/mocks/mockProfilerStream.ts`: a function that emits synthetic `AllocationEvent` objects every 500 ms using `setInterval`; cycles through 8 fixed type names (`System.String`, `System.Byte[]`, etc.) with randomised sizes; accepts an `onEvent` callback and returns a `stop()` cleanup function — mirrors the real `EventSource` teardown interface.
- [ ] 3. Build `useProfilerStream(enabled: boolean)` hook: when `enabled` is true and mock mode is off, opens an `EventSource` to `GET /profiler/stream`; when mock mode is on, calls `mockProfilerStream`; accumulates events into a `useRef` circular buffer capped at 500 entries; exposes `events: AllocationEvent[]`, `isConnected: boolean`, `error: string | null`; cleans up the stream on `enabled` becoming false or component unmount.
- [ ] 4. Build `useAllocationRate(events: AllocationEvent[])` derived hook: recalculates on every render using `useMemo`; groups events from the last 1 000 ms by `typeName`; returns `{ typeName: string; bytesPerSec: number }[]` sorted descending, top 10 only.
- [ ] 5. Build `<AllocationRateChart />`: Recharts `BarChart` consuming `useAllocationRate` output; X-axis is `typeName` (truncated to 30 chars); Y-axis is `bytes/sec` formatted as `KB/s` or `MB/s`; bars colour-coded by `gen` using the design token palette; chart re-renders every 1 s via a `useInterval` hook.
- [ ] 6. Build `<AllocationEventLog />`: virtualised list using `react-window` `FixedSizeList`; each row shows `Timestamp (HH:mm:ss.SSS) | TypeName | Size (KB) | Gen`; auto-scrolls to bottom on new events; "Pause scroll" toggle stops auto-scroll without stopping the stream; row background uses low-opacity gen-colour tints.
- [ ] 7. Build `<ProfilerStatusBar />`: shows a coloured dot with label — `Connected` (green), `Disconnected` (grey), `Error: {message}` (red); a session duration timer counting up from connection time (stops on disconnect); a total events received counter.
- [ ] 8. Build `<AttachButton />`: primary button reading "Attach Now" when disconnected and "Detach" when connected; toggles the `enabled` state passed to `useProfilerStream`; in mock mode the button is fully functional; shows a disabled state with tooltip "Set NEXT_PUBLIC_AGENT_URL to connect" only when not in mock mode and the env var is unset.
- [ ] 9. Assemble `/profiler` page: `<ProfilerStatusBar />` and `<AttachButton />` in a sticky header row; two-column layout with `<AllocationRateChart />` at 60% width and `<AllocationEventLog />` at 40% width; "Attach to begin profiling" empty-state illustration when disconnected.

**DoD:** Clicking "Attach Now" in mock mode starts the synthetic stream; `<AllocationRateChart />` updates every second with accurate per-type rates; `<AllocationEventLog />` scrolls automatically and the pause-scroll toggle works without stopping the stream; clicking "Detach" stops the stream and resets the timer; no TypeScript errors.

---

**Task 4.7: Export Center** ⬜ Pending

Allow users to download raw `.gcdump` snapshot files and export analysis reports as JSON or CSV for use in external tools such as Visual Studio, WinDbg, or spreadsheets.

**Sub-tasks:**

- [ ] 1. Build `<ExportSessionSelector />`: single-selection dropdown reusing `<SnapshotSelector />` from Task 4.5; on selection, triggers `useSession(id)` and `useHeapDiff(id)` to pre-load data; shows an inline loading spinner while hooks are fetching.
- [ ] 2. Build `useGCDumpDownload(sessionId: string, snapshot: 'a' | 'b')` hook: in real mode calls `GET /sessions/{id}/snapshots/{a|b}` and returns `{ filename: string; sizeBytes: number | null; trigger: () => void }`; in mock mode synthesises a `Blob` of 1 024 random bytes with filename `snapshot-{id}-{a|b}.gcdump` and creates an `ObjectURL`.
- [ ] 3. Build `<GCDumpDownloadCard />`: card showing label (`Snapshot A` or `Snapshot B`), file path in monospace (truncated, full path in tooltip), file size formatted as MB (or `Unknown` in mock mode), and a `Download .gcdump` button; on click, tracks download progress via `XMLHttpRequest` `onprogress` and renders a progress bar beneath the button; button resets to idle after download completes or errors.
- [ ] 4. Build `exportAsJson(report: HeapDiffReport, sessionId: string): void` utility in `lib/export.ts`: serialises `HeapDiffReport` with `JSON.stringify(report, null, 2)`; triggers browser download as `heap-diff-{sessionId}.json` via `URL.createObjectURL`.
- [ ] 5. Build `exportAsCsv(report: HeapDiffReport, sessionId: string): void` utility in `lib/export.ts`: maps `TypeDelta[]` to CSV rows with header `TypeName,CountA,CountB,DeltaCount,BytesA,BytesB,DeltaBytes,GrowthPct`; triggers browser download as `heap-diff-{sessionId}.csv` via `URL.createObjectURL`.
- [ ] 6. Build `<ReportExportCard />`: card with two buttons — "Export as JSON" and "Export as CSV" — wired to the utilities above; both buttons are disabled with tooltip "Select a session first" when no session is selected; shows a `Downloaded` confirmation label for 2 s after a successful download.
- [ ] 7. Build `exportAllSessions(sessions: DiagnosticSession[]): void` utility in `lib/export.ts`: serialises the full `DiagnosticSession[]` as `sessions-export-{YYYY-MM-DD}.json` and triggers browser download.
- [ ] 8. Assemble `/export` page: `<ExportSessionSelector />` full-width at top with an "Export All Sessions (JSON)" secondary button in the page header; two-column grid below — left column contains `<GCDumpDownloadCard />` for snapshot A and B stacked vertically, right column contains `<ReportExportCard />`; empty state "Select a session above to see export options." when no session is selected.

**DoD:** Selecting a mock session populates both `<GCDumpDownloadCard />` components and `<ReportExportCard />`; JSON and CSV exports trigger browser downloads with correct content and filenames; mock `.gcdump` download shows a progress bar that reaches 100%; "Export All Sessions" downloads a JSON array of all 12 mock sessions; no TypeScript errors.

---

**Phase 4 DoD:**

- [ ] All six routes (`/`, `/incidents`, `/incidents/[id]`, `/compare`, `/profiler`, `/export`) render without runtime errors in `NEXT_PUBLIC_USE_MOCKS=true` mode.
- [ ] `npm run build` and `tsc --noEmit` both pass with 0 errors.
- [ ] `<ManagedVsUnmanagedChart />` and `<GCPauseTimeChart />` update smoothly at 5 s intervals; pausing and resuming works without remounting charts.
- [ ] Incident Browser search, filter pills, column sort, and pagination all function correctly on 12 mock sessions.
- [ ] `<DiffTable />` sort is functional on all columns; `<HeapTreeMap />` renders with correct cell areas and colour intensity mapping.
- [ ] `<ComparisonDiffTable />` correctly outer-joins two sessions' `TypeDelta[]` arrays by type name; swap reverses the verdict in `<ComparisonSummaryBar />`.
- [ ] Live Profiler mock stream connects, populates `<AllocationRateChart />` and `<AllocationEventLog />`, and disconnects cleanly; pause-scroll toggle works without interrupting the stream.
- [ ] Export Center produces valid JSON, valid CSV, and a downloadable mock `.gcdump` file for any selected mock session; "Export All Sessions" includes all 12 mock sessions.
- [ ] All React Query hooks independently handle loading, error, and empty states with appropriate skeleton or banner UI.
