export function PageSkeleton() {
  return (
    <div className="flex flex-col gap-4 animate-pulse">
      <div className="h-8 w-48 rounded-md bg-gray-800" />
      <div className="h-48 rounded-md bg-gray-800" />
      <div className="h-48 rounded-md bg-gray-800" />
    </div>
  );
}
