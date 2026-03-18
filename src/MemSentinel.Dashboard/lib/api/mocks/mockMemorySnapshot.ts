import type { MemorySnapshot } from "@/lib/api/types";

function point(offsetSeconds: number, base: number): MemorySnapshot {
  const t = new Date(Date.now() - offsetSeconds * 1000).toISOString();
  const drift = Math.sin(offsetSeconds / 60) * 10;
  return {
    timestamp: t,
    rssMb: Math.round((base + drift + Math.random() * 4) * 10) / 10,
    heapMetadata: {
      gen0Mb: Math.round((8 + Math.random() * 4) * 10) / 10,
      gen1Mb: Math.round((12 + Math.random() * 3) * 10) / 10,
      gen2Mb: Math.round((base * 0.4 + drift * 0.3) * 10) / 10,
      lohMb: Math.round((base * 0.15 + drift * 0.1) * 10) / 10,
      pohMb: Math.round((4 + Math.random() * 1) * 10) / 10,
      nativeMb: Math.round((base * 0.25 + Math.random() * 5) * 10) / 10,
      gcPausePercent: Math.round((2 + Math.random() * 8) * 10) / 10,
    },
  };
}

export const mockMemorySnapshots: MemorySnapshot[] = Array.from(
  { length: 60 },
  (_, i) => point((59 - i) * 5, 180 + i * 0.8)
);
