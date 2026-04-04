"use client";

import { Suspense, useEffect, useState } from "react";
import { useMemoryMetrics } from "@/lib/hooks/useMemoryMetrics";
import { ManagedVsUnmanagedChart } from "@/components/charts/ManagedVsUnmanagedChart";
import { GCPauseTimeChart } from "@/components/charts/GCPauseTimeChart";
import { MemoryStatsBar } from "@/components/overview/MemoryStatsBar";
import { ThresholdStatusBadge } from "@/components/overview/ThresholdStatusBadge";
import { useSetHeaderSlot } from "@/app/header-slot-context";
import { PageSkeleton } from "@/components/layout/PageSkeleton";

const RSS_LIMIT_MB = Number(process.env.NEXT_PUBLIC_RSS_LIMIT_MB ?? 512);

export default function HomePage() {
  const [isPaused, setIsPaused] = useState(false);
  const { data: snapshots = [] } = useMemoryMetrics(isPaused ? false : 5_000);
  const setHeaderSlot = useSetHeaderSlot();

  const latest = snapshots[snapshots.length - 1];

  useEffect(() => {
    if (!latest) return;
    setHeaderSlot(
      <ThresholdStatusBadge rssMb={latest.rssMb} limitMb={RSS_LIMIT_MB} />
    );
    return () => setHeaderSlot(null);
  }, [latest, setHeaderSlot]);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-white">Memory Overview</h1>
        <button
          onClick={() => setIsPaused((p) => !p)}
          className={`rounded-md border px-4 py-1.5 text-sm font-medium transition-colors ${
            isPaused
              ? "border-green-600 text-green-400 hover:bg-green-900/30"
              : "border-gray-700 text-gray-300 hover:bg-gray-800"
          }`}
        >
          {isPaused ? "Resume" : "Pause"}
        </button>
      </div>

      <MemoryStatsBar snapshots={snapshots} />

      <Suspense fallback={<PageSkeleton />}>
        <div className="grid grid-cols-1 gap-6 xl:grid-cols-2">
          <div className="rounded-lg border border-gray-800 bg-gray-900 p-4">
            <h2 className="mb-3 text-sm font-medium text-gray-400">
              Managed vs Unmanaged Memory
            </h2>
            <ManagedVsUnmanagedChart snapshots={snapshots} />
          </div>
          <div className="rounded-lg border border-gray-800 bg-gray-900 p-4">
            <h2 className="mb-3 text-sm font-medium text-gray-400">
              GC Pause Time
            </h2>
            <GCPauseTimeChart snapshots={snapshots} />
          </div>
        </div>
      </Suspense>
    </div>
  );
}
