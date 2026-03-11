# Task 1.4 — Linux /proc Parser (Unmanaged Memory)

**PRD Reference:** Phase 1, Task 1.4
**Branch:** `task/1.4-proc-parser` (cut from `phase/1-plumbing`)
**Layers touched:** `MemSentinel.Core` (extract parser, add InternalsVisibleTo), `MemSentinel.UnitTests` (new tests), `MemSentinel.Agent` (new endpoint)

---

## What Already Exists (Phase 0)
`LinuxMemoryProvider` already reads:
- `/proc/[pid]/status` → VmRSS, VmSize
- `/proc/[pid]/smaps_rollup` → Pss
- Algorithm: `ArrayPool<byte>` + `Utf8Parser` + `ReadOnlySpan<byte>` line scanning

**Gap:** parser logic is `private static` — untestable without real `/proc` files. No tests exist. No endpoint to confirm readings in-cluster.

---

## Acceptance Criteria (PRD Phase 1 DoD)
- [ ] Sidecar can read the API's RSS memory from the Linux kernel
- [ ] `GET /metrics` returns `{ rssBytes, pssBytes, vmSizeBytes, capturedAt }` with HTTP 200
- [ ] Unit tests cover the parser with synthetic `/proc` content
- [ ] `dotnet build` — 0 warnings, 0 errors
- [ ] `dotnet test` — all passing

---

## Steps

- [x] **Step 1 — Extract `ProcFileParser` (Core/Collectors)**
  - Create `src/MemSentinel.Core/Collectors/ProcFileParser.cs`
  - `internal static class ProcFileParser`
  - Move `ParseField(ReadOnlySpan<byte>, ReadOnlySpan<byte>)` and `SkipWhitespace(ReadOnlySpan<byte>)` from `LinuxMemoryProvider`

- [x] **Step 2 — Update `LinuxMemoryProvider` (Core/Providers)**
  - Remove the two private static methods
  - Call `ProcFileParser.ParseField(...)` instead
  - No behaviour change — pure refactor

- [x] **Step 3 — Add `InternalsVisibleTo` to `MemSentinel.Core.csproj`**
  - Allows test project to access `internal ProcFileParser`

- [x] **Step 4 — Write unit tests (UnitTests/Collectors/ProcFileParserTests.cs)**
  - Parses VmRSS from realistic `/proc/[pid]/status` bytes
  - Parses Pss from realistic `/proc/[pid]/smaps_rollup` bytes
  - Returns 0 when field not found
  - Returns 0 when line is malformed (no numeric value)
  - Handles large values (multi-GB — fits in `long`)
  - Handles field at end of buffer (no trailing newline)

- [x] **Step 5 — Add `GET /metrics` endpoint (Agent/Program.cs)**
  - Calls `IMemoryProvider.GetRssMemoryAsync()`
  - Returns `200 { rssBytes, pssBytes, vmSizeBytes, capturedAt }`

- [x] **Step 6 — Build, test, and update PRD**
  - `dotnet build` → 0 warnings, 0 errors ✅
  - `dotnet test` → 6/6 passed ✅
  - Update `docs/prd.md` Task 1.4 → ✅ Done

---

## Files to Create / Modify

| File | Action |
|---|---|
| `src/MemSentinel.Core/Collectors/ProcFileParser.cs` | Create |
| `src/MemSentinel.Core/Providers/LinuxMemoryProvider.cs` | Modify — use ProcFileParser |
| `src/MemSentinel.Core/MemSentinel.Core.csproj` | Modify — InternalsVisibleTo |
| `tests/MemSentinel.UnitTests/Collectors/ProcFileParserTests.cs` | Create |
| `src/MemSentinel.Agent/Program.cs` | Modify — add /metrics endpoint |
| `docs/prd.md` | Modify — Task 1.4 ✅ Done |
