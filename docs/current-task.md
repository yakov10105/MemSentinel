# Task 2.3 — TestTarget API & Docker Compose Sidecar Simulation

**PRD Reference:** Phase 2, Task 2.3
**Branch:** `task/2.3-test-target` (cut from `phase/2-watchdog`)
**Layers touched:** New `src/MemSentinel.TestTarget/`, `MemSentinel.slnx`, new `docker-compose.yml`

---

## What This Task Builds

A standalone leaky .NET 10 Minimal API that serves as a realistic sidecar target,
plus the Docker Compose wiring to run the full Agent↔Target loop on any Linux Docker host.

## Steps

- [x] **Step 1 — `MemSentinel.TestTarget.csproj`**
- [x] **Step 2 — `LeakStore.cs`**
- [x] **Step 3 — `Program.cs`** (4 endpoints)
- [x] **Step 4 — `src/MemSentinel.TestTarget/Dockerfile`**
- [x] **Step 5 — Add to `MemSentinel.slnx`**
- [x] **Step 6 — `docker-compose.yml`**
- [x] **Step 7 — `dotnet build`, update PRD**
