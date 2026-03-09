# MemSentinel — Claude Code Workspace

## Project Overview

MemSentinel is a cloud-native .NET 10 memory diagnostics sidecar for Kubernetes/OpenShift. It runs alongside target .NET microservices, performs automated heap diffing via ClrMD, and surfaces results through a React/Next.js dashboard. The goal: identify _why_ memory is high, not just _that_ it is.

## Tech Stack

- **Agent (Sidecar):** .NET 10, C# 13, Minimal API, BackgroundService
- **Diagnostic Library:** ClrMD, Microsoft.Diagnostics.NETCore.Client
- **Dashboard:** Next.js 15 (App Router), TypeScript, Tailwind CSS, Recharts
- **Storage:** S3-compatible, Azure Blob, Local PV (pluggable)
- **Logging:** Serilog with LoggerMessage source generators
- **Testing:** xUnit, Moq, FluentAssertions
- **Target Runtime:** Linux containers (OpenShift/Kubernetes)

## Project Structure

```
src/
  MemSentinel.Agent/          # .NET 10 sidecar — watchdog, orchestration, Minimal API
    BackgroundServices/       # MemoryWatchdog, DiagnosticOrchestrator
    Infrastructure/           # Storage adapters, notifiers
    Api/                      # Minimal API endpoints for dashboard
  MemSentinel.Core/           # Diagnostic library — ClrMD, /proc parsing
    Analysis/                 # HeapDiffEngine, RootChainAnalyzer
    Collectors/               # ProcessMetricsProvider, DotNetDiagnosticClient
  MemSentinel.Dashboard/      # Next.js 15 dashboard (separate build)
tests/
  MemSentinel.UnitTests/      # Fast, mocked — mirrors src structure
  MemSentinel.IntegrationTests/ # Real DB/process — SQLite in-memory
docs/
  prd.md                      # Source of truth for task status
  architecture.md             # Deep-dive reference (load on demand)
  onboarding.md               # Setup guide (load on demand)
  current-task.md             # Active task plan with checkboxes (overwritten each task)
```

## Essential Commands

```bash
dotnet build                              # Build entire solution
dotnet test                               # Run all tests
dotnet run --project src/MemSentinel.Agent  # Run the agent
dotnet format                             # Format code
dotnet restore                            # Restore packages
```

## Core Conventions

1. **PRD is the task tracker.** Before starting any task, read `@docs/prd.md` to understand scope. After completion, update status: `⬜ Pending` → `✅ Done`. Non-optional.
2. **No MediatR.** Use direct handler invocation. Application layer: Vertical Slice by Feature, not by technical type.
3. **Result<T> over exceptions.** Business logic never throws. All handlers return `Result<T>` defined in Core/Common.
4. **No comments in code.** Write self-documenting code. Use XML `///` only for non-obvious public APIs.
5. **Primary constructors for all DI classes.** File-scoped namespaces everywhere.
6. **`DateTimeOffset.UtcNow` always.** Never `DateTime.Now`. Never `async void`.
7. **ArrayPool<byte> for all diagnostic buffers.** Always return in `finally`. No per-operation `new byte[]`.
8. **`SemaphoreSlim` for async locking.** Never `lock` in async code.
9. **Serilog + LoggerMessage source generators.** No string-interpolated log calls.
10. **No Data Annotations on domain entities.** Use `IEntityTypeConfiguration<T>` in Infrastructure.

## Available Skills (auto-invoked by context)

Claude loads these automatically when the task context matches — do not preload all skills:

- `/dotnet-architecture` — designing new services, adding classes/interfaces, DI registration, layer decisions, project references
- `/dotnet-performance` — database queries, async/await patterns, buffer management, hot paths, ClrMD analysis loops
- `/dotnet-tests` — writing or editing tests, mocking strategy, test project structure, coverage gaps
- `/git-workflow` — committing, branching, writing PR descriptions

## Slash Commands (explicit only — you invoke these)

- `/review` — full code review of recent changes against all rules
- `/task` — structured start for a new development task with plan
- `/commit` — staged diff review and conventional commit message

## Task Workflow (ALWAYS follow this)

When I reference a task from the PRD:

1. Read `@docs/prd.md` and confirm your understanding of the task — scope, layers touched, acceptance criteria
2. Read the existing project structure to understand what already exists before proposing anything
3. Create an implementation plan in `docs/current-task.md` with:
   - `[ ]` checkboxes for each step
   - Which layer/project each step touches
   - Acceptance criteria from the PRD
4. Show me the plan and **WAIT for my approval before writing any code**
5. After approval, check off `[ ]` → `[x]` as you complete each step
6. Run `dotnet build` after each completed step — stop and report if it fails
7. When all steps are done, update `docs/prd.md` task status to `✅ Done`

**Never start coding until I explicitly say "go" or "approved".**

## Context Management

- Run `/clear` between unrelated tasks to avoid context bleed
- For architecture deep-dives: `@docs/architecture.md`
- For onboarding/setup: `@docs/onboarding.md`
- For current task scope: `@docs/prd.md`
- For active task plan and progress: `@docs/current-task.md`
