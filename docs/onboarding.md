# Project Onboarding

> Referenced on-demand. Load with: `@docs/onboarding.md`

## Prerequisites

- .NET 10 SDK (`dotnet --version` should be `10.x.x`)
- Node.js 20+ (for Dashboard development)
- Docker Desktop (for local container testing)
- Access to a Kubernetes cluster or local `kind`/`minikube` for integration testing

## Setup Steps

### Prerequisites

| Tool | Minimum Version | Check |
|---|---|---|
| .NET SDK | 10.x | `dotnet --version` |
| Node.js | 20.x | `node --version` |
| Docker Desktop | Any recent | `docker --version` |
| Git | Any | `git --version` |

For Kubernetes integration testing: `kind`, `minikube`, or access to a real cluster.

### 1. Clone and Build

```bash
git clone <repo-url>
cd MemSentinel
dotnet restore
dotnet build
```

Expected: zero warnings, zero errors. If warnings appear, fix before proceeding — the build hook in `.claude/settings.json` enforces a clean build after every file edit.

### 2. Run Tests (Windows / Local Dev)

The agent runs in **Mock mode** on non-Linux environments — `IMemoryProvider` swaps to `MockMemoryProvider` automatically when `ASPNETCORE_ENVIRONMENT=Development`.

```bash
dotnet test                                          # All tests
dotnet test --filter "Category=Unit"                 # Fast unit tests only (< 100ms each)
dotnet test --filter "Category=Integration"          # SQLite in-memory integration tests
```

### 3. Agent — Local Run (Mock Mode)

```bash
# Copy the example env file
cp appsettings.Development.json.example appsettings.Development.json
# Edit appsettings.Development.json — no real secrets needed for mock mode

dotnet run --project src/MemSentinel.Agent
```

The agent will start on `http://localhost:8080`. The `/health` endpoint confirms it is running. Mock memory data will oscillate to simulate threshold triggers.

### 4. Dashboard Setup

```bash
cd src/MemSentinel.Dashboard
npm install
npm run dev
```

Dashboard runs on `http://localhost:3000`. It expects the Agent API at `http://localhost:8080` by default (set `NEXT_PUBLIC_AGENT_URL` to override).

Verify setup: the Health page should show `Status: OK` fetched from the running Agent.

### 5. Docker Build (optional local test)

```bash
docker build -f src/MemSentinel.Agent/Dockerfile -t memsentinel-agent:local .
docker run -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  memsentinel-agent:local
```

Target image size: **< 150MB** (enforced by PRD). Run `docker images memsentinel-agent:local` to verify.

### 6. First Kubernetes Deployment (kind/minikube)

```bash
# Apply the sidecar manifest (see docs/architecture.md for full spec)
kubectl apply -f deploy/manifests/pod-with-sidecar.yaml

# Verify sidecar can see target PID
kubectl logs <pod-name> -c memsentinel-agent | grep "Connection Successful"
```

## Environment Variables

All variables map to `SentinelOptions` in the Agent. Environment variables override `appsettings.json`.

### Agent (`MemSentinel.Agent`)

| Variable | Description | Default | Required |
|---|---|---|---|
| `MEMSENTINEL_TARGET_PROCESS_NAME` | Name of the process to monitor | `dotnet` | No |
| `MEMSENTINEL_DIAGNOSTIC_SOCKET` | Path to the .NET UDS diagnostic socket | `/tmp/dotnet-diagnostic.sock` | Yes (Linux) |
| `MEMSENTINEL_POLLING_INTERVAL_SECONDS` | How often to poll `/proc` and GC stats | `5` | No |
| `MEMSENTINEL_MEMORY_THRESHOLD_PCT` | Hard threshold (% of container RAM limit) to trigger capture | `0.85` | No |
| `MEMSENTINEL_GEN2_GROWTH_LIMIT_MB` | Velocity threshold — Gen2/LOH growth in MB to trigger capture | `100` | No |
| `MEMSENTINEL_COOLING_PERIOD_MINUTES` | Wait time between Snapshot A and Snapshot B | `3` | No |
| `MEMSENTINEL_STORAGE_PROVIDER` | Storage backend: `local`, `s3`, `azureblob` | `local` | No |
| `MEMSENTINEL_STORAGE_LOCAL_PATH` | Directory for local PV storage | `/data/snapshots` | If local |
| `MEMSENTINEL_STORAGE_BUCKET` | S3 bucket name or Azure Blob container name | — | If s3/azureblob |
| `MEMSENTINEL_ALERT_WEBHOOK_URL` | Slack/Teams incoming webhook URL | — | No |
| `MEMSENTINEL_DASHBOARD_BASE_URL` | Base URL for deep links in alerts | `http://localhost:3000` | No |
| `POD_NAME` | Injected by Kubernetes downward API — used in logs and alerts | — | Kubernetes only |
| `POD_NAMESPACE` | Injected by Kubernetes downward API | — | Kubernetes only |
| `ASPNETCORE_ENVIRONMENT` | `Development` enables Mock mode (no Linux deps required) | `Production` | Dev only |

### Dashboard (`MemSentinel.Dashboard`)

| Variable | Description | Default |
|---|---|---|
| `NEXT_PUBLIC_AGENT_URL` | Base URL of the Agent's Minimal API | `http://localhost:8080` |
| `NEXT_PUBLIC_AUTH_MODE` | `none`, `apikey`, or `jwt` | `none` |
| `MEMSENTINEL_API_KEY` | API key for dashboard auth (server-side only) | — |

## Common Issues

### Diagnostic Port connection refused
**Symptom:** `DotNetDiagnosticClient` throws `SocketException` or `UnauthorizedAccessException` on connect.
**Fix:**
1. Confirm `shareProcessNamespace: true` is set in the Pod spec.
2. Confirm both containers mount the same `EmptyDir` volume at `/tmp`.
3. Confirm the target app has `DOTNET_DiagnosticPorts=/tmp/dotnet-diagnostic.sock,nosuspend,listen` set.
4. Confirm the sidecar has `SYS_PTRACE` capability in its `securityContext`.

### /proc/[pid]/status returns zero or empty
**Symptom:** `ProcessMetricsProvider.GetRssMemory()` returns 0 or throws.
**Fix:**
1. In Kubernetes: `shareProcessNamespace: true` must be set — without it, the sidecar cannot see the target's `/proc` entries.
2. In local dev on Windows: `IMemoryProvider` should resolve to `MockMemoryProvider`. If it isn't, check that `ASPNETCORE_ENVIRONMENT=Development` is set.
3. Verify you are reading from the correct PID. Use the `/health` endpoint to confirm what PID the agent has identified.

### Agent crashes the Pod on startup
**Symptom:** Sidecar container enters `CrashLoopBackOff` immediately.
**Fix:** The circuit breaker (Phase 0, Task 0.5) catches repeated attach failures. If the circuit breaker is not yet implemented, unhandled exceptions from `DotNetDiagnosticClient` will bubble up. Check logs with `kubectl logs <pod> -c memsentinel-agent --previous`.

### Docker image exceeds 150MB limit
**Symptom:** `docker images` shows the image over 150MB.
**Fix:** Ensure the Dockerfile uses `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` (not the full SDK) as the final stage. Confirm `--self-contained false` is passed to `dotnet publish`. Check for accidentally included source files.

### Dashboard shows "Failed to fetch" on Health page
**Symptom:** React dashboard cannot reach the Agent API.
**Fix:**
1. Confirm Agent is running: `curl http://localhost:8080/health`
2. Check `NEXT_PUBLIC_AGENT_URL` points to the correct host/port.
3. In Kubernetes: confirm the Agent's port (8080) is exposed via a `Service` and the Dashboard is configured with the Service's cluster DNS name.

### Tests fail on Windows with Linux-specific errors
**Symptom:** Unit or integration tests call `/proc` paths or throw `PlatformNotSupportedException`.
**Fix:** Ensure `IMemoryProvider` and `IDotNetDiagnosticClient` are mocked in all unit tests. Real implementations should only be registered in `Program.cs` based on environment. Check that no test directly instantiates `LinuxMemoryProvider`.

### `SYS_PTRACE` denied in OpenShift
**Symptom:** Sidecar cannot attach, logs show permission denied.
**Fix:** OpenShift uses Security Context Constraints (SCCs) instead of raw Linux capabilities. The `memsentinel-sa` ServiceAccount must be bound to a privileged SCC that allows `SYS_PTRACE`. Run: `oc adm policy add-scc-to-user privileged -z memsentinel-sa -n <namespace>`.

## Key Files to Understand First

1. `docs/prd.md` — product requirements and task tracker
2. `docs/architecture.md` — design decisions and system overview
3. `CLAUDE.md` — Claude Code workspace configuration
4. `src/MemSentinel.Agent/BackgroundServices/MemoryWatchdog.cs` — entry point for the monitoring loop
5. `src/MemSentinel.Core/Analysis/HeapDiffEngine.cs` — core diagnostic logic
