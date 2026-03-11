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
- [ ] **Task 1.3: Unix Domain Socket (UDS) Client Wrapper**
- [ ] Develop the `DotNetDiagnosticClient` using `Microsoft.Diagnostics.NETCore.Client`.
- [ ] **Sub-task:** Implement a "Ping" mechanism to ensure the sidecar can attach to the API without permission errors (`SYS_PTRACE` capabilities check).
- [ ] **Task 1.4: Linux /proc Parser (Unmanaged Memory)**
- [ ] Build a high-performance parser for `/proc/[pid]/status` and `/proc/[pid]/smaps_rollup`.
- [ ] **Sub-task:** Extract Resident Set Size (RSS), Proportional Set Size (PSS), and Virtual Memory metrics.

**Phase 1 DoD:**

- [ ] Sidecar can successfully identify the API's PID.
- [ ] Sidecar can read the API's RSS memory from the Linux kernel.
- [ ] A "Connection Successful" log is generated upon Pod startup.

### Phase 2: The "Watchdog" & Trigger Logic (Monitoring)

The goal is to build the autonomous brain that decides when a leak is occurring.

- [ ] **Task 2.1: Sliding Window Metrics Engine**
- [ ] Implement an in-memory time-series buffer (e.g., last 60 minutes of data) to calculate memory growth velocity.
- [ ] **Sub-task:** Distinguish between "Normal Growth" (Gen 0 allocations) and "Suspected Leak" (Gen 2/LOH growth).
- [ ] **Task 2.2: Multi-Threshold Trigger System**
- [ ] **Hard Threshold:** Trigger at a fixed percentage (e.g., 85% of RAM limit).
- [ ] **Velocity Threshold:** Trigger if memory grows by $X\%$ over $Y$ minutes without a corresponding drop.
