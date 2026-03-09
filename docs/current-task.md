# Current Task: 0.3 — Centralized Configuration System

**PRD Reference:** Phase 0, Task 0.3
**Goal:** Build `SentinelOptions` in `Contracts`, bind it to `appsettings.json` + environment variables, and thread it through `Program.cs` and `Worker.cs` so all settings flow from one place.
**Layer(s) touched:** Contracts (new options class), Agent (registration, appsettings, Worker)

---

## Files to Create / Modify

| File | Action | Layer |
|---|---|---|
| `Contracts/Options/SentinelOptions.cs` | Create — options root + nested ThresholdOptions | Contracts |
| `Agent/appsettings.json` | Update — add full `Sentinel` config block with defaults | Agent |
| `Agent/Program.cs` | Update — `Configure<SentinelOptions>`, use options for provider selection | Agent |
| `Agent/Worker.cs` | Update — inject `IOptionsMonitor<SentinelOptions>`, drive polling interval | Agent |

---

## Steps

- [x] **Step 1 — `Contracts/Options/SentinelOptions.cs`**
  - Create `Contracts/Options/` directory
  - `ThresholdOptions` — `RssLimitPercentage` (default 80.0), `Gen2GrowthLimitMb` (default 100.0)
  - `SentinelOptions` — the root bound to `"Sentinel"` config section:
    - `TargetProcessName` (default `"dotnet"`)
    - `PollingIntervalSeconds` (default `5`)
    - `CoolingPeriodMinutes` (default `3`)
    - `StorageProvider` (default `"Local"`)
    - `Thresholds` — nested `ThresholdOptions`
  - All properties `init`-only; no Data Annotations (use `IValidateOptions<T>` later)

- [x] **Step 2 — `Agent/appsettings.json`**
  - Add a `Sentinel` block with all defaults matching the class defaults:
    ```json
    "Sentinel": {
      "TargetProcessName": "dotnet",
      "PollingIntervalSeconds": 5,
      "CoolingPeriodMinutes": 3,
      "StorageProvider": "Local",
      "Thresholds": {
        "RssLimitPercentage": 80.0,
        "Gen2GrowthLimitMb": 100.0
      }
    }
    ```

- [x] **Step 3 — `Agent/Program.cs`**
  - Register: `builder.Services.Configure<SentinelOptions>(builder.Configuration.GetSection("Sentinel"))`
  - Replace the raw `builder.Configuration["Sentinel:TargetProcessName"]` string lookup with `IOptions<SentinelOptions>` resolved from the service provider

- [x] **Step 4 — `Agent/Worker.cs`**
  - Replace the hardcoded `TimeSpan.FromSeconds(5)` delay with `IOptionsMonitor<SentinelOptions>` so changes survive without a restart (hot-reload friendly)
  - Add a `Log.PollingIntervalUsed` log message at startup showing the active interval

- [x] **Step 5 — `dotnet build`**
  - 0 warnings, 0 errors

---

## Acceptance Criteria (DoD from PRD)

- `SentinelOptions` lives in `Contracts` — no logic, no Data Annotations
- `appsettings.json` contains the full `Sentinel` block
- Setting `Sentinel__PollingIntervalSeconds=2` as an environment variable overrides the json value
- `dotnet build` — 0 warnings, 0 errors
