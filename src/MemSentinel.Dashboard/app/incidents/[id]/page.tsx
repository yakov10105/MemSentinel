import { Suspense } from "react";
import { PageSkeleton } from "@/components/layout/PageSkeleton";

interface Props {
  params: Promise<{ id: string }>;
}

export default async function SessionDetailPage({ params }: Props) {
  const { id } = await params;
  return (
    <Suspense fallback={<PageSkeleton />}>
      <div className="text-gray-400">
        Session Detail ({id}) — coming in Task 4.4
      </div>
    </Suspense>
  );
}
