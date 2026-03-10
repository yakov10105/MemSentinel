"use client";

import { useHealth } from "@/lib/hooks/useHealth";

export default function HomePage() {
  const { data, isLoading, isError } = useHealth();

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-6 bg-gray-950 text-white">
      <h1 className="text-3xl font-bold tracking-tight">MemSentinel</h1>

      <div className="flex items-center gap-3 rounded-lg border border-gray-800 bg-gray-900 px-6 py-4">
        {isLoading && (
          <>
            <span className="h-3 w-3 animate-pulse rounded-full bg-yellow-400" />
            <span className="text-sm text-gray-400">Connecting to agent…</span>
          </>
        )}

        {isError && (
          <>
            <span className="h-3 w-3 rounded-full bg-red-500" />
            <span className="text-sm text-red-400">Agent unreachable</span>
          </>
        )}

        {data && (
          <>
            <span className="h-3 w-3 rounded-full bg-green-500" />
            <span className="text-sm text-green-400">
              Agent connected — v{data.version}
            </span>
          </>
        )}
      </div>
    </main>
  );
}
