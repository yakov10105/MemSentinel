"use client";

import dynamic from "next/dynamic";
import type { ComponentType, ReactNode } from "react";

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

const ClientOnly = dynamic(
  () => Promise.resolve(({ children }: Props) => <>{children}</>),
  {
    ssr: false,
    loading: () => (
      <div className="flex h-full w-full animate-pulse items-center justify-center rounded-md bg-gray-800" />
    ),
  }
) as ComponentType<Props>;

export function ChartWrapper({ children, fallback }: Props) {
  return <ClientOnly fallback={fallback}>{children}</ClientOnly>;
}
