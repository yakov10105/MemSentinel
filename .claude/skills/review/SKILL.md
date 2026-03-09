---
name: review
user-invocable: true
disable-model-invocation: true
description: >
  Performs a full code review of recent changes against all project rules.
  Only runs when the user types /review.
---

# /review — Full Code Review

Run the following steps in order. Do not skip steps even if earlier steps find issues.
Report ALL findings at the end in the structured format below.

## Step 1: See What Changed

```bash
git diff HEAD~1 --stat
git diff HEAD~1
```

If changes are staged but not committed:

```bash
git diff --staged --stat
git diff --staged
```

Identify which layers were touched (Core / Contracts / Agent / Tests / Dashboard)
before proceeding — this determines which checks are relevant.

## Step 2: Architecture Review

Load the `dotnet-architecture` skill and check:

**Layer & Dependency Rules**

- [ ] Are new classes in the correct layer? (Core vs Agent vs Contracts)
- [ ] Does `MemSentinel.Core` have zero new external package dependencies?
- [ ] Does `MemSentinel.Contracts` contain only DTOs, interfaces, and enums — no logic?
- [ ] Are new interfaces defined in Core/Contracts, implementations in Agent/Infrastructure?
- [ ] Is `IMemoryProvider` the only path to `/proc` or ClrMD? No direct calls in handlers or background services?

**DI & Lifetime**

- [ ] Are new services registered with appropriate lifetime (Singleton/Scoped/Transient)?
- [ ] Are DI registrations in `IServiceCollection` extension methods — not inline in `Program.cs`?
- [ ] Is storage provider selection done via the switch pattern in `StorageExtensions`?

**Handler & Feature Structure**

- [ ] Does any new handler use MediatR? (Prohibited — direct invocation only.)
- [ ] Are new features added as Vertical Slices under `Agent/Features/[FeatureName]/`?
- [ ] Are new handlers registered in `HandlerRegistry` with `FrozenDictionary`?
- [ ] Do all handlers return `Result<T>` — never throw business exceptions?

**C# 13 / .NET 10 Standards**

- [ ] Are new DI classes using primary constructors?
- [ ] Are namespaces file-scoped everywhere?
- [ ] Are new data types using `readonly record struct` where appropriate?
- [ ] Is `DateTimeOffset.UtcNow` used — never `DateTime.Now`?
- [ ] Is logging done via `LoggerMessage` source generators — no string interpolation in log calls?
- [ ] Are there any inline comments? (Prohibited — self-documenting code only.)

**BackgroundService Safety**

- [ ] Do new `BackgroundService` implementations have the circuit breaker pattern?
- [ ] Is `OperationCanceledException` caught and handled as clean shutdown (not counted as failure)?
- [ ] Is `TaskScheduler.UnobservedTaskException` handled in `Program.cs`?

## Step 3: Performance Review

Load the `dotnet-performance` skill and check:

**Memory & Buffers**

- [ ] Are any `new byte[]` allocations present in diagnostic loops? (Must use `ArrayPool<byte>`.)
- [ ] Are `ArrayPool` rentals returned in `finally` blocks?
- [ ] Are diagnostic result types `readonly record struct` — not classes?

**ClrMD Safety (highest risk — check carefully)**

- [ ] Is ClrMD heap enumeration single-threaded? (Never `Parallel.ForEachAsync` on `heap.EnumerateObjects()`.)
- [ ] Is there a `SemaphoreSlim(1,1)` guard preventing concurrent ClrMD sessions?
- [ ] Are `ClrRuntime` and `DataTarget` disposed in `finally` blocks — not just `using`?
- [ ] Is `ClrHeap` created and disposed within a single method scope — never stored as a field?
- [ ] Are type name strings interned (`string.Intern`) in heap walk loops?
- [ ] Are accumulation dictionaries pre-sized for large heap dumps?
- [ ] Is there any logging inside the heap walk loop? (Prohibited — log summary after walk only.)

**Async & Concurrency**

- [ ] Are any `lock` statements used in async code? (Must use `SemaphoreSlim`.)
- [ ] Are `.Result` or `.Wait()` calls present? (Prohibited — always `await`.)
- [ ] Are `CancellationToken`s flowing through all async calls?
- [ ] Is `ConfigureAwait(false)` used in `MemSentinel.Core` library code?
- [ ] Is `Channel<T>` used for watchdog→orchestrator handoff? (Never `ConcurrentQueue` + polling.)

**I/O & Parsing**

- [ ] Are `/proc` file reads using `Span<T>` — never `File.ReadAllText`?
- [ ] Are numeric extractions from `/proc` using `Utf8Parser.TryParse` or `long.TryParse(span)`?
- [ ] Is `System.Text.Json` used exclusively? (No Newtonsoft.)
- [ ] Are hot-path types covered by `[JsonSerializable]` source generators?
- [ ] Is LINQ absent from diagnostic hot loops (heap walk, `/proc` polling, metrics sliding window)?

## Step 4: Test Coverage Check

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

Check:

- [ ] Are new public methods in Core and Agent/Features covered by at least one test?
- [ ] Are both `Result<T>` success AND failure paths tested for every new handler?
- [ ] Are new `BackgroundService` implementations tested with `FakeMemoryProvider`?
- [ ] Is the circuit breaker state transition tested (1 failure, 2, 3 → CircuitOpen)?
- [ ] Do new tests follow `MethodName_Scenario_ExpectedBehavior` naming?
- [ ] Are tests using NSubstitute — not Moq?
- [ ] Are tests using `IAsyncLifetime` for async setup/teardown — not `IDisposable`?
- [ ] Are unit tests isolated — no real `/proc`, network, or file system access?
- [ ] Are unit tests < 100ms each?

## Step 5: Build & Test

```bash
dotnet build --no-incremental
dotnet test --no-build
```

Report exact output. If build fails, stop and report immediately — do not continue.

## Step 6: PRD & Task Sync

Check `docs/prd.md`:

- [ ] Are completed tasks marked `✅ Done`?
- [ ] Are in-progress tasks still marked `⬜ Pending` (not prematurely closed)?

Check `docs/current-task.md`:

- [ ] Are all completed steps checked off `[x]`?
- [ ] Are any steps marked complete that don't have corresponding passing tests?

## Step 7: Report

---

### /review Results

**Layers Touched**

- [ ] Core [ ] Contracts [ ] Agent [ ] Tests [ ] Dashboard

**Architecture**
| Check | Status | Detail |
|---|---|---|
| Layer placement | ✅ / ⚠️ | |
| IMemoryProvider abstraction respected | ✅ / ⚠️ | |
| No MediatR | ✅ / ⚠️ | |
| Result<T> over exceptions | ✅ / ⚠️ | |
| C# 13 standards | ✅ / ⚠️ | |
| Circuit breaker in BackgroundServices | ✅ / ⚠️ / N/A | |

**ClrMD Safety** _(skip if no ClrMD changes)_
| Check | Status | Detail |
|---|---|---|
| Single-threaded heap enumeration | ✅ / ⚠️ | |
| SemaphoreSlim session guard | ✅ / ⚠️ | |
| finally disposal of ClrRuntime/DataTarget | ✅ / ⚠️ | |
| No logging inside heap walk | ✅ / ⚠️ | |

**Performance**
| Check | Status | Detail |
|---|---|---|
| ArrayPool for buffers | ✅ / ⚠️ / N/A | |
| No lock in async code | ✅ / ⚠️ | |
| Channel<T> for async handoff | ✅ / ⚠️ / N/A | |
| Span-based /proc parsing | ✅ / ⚠️ / N/A | |
| No LINQ in hot loops | ✅ / ⚠️ / N/A | |

**Tests**
| Check | Status | Detail |
|---|---|---|
| Coverage of new public methods | ✅ / ⚠️ | |
| Both Result<T> paths tested | ✅ / ⚠️ | |
| NSubstitute (not Moq) | ✅ / ⚠️ | |
| IAsyncLifetime used | ✅ / ⚠️ | |

**Build & Tests**

```
Build:  PASS / FAIL
Tests:  X passed, Y failed, Z skipped
```

**PRD & Task Sync**

- `docs/prd.md` updated: YES / NO / N/A
- `docs/current-task.md` in sync: YES / NO / N/A

**Action Items** _(blockers first)_

1. [BLOCKER] ...
2. [WARNING] ...
3. [SUGGESTION] ...

---
