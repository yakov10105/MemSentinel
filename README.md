# MemSentinel

A cloud-native .NET 10 memory diagnostics sidecar for Kubernetes and OpenShift. MemSentinel identifies **why** memory is high in .NET microservices — not just that it is — by performing automated heap diffing and path-to-root analysis via ClrMD.

## How It Works

MemSentinel runs as a companion container in the same Pod as your target .NET API:

1. **Monitor** — polls RSS from `/proc/[pid]/status` and GC stats via the .NET Diagnostic Port every 5 seconds
2. **Trigger** — fires when memory exceeds a threshold (e.g. 85% of container limit) or shows a sustained climb
3. **Diff** — captures Snapshot A, waits a cooling period, captures Snapshot B, then compares heap object counts and sizes with ClrMD
4. **Report** — pushes a Slack/Teams/webhook alert with the suspected leaking types and a direct link to the dashboard
5. **Visualize** — the React/Next.js dashboard shows memory trends, heap treemaps, and type-level diff tables

## Project Structure

```
src/
  MemSentinel.Contracts/    # Shared DTOs, interfaces, options — no logic
  MemSentinel.Core/         # Diagnostic library: ClrMD, /proc parsing, IMemoryProvider
  MemSentinel.Agent/        # Sidecar: watchdog, orchestrator, Minimal API
  MemSentinel.Dashboard/    # Next.js 15 dashboard (separate build)
tests/
  MemSentinel.UnitTests/    # Fast, mocked unit tests
docs/
  prd.md                    # Task tracker and roadmap
  architecture.md           # Architecture deep-dive
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Node.js 20+ (for the dashboard)
- Docker (for container builds)

## Getting Started

```bash
# Clone and restore
git clone <repo-url>
cd MemSentinel
dotnet restore

# Build
dotnet build

# Run the agent locally (uses MockMemoryProvider on Windows/macOS)
dotnet run --project src/MemSentinel.Agent

# Run all tests
dotnet test
```

On Windows and macOS, the agent automatically uses `MockMemoryProvider` which simulates growing RSS memory — no Linux `/proc` filesystem required for local development.

## Configuration

Settings are defined in `appsettings.json` and overridable via environment variables using the `Sentinel__` prefix:

| Key | Default | Description |
|---|---|---|
| `Sentinel__TargetProcessName` | `dotnet` | Name of the target process to monitor |
| `Sentinel__PollingIntervalSeconds` | `5` | How often to poll memory metrics |
| `Sentinel__RssLimitPercentage` | `80` | RSS threshold (% of container limit) to trigger analysis |
| `Sentinel__Gen2GrowthLimitMb` | `100` | Gen 2 growth in MB to trigger analysis |
| `Sentinel__CoolingPeriodMinutes` | `3` | Wait time between Snapshot A and B |
| `Sentinel__StorageProvider` | `Local` | Storage backend: `Local`, `S3`, or `Azure` |

Example OpenShift override:
```yaml
env:
  - name: Sentinel__RssLimitPercentage
    value: "85"
  - name: Sentinel__StorageProvider
    value: "S3"
```

## Architecture

MemSentinel is structured around the **Environment Abstraction** pattern. All diagnostic logic depends on `IMemoryProvider` — never on `/proc` or ClrMD directly. This makes the agent fully testable on Windows without code changes.

```
IMemoryProvider
  ├── LinuxMemoryProvider   (production: reads /proc, attaches via ClrMD)
  └── MockMemoryProvider    (development: returns simulated growth data)
```

Provider selection is automatic based on `OperatingSystem.IsLinux()` at startup.

See [`docs/architecture.md`](docs/architecture.md) for a full deep-dive.

## Roadmap

| Phase | Description | Status |
|---|---|---|
| 0 | Project foundation, abstractions, configuration, logging | 🔨 In Progress |
| 1 | Sidecar plumbing: `/proc` parser, UDS client, PID detection | ⬜ Pending |
| 2 | Watchdog: threshold triggers, sliding window metrics engine | ⬜ Pending |
| 3 | Analysis engine: ClrMD heap diff, path-to-root | ⬜ Pending |
| 4 | Storage, alerting, and dashboard | ⬜ Pending |

## License

MIT
