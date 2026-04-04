# Task 4.2 — Real-Time Memory Overview Page

**Branch:** `task/4.2-memory-overview-page`  
**Phase branch:** `phase/4-dashboard`

## Scope

Build the live monitoring view on `/`: stacked area chart (managed vs. unmanaged), GC pause time chart, stat cards with sparklines, threshold badge in the header, and pause/resume control.

## Layers Touched

- `src/MemSentinel.Dashboard/lib/hooks/` — new `useMemoryMetrics` hook
- `src/MemSentinel.Dashboard/components/charts/` — two new Recharts wrappers
- `src/MemSentinel.Dashboard/components/overview/` — stat bar + threshold badge
- `src/MemSentinel.Dashboard/components/layout/Header.tsx` — context slot wiring
- `src/MemSentinel.Dashboard/app/` — new `HeaderSlotContext`, updated `layout.tsx` + `page.tsx`

## Pre-conditions (already done)

- `MemorySnapshot` interface exists in `lib/api/types.ts` ✅
- `mockMemorySnapshots` (60 points × 5 s) exists in `lib/api/mocks/mockMemorySnapshot.ts` ✅
- `useMockFlag()` exists ✅
- `ChartWrapper` (SSR-safe `dynamic`) exists ✅

## Implementation Steps

- [x] 1. **`useMemoryMetrics` hook** — `lib/hooks/useMemoryMetrics.ts`  
  React Query with `refetchInterval: intervalMs`. Mock mode: maintain a `useRef` cursor that advances one index per refetch over `mockMemorySnapshots`, returning a sliding 60-point window. Real mode: `GET /metrics/live` via `apiClient`.

- [x] 2. **`HeaderSlotContext`** — `app/header-slot-context.tsx`  
  React context (`createContext<ReactNode>`) + `HeaderSlotProvider` wrapper and `useHeaderSlot` / `useSetHeaderSlot` hooks. Needed so the Overview page can inject `<ThresholdStatusBadge />` into the shared header without prop-drilling through `layout.tsx`.

- [x] 3. **Wire `HeaderSlotProvider` into layout** — `app/layout.tsx` + `components/layout/Header.tsx`  
  Wrap `<Providers>` children with `HeaderSlotProvider`; replace `<div id="header-slot" />` in `Header.tsx` with `useHeaderSlot()` rendering.

- [x] 4. **`<ThresholdStatusBadge />`** — `components/overview/ThresholdStatusBadge.tsx`  
  Pill reading `rssMb` vs. `NEXT_PUBLIC_RSS_LIMIT_MB` (default 512). Renders `Healthy` (green, ≤70%), `Warning` (amber, >70%), `Critical` (red, >85%). Pure presentational — receives `rssMb` and `limitMb` as props.

- [x] 5. **`<ManagedVsUnmanagedChart />`** — `components/charts/ManagedVsUnmanagedChart.tsx`  
  Recharts `AreaChart` inside `ChartWrapper`. Stacked `Area` series: Gen0 (`--color-gen0`), Gen1, Gen2, LOH, POH, then a non-stacked `Area` for `nativeMb` (muted `--color-native`). X-axis: relative time labels (`-5m` … `now`). Y-axis: MB, `domain: ['auto', 'auto']`.

- [x] 6. **`<GCPauseTimeChart />`** — `components/charts/GCPauseTimeChart.tsx`  
  Recharts `LineChart` inside `ChartWrapper` for `gcPausePercent`. `ReferenceLine y={10}` with amber label "Warning". Line stroke switches to `--color-critical` (`#ef4444`) if any point in the window exceeds 10%.

- [x] 7. **`<MemoryStatsBar />`** — `components/overview/MemoryStatsBar.tsx`  
  Four stat cards (Current RSS, Gen2 Size, LOH Size, GC Pause %). Each card: current value, mini sparkline of last 10 points (Recharts `LineChart` tiny, no axes), coloured delta badge comparing latest vs. previous point (`+N MB` green / `-N MB` red).

- [x] 8. **Assemble `/` page** — `app/page.tsx`  
  - `useMemoryMetrics(5_000)` drives all components  
  - `<MemoryStatsBar />` full-width at top  
  - Two-column grid: `<ManagedVsUnmanagedChart />` | `<GCPauseTimeChart />`  
  - Pause / Resume toggle: `isPaused` state; when paused pass `refetchInterval: false`, freeze on last data  
  - `useSetHeaderSlot` to inject `<ThresholdStatusBadge />` with current RSS  
  - Wrap chart grid in `<Suspense fallback={<PageSkeleton />}>`

- [x] 9. **Build verification** — `npm run build` must pass with 0 TypeScript errors

## Acceptance Criteria (DoD)

- Overview page auto-refreshes at 5 s in `NEXT_PUBLIC_USE_MOCKS=true` mode
- Pausing and resuming works without remounting charts
- `<ManagedVsUnmanagedChart />` renders all five managed series stacked correctly
- `<ThresholdStatusBadge />` appears in the top header bar
- `npm run build` passes with 0 TypeScript errors
