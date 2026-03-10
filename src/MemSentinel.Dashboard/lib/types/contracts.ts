export interface HealthStatus {
  status: string;
  version: string;
}

export interface RssMemoryReading {
  rssBytes: number;
  pssBytes: number;
  vmSizeBytes: number;
  capturedAt: string;
}

export interface HeapMetadata {
  gen0Bytes: number;
  gen1Bytes: number;
  gen2Bytes: number;
  lohBytes: number;
  pohBytes: number;
  capturedAt: string;
}

export interface MemorySnapshot {
  id: string;
  podName: string;
  namespace: string;
  capturedAt: string;
  rss: RssMemoryReading;
  heap: HeapMetadata;
}

export interface LeakReport {
  id: string;
  snapshotAId: string;
  snapshotBId: string;
  capturedAt: string;
  topLeakingTypes: LeakingType[];
}

export interface LeakingType {
  typeName: string;
  countDelta: number;
  sizeDeltaBytes: number;
  growthPercent: number;
  retentionPath: string;
}
