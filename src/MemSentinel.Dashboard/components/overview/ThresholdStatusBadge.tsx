"use client";

interface Props {
  rssMb: number;
  limitMb: number;
}

export function ThresholdStatusBadge({ rssMb, limitMb }: Props) {
  const pct = (rssMb / limitMb) * 100;

  let label: string;
  let cls: string;

  if (pct > 85) {
    label = "Critical";
    cls = "bg-red-500/20 text-red-400 border-red-500/40";
  } else if (pct > 70) {
    label = "Warning";
    cls = "bg-amber-500/20 text-amber-400 border-amber-500/40";
  } else {
    label = "Healthy";
    cls = "bg-green-500/20 text-green-400 border-green-500/40";
  }

  return (
    <span
      className={`rounded-full border px-2.5 py-0.5 text-xs font-medium ${cls}`}
    >
      {label} · {rssMb.toFixed(0)} MB
    </span>
  );
}
