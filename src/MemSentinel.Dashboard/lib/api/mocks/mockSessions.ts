import type { DiagnosticSession, TriggerReason } from "@/lib/api/types";
import { mockHeapDiffReport } from "./mockHeapDiffReport";

const reasons: TriggerReason[] = [
  "HardThreshold",
  "Velocity",
  "Manual",
  "HardThreshold",
  "Velocity",
  "HardThreshold",
  "Velocity",
  "Manual",
  "HardThreshold",
  "Velocity",
  "HardThreshold",
  "Velocity",
];

function hoursAgo(h: number): string {
  return new Date(Date.now() - h * 60 * 60 * 1000).toISOString();
}

export const mockSessions: DiagnosticSession[] = Array.from(
  { length: 12 },
  (_, i) => ({
    id: `00000000-0000-0000-0000-${String(i + 1).padStart(12, "0")}`,
    startedAt: hoursAgo((i + 1) * 3),
    completedAt: i % 5 === 4 ? null : hoursAgo((i + 1) * 3 - 1),
    triggerReason: reasons[i],
    snapshotAPath: `/data/memsentinel/session-${i + 1}/snapshot-a.gcdump`,
    snapshotBPath: `/data/memsentinel/session-${i + 1}/snapshot-b.gcdump`,
    diffReport: i % 5 === 4 ? null : mockHeapDiffReport,
  })
);
