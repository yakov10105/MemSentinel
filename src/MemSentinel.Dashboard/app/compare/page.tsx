import { Suspense } from "react";
import { PageSkeleton } from "@/components/layout/PageSkeleton";

export default function ComparePage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <div className="text-gray-400">
        Snapshot Comparison — coming in Task 4.5
      </div>
    </Suspense>
  );
}
