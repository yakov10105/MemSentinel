import type { HealthStatus } from "@/lib/types/contracts";

const AGENT_URL = process.env.NEXT_PUBLIC_AGENT_URL ?? "http://localhost:5000";

async function agentFetch<T>(path: string): Promise<T> {
  const res = await fetch(`${AGENT_URL}${path}`);
  if (!res.ok) {
    throw new Error(`Agent request failed: ${res.status} ${res.statusText}`);
  }
  return res.json() as Promise<T>;
}

export function fetchHealth(): Promise<HealthStatus> {
  return agentFetch<HealthStatus>("/health");
}
