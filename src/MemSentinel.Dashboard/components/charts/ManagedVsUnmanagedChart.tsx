"use client";

import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from "recharts";
import { ChartWrapper } from "./ChartWrapper";
import type { MemorySnapshot } from "@/lib/api/types";

const COLORS = {
  gen0: "#60a5fa",
  gen1: "#34d399",
  gen2: "#f59e0b",
  loh: "#f97316",
  poh: "#a78bfa",
  native: "#6b7280",
};

interface Props {
  snapshots: MemorySnapshot[];
}

function relativeLabel(timestamp: string, nowMs: number): string {
  const diffMs = nowMs - new Date(timestamp).getTime();
  const mins = Math.round(diffMs / 60_000);
  return mins === 0 ? "now" : `-${mins}m`;
}

export function ManagedVsUnmanagedChart({ snapshots }: Props) {
  const nowMs = Date.now();
  const data = snapshots.map((s) => ({
    time: relativeLabel(s.timestamp, nowMs),
    gen0: s.heapMetadata.gen0Mb,
    gen1: s.heapMetadata.gen1Mb,
    gen2: s.heapMetadata.gen2Mb,
    loh: s.heapMetadata.lohMb,
    poh: s.heapMetadata.pohMb,
    native: s.heapMetadata.nativeMb,
  }));

  return (
    <ChartWrapper>
      <ResponsiveContainer width="100%" height={260}>
        <AreaChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#374151" />
          <XAxis
            dataKey="time"
            tick={{ fill: "#9ca3af", fontSize: 11 }}
            interval="preserveStartEnd"
          />
          <YAxis
            tick={{ fill: "#9ca3af", fontSize: 11 }}
            unit=" MB"
            width={60}
            domain={["auto", "auto"]}
          />
          <Tooltip
            contentStyle={{
              backgroundColor: "#111827",
              border: "1px solid #374151",
              borderRadius: 6,
            }}
            labelStyle={{ color: "#e5e7eb" }}
            formatter={(v) => (typeof v === "number" ? `${v.toFixed(1)} MB` : v)}
          />
          <Area type="monotone" dataKey="gen0" stackId="managed" stroke={COLORS.gen0} fill={COLORS.gen0} fillOpacity={0.6} name="Gen 0" />
          <Area type="monotone" dataKey="gen1" stackId="managed" stroke={COLORS.gen1} fill={COLORS.gen1} fillOpacity={0.6} name="Gen 1" />
          <Area type="monotone" dataKey="gen2" stackId="managed" stroke={COLORS.gen2} fill={COLORS.gen2} fillOpacity={0.6} name="Gen 2" />
          <Area type="monotone" dataKey="loh"  stackId="managed" stroke={COLORS.loh}  fill={COLORS.loh}  fillOpacity={0.6} name="LOH" />
          <Area type="monotone" dataKey="poh"  stackId="managed" stroke={COLORS.poh}  fill={COLORS.poh}  fillOpacity={0.6} name="POH" />
          <Area type="monotone" dataKey="native" stroke={COLORS.native} fill={COLORS.native} fillOpacity={0.3} name="Native" />
        </AreaChart>
      </ResponsiveContainer>
    </ChartWrapper>
  );
}
