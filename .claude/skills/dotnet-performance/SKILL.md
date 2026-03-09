---
name: dotnet-performance
description: >
  .NET performance patterns and rules for MemSentinel.
  Auto-invoke when: writing diagnostic collection loops, processing heap dumps,
  implementing caching, working with async/await, handling large object graphs,
  parsing /proc files, implementing the metrics sliding window, or when the
  words "performance", "allocation", "buffer", or "optimization" appear.
  Do NOT load for simple config changes or test files.
---

# MemSentinel Performance Rules

## Memory Management — Buffer Handling

`ArrayPool<byte>` is MANDATORY for all diagnostic I/O buffers. Never `new byte[]` in a loop:

```csharp
var buffer = ArrayPool<byte>.Shared.Rent(4096);
try
{
    var bytesRead = await stream.ReadAsync(buffer.AsMemory(), ct);
    ProcessDiagnosticData(buffer.AsSpan(0, bytesRead));
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
}
```

- **`Span<T>` / `ReadOnlySpan<T>`:** All synchronous data processing — `/proc` parsing, byte slicing, string scanning.
- **`Memory<T>`:** Passing buffers to async methods (`ReadAsync`, `WriteAsync`).
- **`stackalloc`:** Fixed-size buffers under 1KB known at compile time only.
- **`ObjectPool<T>`:** For frequently allocated analysis result objects if heap profiling confirms pressure.

## /proc File Parsing (ProcessMetricsProvider)

`/proc/[pid]/status` and `/proc/[pid]/smaps_rollup` are polled every 5 seconds.
Treat this as a hot path:

```csharp
// GOOD — Span-based parsing, no string allocations
private static long ParseVmRss(ReadOnlySpan<byte> fileContent)
{
    var vmRssLine = fileContent.FindLine("VmRSS:"u8);
    Utf8Parser.TryParse(vmRssLine.TrimStart(), out long kbValue, out _);
    return kbValue * 1024;
}

// BAD
var lines = File.ReadAllText($"/proc/{pid}/status").Split('\n');
var rss = long.Parse(lines.First(l => l.StartsWith("VmRSS:")).Split(':')[1].Trim().Split(' ')[0]);
```

Rules:

- Use `RandomAccess.ReadAsync` or `FileStream` with `FileOptions.SequentialScan` for `/proc` reads.
- Never `File.ReadAllText` — allocates a full string for a file you'll scan with Span anyway.
- Cache the target PID at startup in `ProcessMetricsProvider`. Never re-scan `/proc` on every poll.
- Extract from `/proc/[pid]/smaps_rollup` for PSS (Proportional Set Size) — more accurate than RSS for shared memory.
- Use `long.TryParse(span)` and `Utf8Parser.TryParse` — never `int.Parse(string)` after splitting.
- No `string.Split` in `/proc` parsers — use `MemoryExtensions.IndexOf` and span slicing.

## Sliding Window Metrics Buffer (MetricsEngine)

The in-memory time-series buffer (last 60 minutes of readings at 5s intervals = ~720 entries)
must be allocation-efficient. Use a fixed-size circular buffer, not `List<T>`:

```csharp
// Core/Analysis/CircularMetricsBuffer.cs
public sealed class CircularMetricsBuffer(int capacity)
{
    private readonly RssMemoryReading[] _buffer = new RssMemoryReading[capacity];
    private int _head;
    private int _count;
    private readonly Lock _sync = new();

    public void Add(RssMemoryReading reading)
    {
        lock (_sync)
        {
            _buffer[_head] = reading;
            _head = (_head + 1) % capacity;
            if (_count < capacity) _count++;
        }
    }

    public ReadOnlySpan<RssMemoryReading> GetSnapshot() { ... }
}
```

- `RssMemoryReading` must be a `readonly record struct` — never a class.
- Pre-size to `(60 * 60) / pollingIntervalSeconds` at startup from `SentinelOptions`.
- Growth velocity calculation iterates the span once — no LINQ.

## Producer/Consumer: MemoryWatchdog → DiagnosticOrchestrator

Use `System.Threading.Channels` for handoff between the watchdog and orchestrator.
Never `ConcurrentQueue` + polling — that burns CPU. Never shared state:

```csharp
// Agent/Program.cs — register as singleton
var channel = Channel.CreateBounded<DiagnosticTrigger>(new BoundedChannelOptions(4)
{
    FullMode = BoundedChannelFullMode.DropOldest,
    SingleReader = true,
    SingleWriter = true
});
services.AddSingleton(channel.Writer);
services.AddSingleton(channel.Reader);

// MemoryWatchdog — writes when threshold exceeded
await _triggerWriter.TryWriteAsync(new DiagnosticTrigger(reason, timestamp), ct);

// DiagnosticOrchestrator — reads and processes
await foreach (var trigger in _triggerReader.ReadAllAsync(stoppingToken))
{
    await RunDiagnosticCycleAsync(trigger, stoppingToken);
}
```

`BoundedChannelOptions(4)` with `DropOldest` prevents unbounded queuing during a leak storm.

## ClrMD Analysis Loops

**ClrMD heap enumeration is NOT thread-safe.** Never parallelize the heap walk itself.
Parallelize only post-collection aggregation on a copied data structure:

```csharp
// WRONG — heap enumeration is single-threaded only
await Parallel.ForEachAsync(heap.EnumerateObjects(), options, ProcessObjectAsync);

// CORRECT — enumerate first, then parallel aggregate
var objects = new List<ClrObject>(capacity: 100_000);
foreach (var obj in heap.EnumerateObjects()) // single-threaded
{
    if (obj.Size > MinSizeThreshold)
        objects.Add(obj);
}

// Now safe to parallelize on the captured List
var grouped = objects
    .GroupBy(o => o.Type?.Name ?? "Unknown")
    .ToDictionary(g => g.Key, g => g.Sum(o => (long)o.Size));
```

Additional ClrMD performance rules:

- **Pre-size accumulation dictionaries** — heap dumps contain millions of objects:
  `new Dictionary<string, long>(capacity: 10_000)`
- **Intern type name strings** — ClrMD returns new string instances per object:
  `string.Intern(obj.Type?.Name ?? "Unknown")`
- **Early size filtering** before grouping — skip objects below `MinSizeThreshold` (default: 85 bytes, LOH boundary).
- **`[MethodImpl(MethodImplOptions.AggressiveInlining)]`** on the size filter predicate.
- Always detach `ClrRuntime` and dispose `DataTarget` in `finally` — see architecture skill for full pattern.

## Async / Task Performance

- **`ValueTask<Result<T>>`** for handlers that frequently resolve without I/O (threshold not exceeded → immediate return).
- **`ConfigureAwait(false)`** in `MemSentinel.Core` (library code). Not required in Agent.
- **`CancellationToken`** flows through every async call. Link to `ApplicationStopping`.
- **Never `.Result` or `.Wait()`** on Tasks.
- **`Task.Run`** only for CPU-bound ClrMD analysis blocks. Never for I/O.
- **Never `async void`.**

## Concurrency Primitives

- **`SemaphoreSlim(1,1)`** for ClrMD session guard — one active analysis at a time:

```csharp
  await _analysisLock.WaitAsync(ct);
  try { /* ClrMD work */ }
  finally { _analysisLock.Release(); }
```

- **`ConcurrentDictionary`** for in-memory session/state stores. Initialize with explicit capacity:

```csharp
  new ConcurrentDictionary<Guid, SnapshotMetadata>(
      concurrencyLevel: Environment.ProcessorCount,
      capacity: 64);
```

- **`FrozenDictionary`** for read-only lookup tables built at startup (handler routing, metric name maps).
- **`Interlocked`** for simple counters (snapshots captured, bytes processed, failure counts).
- **`Lock`** (C# 13 `System.Threading.Lock`) for synchronous critical sections. Never in async code.

## JSON Serialization

System.Text.Json only. Configure once at startup:

```csharp
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
};
```

- **`JsonSerializer.SerializeToUtf8Bytes`** over `Serialize` + string for storage/network writes.
- **`Utf8JsonWriter`** for streaming heap analysis output — zero-allocation path.
- **`[JsonSerializable]` source generators** for all types on the hot path (snapshot DTOs, leak reports).

## Serilog Performance

Serilog destructuring of ClrMD objects can allocate massively. Apply destructuring policies:

```csharp
// Never log full ClrMD objects — they're enormous object graphs
// BAD
Log.HeapDiffComplete(logger, clrHeap); // will try to destructure the entire heap

// GOOD — log only extracted scalar values
Log.HeapDiffComplete(logger, typeCount: results.Count, totalBytes: results.TotalSize);
```

- Use `{@obj}` destructuring only for small, known-size DTOs.
- Configure `Destructure.ByTransforming<ClrType>(t => t.Name)` if ClrMD types appear in logs.
- Never log inside the heap walk loop — batch and log summary after the walk completes.

## Allocation Reduction

- **`readonly record struct`** for all message DTOs, `RssMemoryReading`, `HeapMetadata`, `DiagnosticTrigger`.
- **Avoid boxing in hot paths** — never cast value types to `object` in diagnostic loops.
- **No LINQ in hot loops** — heap walk, `/proc` polling, sliding window velocity calculation.
- **`string.Intern`** for ClrMD type names — millions of identical strings per heap walk.
- **`Enum.ToString()` prohibited in hot paths** — use precomputed `FrozenDictionary<TEnum, string>` maps.

## EF Core (if used for snapshot metadata)

- **`.AsNoTracking()`** on all read-only queries.
- **`.Select()`** projections only — never load full entities to read two fields.
- **`EF.CompileAsyncQuery`** for frequently executed queries (latest N snapshots by timestamp).
- **Indexes** on timestamp columns — all snapshot queries are time-range based.
- **N+1 prevention** — use `.Include()` for known navigation loads, never lazy-load in loops.

## Prohibited

- No `new byte[]` for buffers — `ArrayPool<byte>` only
- No `lock` in async code — `SemaphoreSlim` only
- No `.Result` or `.Wait()` on Tasks
- No `Task.Run` for I/O
- No LINQ in diagnostic hot loops (heap walk, `/proc` polling, metrics sliding window)
- No `string.Split` in `/proc` parsers — Span slicing only
- No `File.ReadAllText` for `/proc` reads
- No `Enum.ToString()` in serialization hot paths
- No Newtonsoft.Json
- No tracking EF queries for read-only data
- No parallelizing ClrMD heap enumeration — only post-collection aggregation
- No logging inside the heap walk loop
- No `ConcurrentQueue` + polling — use `Channel<T>`
