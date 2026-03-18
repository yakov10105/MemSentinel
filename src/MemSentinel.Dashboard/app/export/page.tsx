import { Suspense } from "react";
import { PageSkeleton } from "@/components/layout/PageSkeleton";

export default function ExportPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <div className="text-gray-400">Export Center — coming in Task 4.7</div>
    </Suspense>
  );
}
