"use client";

import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ReferenceLine,
  ResponsiveContainer,
} from "recharts";
import { ChartWrapper } from "./ChartWrapper";
import type { MemorySnapshot } from "@/lib/api/types";

interface Props {
  snapshots: MemorySnapshot[];
}

function relativeLabel(timestamp: string, nowMs: number): string {
  const diffMs = nowMs - new Date(timestamp).getTime();
  const mins = Math.round(diffMs / 60_000);
  return mins === 0 ? "now" : `-${mins}m`;
}

export function GCPauseTimeChart({ snapshots }: Props) {
  const nowMs = Date.now();
  const hasHigh = snapshots.some((s) => s.heapMetadata.gcPausePercent > 10);

  const data = snapshots.map((s) => ({
    time: relativeLabel(s.timestamp, nowMs),
    gcPause: s.heapMetadata.gcPausePercent,
  }));

  return (
    <ChartWrapper>
      <ResponsiveContainer width="100%" height={260}>
        <LineChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#374151" />
          <XAxis
            dataKey="time"
            tick={{ fill: "#9ca3af", fontSize: 11 }}
            interval="preserveStartEnd"
          />
          <YAxis
            tick={{ fill: "#9ca3af", fontSize: 11 }}
            unit="%"
            width={48}
            domain={[0, "auto"]}
          />
          <Tooltip
            contentStyle={{
              backgroundColor: "#111827",
              border: "1px solid #374151",
              borderRadius: 6,
            }}
            labelStyle={{ color: "#e5e7eb" }}
            formatter={(v) => (typeof v === "number" ? `${v.toFixed(1)}%` : v)}
          />
          <ReferenceLine
            y={10}
            stroke="#fbbf24"
            strokeDasharray="4 2"
            label={{ value: "Warning", fill: "#fbbf24", fontSize: 11 }}
          />
          <Line
            type="monotone"
            dataKey="gcPause"
            stroke={hasHigh ? "#ef4444" : "#60a5fa"}
            strokeWidth={2}
            dot={false}
            name="GC Pause %"
          />
        </LineChart>
      </ResponsiveContainer>
    </ChartWrapper>
  );
}
