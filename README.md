# MemSentinel

A cloud-native .NET 10 memory diagnostics sidecar for Kubernetes and OpenShift. MemSentinel identifies **why** memory is high in .NET microservices — not just that it is — by performing automated heap diffing and path-to-root analysis via ClrMD.

## How It Works

MemSentinel runs as a companion container in the same Pod as your target .NET API:

1. **Monitor** — polls RSS/PSS from `/proc/[pid]/status` and GC stats via the .NET Diagnostic Port every 5 seconds
2. **Trigger** — fires when RSS exceeds a configured hard threshold (% of container limit) or shows sustained growth velocity (MB/min)
3. **Diff** — captures Snapshot A, waits a cooling period, captures Snapshot B, then compares heap object counts and sizes with ClrMD
4. **Report** — pushes a Slack/Teams/webhook alert with the suspected leaking types and a direct link to the dashboard
5. **Visualize** — the React/Next.js dashboard shows memory trends, heap treemaps, and type-level diff tables

## Current Status

| Phase | Description                                                                                                         | Status     |
| ----- | ------------------------------------------------------------------------------------------------------------------- | ---------- |
| 0     | Project foundation, abstractions, configuration, logging, Docker                                                    | ✅ Done    |
| 1     | Sidecar plumbing: `/proc` parser, UDS client, PID detection                                                         | ✅ Done    |
| 2     | Watchdog: sliding window metrics, multi-threshold trigger system, Docker Compose sidecar simulation                 | ✅ Done    |
| 3     | Diagnostic engine: EventPipe GC metrics, gcdump capture, ClrMD heap diff, orchestrator state machine, local storage | ⬜ Pending |
| 4     | Cloud storage (S3/Azure), alerting (Slack/webhook)                                                                  | ⬜ Pending |
| 5     | React/Next.js dashboard                                                                                             | ⬜ Pending |

### What's Working Now

- Agent boots and attaches to the target process via the shared diagnostic socket
- Reads RSS, PSS, and VmSize from `/proc/[pid]/status` and `/proc/[pid]/smaps_rollup` every 5 seconds
- Detects the target process PID via shared process namespace
- Pings the .NET Diagnostic Port to confirm connectivity
- Sliding window keeps the last 60 minutes of readings and calculates memory growth velocity (MB/min)
- Velocity-based leak detection: logs `LeakSuspected` when managed heap growth pattern is detected
- Multi-threshold trigger: fires on hard threshold (RSS % of limit) and/or velocity threshold independently
- Logs `TriggerFired` with reason, current RSS, limit, and velocity
- Circuit breaker: 3 consecutive failures → 10-minute sleep to avoid CPU spiking
- `UnobservedTaskException` handler prevents background failures from crashing the host process
- Full Docker Compose sidecar simulation with `TestTarget` API (see below)

## Project Structure

```
src/
  MemSentinel.Contracts/        # Shared DTOs, interfaces, options — no logic
    Options/
      SentinelOptions.cs        # All configuration with ThresholdOptions nested
  MemSentinel.Core/             # Diagnostic library: ClrMD, /proc parsing, IMemoryProvider
    Analysis/
      MemoryGrowthAnalyzer.cs   # Sliding window velocity (least-squares linear regression)
      MetricsBuffer.cs          # Thread-safe circular buffer (last N minutes of readings)
      TriggerEvaluator.cs       # Pure static evaluator: hard threshold + velocity threshold
      TriggerResult.cs          # Result value: reason, RSS, limit, velocity
      TriggerThresholds.cs      # Threshold config value object
      GrowthVelocity.cs         # Computed MB/min for RSS and managed heap
      MetricSample.cs           # Single timestamped reading (RSS + heap metadata)
    Collectors/
      ProcFileParser.cs         # Span-based /proc parser (zero alloc on hot path)
      DotNetDiagnosticClient.cs # Microsoft.Diagnostics.NETCore.Client wrapper
      SystemProcessLocator.cs   # Finds target process by name across shared PID namespace
      UnixDiagnosticPortLocator.cs  # Scans /tmp for dotnet-diagnostic-*.sock
    Common/
      Result.cs                 # Result<T> pattern — no business exceptions
    Providers/
      IMemoryProvider.cs        # Core abstraction: GetRssMemoryAsync + GetHeapMetadataAsync
      LinuxMemoryProvider.cs    # Production: /proc + smaps_rollup
      MockMemoryProvider.cs     # Development: simulated growing RSS (Windows/macOS)
      RssMemoryReading.cs       # RSS/PSS/VmSize reading (readonly record struct)
      HeapMetadata.cs           # GC generation sizes (populated in Phase 3)
  MemSentinel.Agent/            # Sidecar: watchdog, Minimal API
    Worker.cs                   # BackgroundService: poll → analyze → trigger
    Logging/Log.cs              # All LoggerMessage source generators
    Infrastructure/
      CoreExtensions.cs         # IServiceCollection extension for Core registrations
  MemSentinel.TestTarget/       # Standalone .NET 10 API for local leak simulation
    LeakStore.cs                # Static buckets: managed objects, GCHandle-pinned buffers
    Program.cs                  # Minimal API: /leak/managed, /leak/unmanaged, /leak/reset
tests/
  MemSentinel.UnitTests/
    Analysis/
      MemoryGrowthAnalyzerTests.cs  # Velocity calculation, leak detection logic
      MetricsBufferTests.cs         # Circular buffer add/snapshot/capacity
    Collectors/
      ProcFileParserTests.cs        # /proc line parsing correctness
docs/
  prd.md                        # Task tracker and full roadmap
  architecture.md               # Architecture deep-dive
  current-task.md               # Active task plan with checkboxes
docker-compose.yml              # Full sidecar simulation stack
Dockerfile                      # Agent multi-stage alpine image
src/MemSentinel.TestTarget/Dockerfile  # TestTarget multi-stage alpine image
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Node.js 20+ (for the dashboard — Phase 5)
- Docker with Compose v2 (for sidecar simulation)

## Getting Started

### Local development (Windows/macOS)

```bash
# Clone and restore
git clone <repo-url>
cd MemSentinel
dotnet restore

# Build
dotnet build

# Run the agent (uses MockMemoryProvider — no Linux required)
dotnet run --project src/MemSentinel.Agent

# Run all tests
dotnet test
```

On Windows and macOS the agent automatically uses `MockMemoryProvider`, which simulates growing RSS memory without a Linux `/proc` filesystem.

### Docker Compose sidecar simulation (recommended for trigger testing)

This runs the agent as a true Linux sidecar against the `TestTarget` API — the closest local equivalent to a Kubernetes Pod with `shareProcessNamespace: true`.

```bash
# Build and start both containers
docker compose up --build

# In a second terminal: trigger managed memory growth
curl -X POST http://localhost:8080/leak/managed   # call several times
curl -X POST http://localhost:8080/leak/unmanaged

# Watch agent logs for TriggerFired
docker compose logs -f agent

# Reset all leaks
curl -X POST http://localhost:8080/leak/reset
```

The TestTarget also ships with Scalar UI at `http://localhost:8080/scalar` for browser-based endpoint testing.

Agent thresholds are set low in `docker-compose.yml` so triggers fire quickly:

| Setting                        | Value    |
| ------------------------------ | -------- |
| `ContainerMemoryLimitMb`       | 256 MB   |
| `RssLimitPercentage`           | 50%      |
| `VelocityThresholdMbPerMinute` | 1 MB/min |

### TestTarget Endpoints

| Endpoint               | Effect                                                                                                |
| ---------------------- | ----------------------------------------------------------------------------------------------------- |
| `GET /health`          | Returns 200 — used by Docker Compose health check                                                     |
| `POST /leak/managed`   | Allocates strings, byte arrays, and dictionary entries into static collections (promotes to Gen2/LOH) |
| `POST /leak/unmanaged` | Creates leaked `HttpClient` instances and `GCHandle`-pinned 256 KB buffers                            |
| `POST /leak/reset`     | Clears all static collections, releases GC handles, forces Gen2 GC                                    |
| `GET /scalar`          | Scalar interactive API docs                                                                           |

## Configuration

Settings live in `appsettings.json` and are overridable via environment variables using the `Sentinel__` prefix:

| Key                                                  | Default  | Description                                            |
| ---------------------------------------------------- | -------- | ------------------------------------------------------ |
| `Sentinel__TargetProcessName`                        | `dotnet` | Process name to locate in the shared PID namespace     |
| `Sentinel__PollingIntervalSeconds`                   | `5`      | Memory polling interval                                |
| `Sentinel__CoolingPeriodMinutes`                     | `3`      | Wait between Snapshot A and Snapshot B                 |
| `Sentinel__StorageProvider`                          | `Local`  | Storage backend: `Local`, `S3`, or `Azure`             |
| `Sentinel__MetricsWindowMinutes`                     | `60`     | Sliding window size for velocity calculation           |
| `Sentinel__Thresholds__ContainerMemoryLimitMb`       | `512`    | Container memory limit used for percentage calculation |
| `Sentinel__Thresholds__RssLimitPercentage`           | `80`     | RSS % of limit that triggers analysis                  |
| `Sentinel__Thresholds__VelocityThresholdMbPerMinute` | `5`      | Growth velocity (MB/min) that triggers analysis        |
| `Sentinel__Thresholds__Gen2GrowthLimitMb`            | `100`    | Gen 2 growth in MB (used in Phase 3)                   |

Example OpenShift override:

```yaml
env:
  - name: Sentinel__Thresholds__RssLimitPercentage
    value: "85"
  - name: Sentinel__Thresholds__VelocityThresholdMbPerMinute
    value: "3"
  - name: Sentinel__StorageProvider
    value: "S3"
```

## Architecture

MemSentinel is built around the **Environment Abstraction** pattern. All diagnostic logic depends on `IMemoryProvider` — never on `/proc` or ClrMD directly. This keeps the agent fully testable on Windows without code changes and makes provider-level unit tests trivial.

```
IMemoryProvider
  ├── LinuxMemoryProvider   (production: reads /proc, attaches via ClrMD)
  └── MockMemoryProvider    (development: returns simulated growth data)
```

Provider selection is automatic based on `OperatingSystem.IsLinux()` at startup.

The trigger pipeline is a pure data flow — no side effects until a trigger fires:

```
Worker (BackgroundService)
  └── IMemoryProvider.GetRssMemoryAsync()
  └── MetricsBuffer.AddAsync()            ← circular ring buffer
  └── MemoryGrowthAnalyzer.Calculate()    ← least-squares velocity
  └── TriggerEvaluator.Evaluate()         ← pure static, no DI
        ├── HardThreshold (RSS % of limit)
        └── VelocityThreshold (MB/min)
```

See [`docs/architecture.md`](docs/architecture.md) for a full deep-dive.

## Key Constraints

- Agent must consume < 1.5% CPU and < 100 MB RAM during idle monitoring
- ClrMD heap enumeration is single-threaded — only post-collection aggregation is parallelized

## License

MIT
