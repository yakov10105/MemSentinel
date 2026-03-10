# Current Task: 0.6 — Next.js + TypeScript Scaffolding

**PRD Reference:** Phase 0, Task 0.6
**Goal:** Scaffold the dashboard, define TS interfaces mirroring C# Contracts, set up React Query, and build a Health page that fetches from the Agent.
**Layer(s) touched:** Agent (add HTTP server), Dashboard (new Next.js project)

---

## Files Created / Modified

| File | Action |
|---|---|
| `Agent/MemSentinel.Agent.csproj` | SDK → `Microsoft.NET.Sdk.Web`; removed `Microsoft.Extensions.Hosting` (included by Web SDK) |
| `Agent/Program.cs` | Migrated to `WebApplication.CreateBuilder`; added `GET /health` endpoint |
| `Agent/appsettings.json` | Added `"Urls": "http://+:5000"` |
| `Dashboard/` | Scaffolded via `create-next-app@latest` (Next.js 16, TypeScript, Tailwind, App Router) |
| `Dashboard/lib/types/contracts.ts` | `HealthStatus`, `RssMemoryReading`, `HeapMetadata`, `MemorySnapshot`, `LeakReport`, `LeakingType` |
| `Dashboard/lib/api/agent.ts` | `agentFetch<T>` + `fetchHealth()` using `NEXT_PUBLIC_AGENT_URL` |
| `Dashboard/lib/hooks/useHealth.ts` | `useHealth()` — `useQuery` wrapping `fetchHealth`, 10s refetch |
| `Dashboard/app/providers.tsx` | `QueryClientProvider` client component wrapper |
| `Dashboard/app/layout.tsx` | Updated metadata; wrapped children in `<Providers>` |
| `Dashboard/app/page.tsx` | Health status page with green/yellow/red indicator |
| `Dashboard/.env.local` | `NEXT_PUBLIC_AGENT_URL=http://localhost:5000` |

---

## Steps

- [x] **Step 1 — Agent: HTTP server**
  - SDK: `Microsoft.NET.Sdk.Web`
  - `WebApplication.CreateBuilder` + `app.MapGet("/health", ...)`

- [x] **Step 2 — `dotnet build`** — 0 warnings, 0 errors ✅

- [x] **Step 3 — Scaffold Next.js**
  - Next.js 16.1.6, TypeScript, Tailwind, App Router
  - `@tanstack/react-query` installed

- [x] **Step 4 — TypeScript contracts** (`lib/types/contracts.ts`)

- [x] **Step 5 — API client + hook**

- [x] **Step 6 — Health page** (`app/page.tsx`)

- [x] **Step 7 — `npm run build`** — 0 type errors ✅

---

## Notes

- npm naming restrictions require lowercase; scaffold directory was renamed from `memsentinel-dashboard` → `MemSentinel.Dashboard` post-creation
- Health page is a `"use client"` component; React Query hydrates client-side after static prerender shell

## Acceptance Criteria (DoD from PRD)

- `GET /health` returns `{ "status": "ok", "version": "1.0.0" }` ✅
- TypeScript interfaces structurally match C# DTOs ✅
- `dotnet build` — 0 warnings, 0 errors ✅
- `npm run build` — 0 type errors ✅
