"use client";

import { LineChart, Line, ResponsiveContainer } from "recharts";
import { ChartWrapper } from "@/components/charts/ChartWrapper";
import type { MemorySnapshot } from "@/lib/api/types";

interface StatCardProps {
  label: string;
  value: string;
  sparkData: number[];
  delta: number;
  unit: string;
}

function StatCard({ label, value, sparkData, delta, unit }: StatCardProps) {
  const isPositive = delta >= 0;
  const deltaText = `${isPositive ? "+" : ""}${delta.toFixed(1)} ${unit}`;
  const deltaClass = isPositive ? "text-red-400" : "text-green-400";

  return (
    <div className="flex flex-col gap-2 rounded-lg border border-gray-800 bg-gray-900 p-4">
      <span className="text-xs font-medium text-gray-400">{label}</span>
      <div className="flex items-end justify-between gap-2">
        <span className="text-2xl font-bold text-white">{value}</span>
        <span className={`text-xs font-medium ${deltaClass}`}>{deltaText}</span>
      </div>
      <ChartWrapper>
        <ResponsiveContainer width="100%" height={32}>
          <LineChart data={sparkData.map((v) => ({ v }))}>
            <Line
              type="monotone"
              dataKey="v"
              stroke="#60a5fa"
              strokeWidth={1.5}
              dot={false}
              isAnimationActive={false}
            />
          </LineChart>
        </ResponsiveContainer>
      </ChartWrapper>
    </div>
  );
}

interface Props {
  snapshots: MemorySnapshot[];
}

export function MemoryStatsBar({ snapshots }: Props) {
  if (snapshots.length === 0) return null;

  const recent = snapshots.slice(-10);
  const latest = snapshots[snapshots.length - 1];
  const prev = snapshots.length > 1 ? snapshots[snapshots.length - 2] : latest;

  return (
    <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
      <StatCard
        label="Current RSS"
        value={`${latest.rssMb.toFixed(0)} MB`}
        sparkData={recent.map((s) => s.rssMb)}
        delta={latest.rssMb - prev.rssMb}
        unit="MB"
      />
      <StatCard
        label="Gen 2 Size"
        value={`${latest.heapMetadata.gen2Mb.toFixed(1)} MB`}
        sparkData={recent.map((s) => s.heapMetadata.gen2Mb)}
        delta={latest.heapMetadata.gen2Mb - prev.heapMetadata.gen2Mb}
        unit="MB"
      />
      <StatCard
        label="LOH Size"
        value={`${latest.heapMetadata.lohMb.toFixed(1)} MB`}
        sparkData={recent.map((s) => s.heapMetadata.lohMb)}
        delta={latest.heapMetadata.lohMb - prev.heapMetadata.lohMb}
        unit="MB"
      />
      <StatCard
        label="GC Pause"
        value={`${latest.heapMetadata.gcPausePercent.toFixed(1)}%`}
        sparkData={recent.map((s) => s.heapMetadata.gcPausePercent)}
        delta={
          latest.heapMetadata.gcPausePercent - prev.heapMetadata.gcPausePercent
        }
        unit="%"
      />
    </div>
  );
}
