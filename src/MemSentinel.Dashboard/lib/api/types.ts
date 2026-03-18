export interface HealthStatus {
  status: string;
  version: string;
}

export interface HeapMetadataSnapshot {
  gen0Mb: number;
  gen1Mb: number;
  gen2Mb: number;
  lohMb: number;
  pohMb: number;
  nativeMb: number;
  gcPausePercent: number;
}

export interface MemorySnapshot {
  timestamp: string;
  rssMb: number;
  heapMetadata: HeapMetadataSnapshot;
}

export type TriggerReason = "HardThreshold" | "Velocity" | "Manual";

export type SessionState = "Analyzing" | "Complete" | "Failed";

export interface TypeDelta {
  typeName: string;
  countA: number;
  countB: number;
  bytesA: number;
  bytesB: number;
}

export interface HeapDiffReport {
  analyzedAt: string;
  topGrowingTypes: TypeDelta[];
  totalObjectDelta: number;
  totalBytesDelta: number;
  lohFreePercent: number | null;
}

export interface DiagnosticSession {
  id: string;
  startedAt: string;
  completedAt: string | null;
  triggerReason: TriggerReason;
  snapshotAPath: string;
  snapshotBPath: string;
  diffReport: HeapDiffReport | null;
}

export interface RetentionPath {
  typeName: string;
  path: string[];
}

export type Gen = 0 | 1 | 2 | "LOH" | "POH";

export interface AllocationEvent {
  timestamp: string;
  typeName: string;
  sizeBytes: number;
  gen: Gen;
}
