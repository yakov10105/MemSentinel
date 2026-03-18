import { Suspense } from "react";
import { PageSkeleton } from "@/components/layout/PageSkeleton";

export default function IncidentsPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <div className="text-gray-400">Incident Browser — coming in Task 4.3</div>
    </Suspense>
  );
}
