# Architecture Deep Dive

> Referenced on-demand. Load with: `@docs/architecture.md`
> For quick rules, see CLAUDE.md. This document is for deeper design context.

## System Overview

MemSentinel runs as a sidecar container within the same Kubernetes Pod as a target .NET microservice. It shares the process namespace (`shareProcessNamespace: true`) to access the target's PID and `/proc` filesystem. A shared `EmptyDir` volume at `/tmp` provides the Unix Domain Socket path for the .NET Diagnostic Port.

```
[Pod]
  ├── target-api (container)        <- The .NET service being monitored
  └── memsentinel-agent (container) <- The sidecar
        ├── reads /proc/[pid]/status    (RSS, memory limits)
        ├── connects to UDS at /tmp     (gcdump, EventPipe)
        └── exposes :8080               (Dashboard API)
```

## Layer Responsibilities

| Project | Responsibility | Dependencies |
|---|---|---|
| `MemSentinel.Core` | Diagnostic logic — heap diff, root chain analysis, /proc parsing, diagnostic client wrapper | ClrMD, Microsoft.Diagnostics.NETCore.Client |
| `MemSentinel.Agent` | Sidecar host — watchdog, orchestrator, storage adapters, notifiers, Minimal API | Core, Serilog, EF Core (optional) |
| `MemSentinel.Dashboard` | React visualization — memory charts, diff table, HeapTreeMap | Next.js, Recharts, TypeScript |

## Key Design Decisions

### ADR-001: No MediatR
**Decision:** Direct handler invocation via a dispatcher registered in DI.
**Reason:** Eliminates implicit magic and reflection overhead in a latency-sensitive sidecar. Handler routing is explicit and discoverable. `FrozenDictionary<Type, IHandler>` built at startup.

### ADR-002: Result<T> Instead of Exceptions for Business Logic
**Decision:** All handlers return `Result<T>`. Business failures are values, not exceptions.
**Reason:** Exceptions are expensive (stack unwinding) and represent control flow as side effects. In a monitoring system that may encounter failures frequently (target process died, port unavailable), structured results are cleaner and more predictable.

### ADR-003: System.Text.Json Only
**Decision:** No Newtonsoft.Json.
**Reason:** STJ is allocation-efficient, supports source generators, and is the .NET 10 standard. Newtonsoft introduces unnecessary dependency weight.

### ADR-004: Pluggable Storage via Interface
**Decision:** `IStorageProvider` interface with implementations for S3, Azure Blob, and Local PV.
**Reason:** Deployment targets vary — local dev uses PV, cloud deployments use S3 or Blob. Switching storage is config-driven, not code-driven.

### ADR-005: ArrayPool for All Diagnostic Buffers
**Decision:** No `new byte[]` in diagnostic collection paths.
**Reason:** The sidecar must have minimal GC impact on the target process's shared node. Excessive allocation in the sidecar can trigger GC pauses that affect the very thing being measured.

## Orchestrator State Machine

```
IDLE
  → threshold exceeded or manual trigger
MONITORING_TRIGGERED
  → capture Snapshot A
SNAPSHOT_A_CAPTURED
  → wait cooling period (configurable)
COOLING
  → capture Snapshot B
SNAPSHOT_B_CAPTURED
  → run HeapDiffEngine
ANALYZING
  → persist results + send alert
COMPLETING
  → IDLE
```

## TODO Sections

### C4 Context Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│  Kubernetes / OpenShift Cluster                                          │
│                                                                          │
│  ┌─────────────────────────────────────────────┐                        │
│  │  Pod                                         │                        │
│  │                                              │                        │
│  │  ┌──────────────────┐  /proc (shared ns)     │                        │
│  │  │  target-api      │◄──────────────────┐   │                        │
│  │  │  (.NET service)  │                   │   │                        │
│  │  │                  │  UDS at /tmp      │   │                        │
│  │  │  dotnet-diag-    │◄──────────────┐   │   │                        │
│  │  │  *.sock          │               │   │   │                        │
│  │  └──────────────────┘               │   │   │                        │
│  │                                     │   │   │                        │
│  │  ┌──────────────────────────────────┴───┴─┐ │                        │
│  │  │  memsentinel-agent                      │ │                        │
│  │  │  (.NET 10 sidecar)                      │ │                        │
│  │  │                                         │ │                        │
│  │  │  :8080 (Minimal API for Dashboard)      │ │                        │
│  │  └────────────────────────┬────────────────┘ │                        │
│  └───────────────────────────│──────────────────┘                        │
│                              │                                           │
│  ┌───────────────────────────│──────────────────┐                        │
│  │  PersistentVolume / S3    │                  │                        │
│  │  (*.gcdump, *.json)       │                  │                        │
│  └───────────────────────────│──────────────────┘                        │
└──────────────────────────────│──────────────────────────────────────────┘
                               │
             ┌─────────────────┼──────────────────┐
             │                 │                  │
    ┌────────▼───────┐  ┌──────▼──────┐  ┌───────▼──────┐
    │  React/Next.js │  │ Slack/Teams │  │  S3 / Azure  │
    │  Dashboard     │  │  Webhook    │  │  Blob Storage │
    │  (browser)     │  │  Alerts     │  │  (artifact   │
    └────────────────┘  └─────────────┘  │   archive)   │
                                         └──────────────┘
```

**External actors:**
- **Dashboard user** — views memory trends, browses incidents, triggers manual captures via browser
- **Slack/Teams** — receives push alerts when a suspected leak is detected (pod name, leaking type, dashboard link)
- **S3 / Azure Blob / Local PV** — stores `.gcdump` binary snapshots and JSON analysis reports with configurable TTL

### Data Flow Detail

Full sequence from threshold breach to alert delivery:

```
MemoryWatchdog          DiagnosticOrchestrator      DotNetDiagnosticClient      IStorageProvider      INotifier
      │                         │                           │                          │                   │
      │ Poll /proc every 5s     │                           │                          │                   │
      │─────────────────────►   │                           │                          │                   │
      │                         │                           │                          │                   │
      │ RSS > 85% threshold      │                           │                          │                   │
      │  OR velocity trigger     │                           │                          │                   │
      │ TriggerCaptureAsync() ──►│                           │                          │                   │
      │                         │                           │                          │                   │
      │                         │── CaptureGcdumpAsync() ──►│                          │                   │
      │                         │   (Snapshot A)            │                          │                   │
      │                         │◄── Result<Snapshot> ──────│                          │                   │
      │                         │                           │                          │                   │
      │                         │── UploadAsync(snapshotA) ─────────────────────────►│                   │
      │                         │◄── Result<Uri> ──────────────────────────────────── │                   │
      │                         │                           │                          │                   │
      │                         │   [Wait CoolingPeriod]    │                          │                   │
      │                         │   default: 3 minutes      │                          │                   │
      │                         │                           │                          │                   │
      │                         │── CaptureGcdumpAsync() ──►│                          │                   │
      │                         │   (Snapshot B)            │                          │                   │
      │                         │◄── Result<Snapshot> ──────│                          │                   │
      │                         │                           │                          │                   │
      │                         │── UploadAsync(snapshotB) ─────────────────────────►│                   │
      │                         │◄── Result<Uri> ──────────────────────────────────── │                   │
      │                         │                           │                          │                   │
      │                    HeapDiffEngine.DiffAsync(A, B)   │                          │                   │
      │                         │  ► Top N types by growth velocity                    │                   │
      │                         │  ► LOH fragmentation %                               │                   │
      │                         │  ► Retention path analysis (RootChainAnalyzer)       │                   │
      │                         │                           │                          │                   │
      │                         │── UploadAsync(report.json) ───────────────────────►│                   │
      │                         │◄── Result<Uri> ──────────────────────────────────── │                   │
      │                         │                           │                          │                   │
      │                         │── SendAlertAsync(LeakAlert) ─────────────────────────────────────────►│
      │                         │   {PodName, Namespace, TopLeakingType, DashboardUrl}                   │
      │                         │◄── Result ────────────────────────────────────────────────────────────│
      │                         │                           │                          │                   │
      │◄── IDLE ────────────────│                           │                          │                   │
```

**Key timing constraints:**
- Polling interval: 5 seconds (configurable via `PollingIntervalSeconds`)
- Cooling period between A and B: 3 minutes default (configurable via `CoolingPeriodMinutes`)
- CloseAsync timeout on shutdown: 5 seconds hard limit
- Circuit breaker: 3 consecutive attach failures → 10-minute sleep

### Kubernetes Manifest Structure

Core Pod spec pattern. All production deployments must follow this skeleton:

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: my-api-with-sentinel
  namespace: my-namespace
spec:
  # Required: allows sidecar to see target's PID and /proc filesystem
  shareProcessNamespace: true

  serviceAccountName: memsentinel-sa  # needs SYS_PTRACE capability

  volumes:
    # Shared EmptyDir for the .NET diagnostic Unix Domain Socket
    - name: diagnostic-socket
      emptyDir: {}

  containers:
    # ── Target application ────────────────────────────────────────────
    - name: target-api
      image: my-org/my-api:latest
      env:
        - name: DOTNET_DiagnosticPorts
          value: /tmp/dotnet-diagnostic.sock,nosuspend,listen
      volumeMounts:
        - name: diagnostic-socket
          mountPath: /tmp

    # ── MemSentinel sidecar ───────────────────────────────────────────
    - name: memsentinel-agent
      image: my-org/memsentinel-agent:latest
      resources:
        requests:
          cpu: "50m"
          memory: "64Mi"
        limits:
          cpu: "150m"     # Must stay < 1.5% CPU at idle per PRD constraint
          memory: "100Mi" # Must stay < 100MB RAM at idle per PRD constraint
      securityContext:
        capabilities:
          add: ["SYS_PTRACE"]  # Required to attach to target process
      env:
        - name: MEMSENTINEL_DIAGNOSTIC_SOCKET
          value: /tmp/dotnet-diagnostic.sock
        - name: MEMSENTINEL_TARGET_PROCESS_NAME
          value: dotnet
        - name: MEMSENTINEL_MEMORY_THRESHOLD_PCT
          value: "0.85"
        - name: MEMSENTINEL_COOLING_PERIOD_MINUTES
          value: "3"
        - name: MEMSENTINEL_STORAGE_PROVIDER
          value: s3
        - name: MEMSENTINEL_STORAGE_BUCKET
          valueFrom:
            secretKeyRef:
              name: memsentinel-secrets
              key: storage-bucket
        - name: MEMSENTINEL_ALERT_WEBHOOK_URL
          valueFrom:
            secretKeyRef:
              name: memsentinel-secrets
              key: webhook-url
        - name: POD_NAME
          valueFrom:
            fieldRef:
              fieldPath: metadata.name
        - name: POD_NAMESPACE
          valueFrom:
            fieldRef:
              fieldPath: metadata.namespace
      volumeMounts:
        - name: diagnostic-socket
          mountPath: /tmp
      ports:
        - containerPort: 8080
          name: dashboard-api
```

**Required RBAC (ServiceAccount + RoleBinding):**

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: memsentinel-sa
  namespace: my-namespace
---
# OpenShift: grant the SYS_PTRACE privileged SCC
# Kubernetes: no cluster role needed if securityContext.capabilities is sufficient
```

**Dockerfile pattern (multi-stage, target < 150MB):**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/MemSentinel.Agent -c Release -o /app/publish \
    --self-contained false \
    -p:PublishSingleFile=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MemSentinel.Agent.dll"]
```
