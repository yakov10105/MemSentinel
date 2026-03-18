import type { HeapDiffReport, TypeDelta } from "@/lib/api/types";

const typeNames = [
  "System.String",
  "System.Byte[]",
  "System.Collections.Generic.Dictionary`2",
  "System.Object[]",
  "MyApp.Services.RequestContext",
  "System.Threading.Tasks.Task",
  "Microsoft.AspNetCore.Http.DefaultHttpContext",
  "System.Collections.Concurrent.ConcurrentDictionary`2",
  "System.Text.StringBuilder",
  "System.Runtime.CompilerServices.AsyncTaskMethodBuilder",
  "MyApp.Models.UserSession",
  "System.Collections.Generic.List`1",
  "System.Net.Http.HttpRequestMessage",
  "System.IO.MemoryStream",
  "MyApp.Caching.CacheEntry",
  "System.Linq.Enumerable+WhereSelectListIterator`2",
  "System.Diagnostics.Activity",
  "System.Collections.Generic.HashSet`1",
  "MyApp.Data.EntityChangeTracker",
  "System.Runtime.InteropServices.GCHandle",
];

export const mockTypeDeltaList: TypeDelta[] = typeNames.map((typeName, i) => {
  const countA = 1000 + i * 200;
  const countB = countA + 500 + i * 300;
  const bytesA = countA * (64 + i * 8);
  const bytesB = countB * (64 + i * 8);
  return { typeName, countA, countB, bytesA, bytesB };
});

export const mockHeapDiffReport: HeapDiffReport = {
  analyzedAt: new Date(Date.now() - 30 * 60 * 1000).toISOString(),
  topGrowingTypes: mockTypeDeltaList,
  totalObjectDelta: mockTypeDeltaList.reduce(
    (sum, t) => sum + (t.countB - t.countA),
    0
  ),
  totalBytesDelta: mockTypeDeltaList.reduce(
    (sum, t) => sum + (t.bytesB - t.bytesA),
    0
  ),
  lohFreePercent: 34.7,
};
