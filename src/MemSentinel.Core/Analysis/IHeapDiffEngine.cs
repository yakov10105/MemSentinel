using MemSentinel.Contracts;
using MemSentinel.Core.Common;

namespace MemSentinel.Core.Analysis;

public interface IHeapDiffEngine
{
    Task<Result<HeapDiffReport>> DiffAsync(string pathA, string pathB, CancellationToken ct);
}
