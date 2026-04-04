"use client";

import { useQuery } from "@tanstack/react-query";
import { useMockFlag } from "@/lib/useMockFlag";
import { mockMemorySnapshots } from "@/lib/api/mocks/mockMemorySnapshot";
import { apiClient } from "@/lib/api/client";
import type { MemorySnapshot } from "@/lib/api/types";

let mockCursor = 0;

export function useMemoryMetrics(intervalMs: number | false) {
  const isMock = useMockFlag();

  return useQuery<MemorySnapshot[]>({
    queryKey: ["memory-metrics"],
    queryFn: async () => {
      if (isMock) {
        mockCursor = (mockCursor + 1) % mockMemorySnapshots.length;
        const end = mockCursor + 1;
        const start = Math.max(0, end - 60);
        return mockMemorySnapshots.slice(start, end);
      }
      const { data } = await apiClient.get<MemorySnapshot[]>("/metrics/live");
      return data;
    },
    refetchInterval: intervalMs,
  });
}
