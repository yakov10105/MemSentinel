"use client";

const POD_OPTIONS = ["memsentinel-demo", "api-service-7d9f", "worker-abc123"];
const NS_OPTIONS = ["default", "production", "staging"];

export function Header() {
  return (
    <header className="flex h-14 shrink-0 items-center justify-between border-b border-gray-800 bg-gray-950 px-6">
      <div id="header-slot" />

      <div className="flex items-center gap-3">
        <select className="rounded-md border border-gray-700 bg-gray-900 px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-gray-600">
          {NS_OPTIONS.map((ns) => (
            <option key={ns}>{ns}</option>
          ))}
        </select>

        <select className="rounded-md border border-gray-700 bg-gray-900 px-3 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-gray-600">
          {POD_OPTIONS.map((pod) => (
            <option key={pod}>{pod}</option>
          ))}
        </select>
      </div>
    </header>
  );
}
