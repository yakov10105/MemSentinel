"use client";

import { useQuery } from "@tanstack/react-query";
import { fetchHealth } from "@/lib/api/agent";
import type { HealthStatus } from "@/lib/types/contracts";

export function useHealth() {
  return useQuery<HealthStatus, Error>({
    queryKey: ["health"],
    queryFn: fetchHealth,
    refetchInterval: 10_000,
    retry: 2,
  });
}
