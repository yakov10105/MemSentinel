import { Suspense } from "react";
import { PageSkeleton } from "@/components/layout/PageSkeleton";

export default function ProfilerPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <div className="text-gray-400">Live Profiler — coming in Task 4.6</div>
    </Suspense>
  );
}
