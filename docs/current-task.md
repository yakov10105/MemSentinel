# Current Task: 0.2 — Abstraction Layer for Testability

**PRD Reference:** Phase 0, Task 0.2
**Goal:** Define `IMemoryProvider`, implement `LinuxMemoryProvider` (reads `/proc`) and `MockMemoryProvider` (fake data), wire DI in `Program.cs` to auto-select based on OS platform.
**Layer(s) touched:** Core (interface + implementations), Agent (DI registration, Worker update)

---

## Files to Create / Modify

| File | Action | Layer |
|---|---|---|
| `Core/Common/Result.cs` | Create — `Result<T>` + `Error` | Core |
| `Core/Providers/RssMemoryReading.cs` | Create — value type | Core |
| `Core/Providers/HeapMetadata.cs` | Create — value type | Core |
| `Core/Providers/IMemoryProvider.cs` | Create — interface | Core |
| `Core/Providers/LinuxMemoryProvider.cs` | Create — `/proc` reader | Core |
| `Core/Providers/MockMemoryProvider.cs` | Create — fake data | Core |
| `Agent/Program.cs` | Update — DI swap on OS | Agent |
| `Agent/Worker.cs` | Update — inject provider, fix UtcNow, LoggerMessage | Agent |

---

## Steps

- [x] **Step 1 — `Core/Common/Result.cs`**
  - `Result<T>` readonly record struct with `IsSuccess`, `Value?`, `Error?`
  - `Error` readonly record struct with `Code`, `Message`
  - Static factory: `Result<T>.Success(value)` and `Result<T>.Failure(error)`

- [x] **Step 2 — `Core/Providers/RssMemoryReading.cs`**
  - Record struct: `RssBytes`, `PssBytes`, `VmSizeBytes`, `CapturedAt` (DateTimeOffset)

- [x] **Step 3 — `Core/Providers/HeapMetadata.cs`**
  - Record struct: `Gen0Bytes`, `Gen1Bytes`, `Gen2Bytes`, `LohBytes`, `PohBytes`, `CapturedAt`

- [x] **Step 4 — `Core/Providers/IMemoryProvider.cs`**
  - `bool IsAvailable { get; }`
  - `ValueTask<RssMemoryReading> GetRssMemoryAsync(CancellationToken ct)`
  - `ValueTask<HeapMetadata> GetHeapMetadataAsync(CancellationToken ct)`

- [x] **Step 5 — `Core/Providers/LinuxMemoryProvider.cs`**
  - Constructor takes `int pid`
  - `IsAvailable`: returns `true` only on Linux (`OperatingSystem.IsLinux()`)
  - `GetRssMemoryAsync`: parses `/proc/[pid]/status` for `VmRSS`, `VmSize`; parses `/proc/[pid]/smaps_rollup` for PSS
  - `GetHeapMetadataAsync`: returns empty `HeapMetadata` (ClrMD integration is Phase 1+)

- [x] **Step 6 — `Core/Providers/MockMemoryProvider.cs`**
  - `IsAvailable`: always `true`
  - Returns deterministic fake readings (fixed values + small simulated growth each call)
  - Constructor takes optional `MockMemoryOptions` for configurable baseline values

- [x] **Step 7 — `Agent/Program.cs`**
  - Detect target PID: find process by name from config (`TargetProcessName`, default `"dotnet"`) — on Windows, fall back to current process PID for Mock mode
  - Register `IMemoryProvider`: Linux → `LinuxMemoryProvider`, else → `MockMemoryProvider`

- [x] **Step 8 — `Agent/Worker.cs`**
  - Inject `IMemoryProvider`
  - Call `GetRssMemoryAsync` each iteration, log the result
  - Fix `DateTimeOffset.Now` → `DateTimeOffset.UtcNow`
  - Replace `logger.LogInformation(...)` with a `Log.cs` partial class using `[LoggerMessage]`

- [x] **Step 9 — `dotnet build`**
  - Must succeed with 0 warnings, 0 errors

---

## Acceptance Criteria (DoD from PRD)

- `IMemoryProvider` is defined in `Core/Providers/` — no reference to `Agent`
- `LinuxMemoryProvider` and `MockMemoryProvider` both implement it
- `Program.cs` selects the provider based on `OperatingSystem.IsLinux()`
- `dotnet run` on Windows starts the Agent using `MockMemoryProvider` without crashing
- `dotnet build` — 0 warnings, 0 errors
