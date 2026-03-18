import type { AllocationEvent, Gen } from "@/lib/api/types";

const TYPE_NAMES = [
  "System.String",
  "System.Byte[]",
  "System.Object[]",
  "MyApp.Services.RequestContext",
  "System.Threading.Tasks.Task",
  "System.Collections.Generic.List`1",
  "System.IO.MemoryStream",
  "MyApp.Models.UserSession",
];

const GENS: Gen[] = [0, 0, 0, 1, 1, 2, "LOH", "POH"];

export function mockProfilerStream(
  onEvent: (event: AllocationEvent) => void
): () => void {
  let index = 0;
  const id = setInterval(() => {
    const typeName = TYPE_NAMES[index % TYPE_NAMES.length];
    const gen = GENS[index % GENS.length];
    const sizeBytes = Math.floor(64 + Math.random() * 4096);
    onEvent({
      timestamp: new Date().toISOString(),
      typeName,
      sizeBytes,
      gen,
    });
    index++;
  }, 500);

  return () => clearInterval(id);
}
