using System.Runtime.InteropServices;

namespace MemSentinel.TestTarget;

internal static class LeakStore
{
    private static readonly List<byte[]> _managedArrays = new();
    private static readonly List<string> _strings = new();
    private static readonly List<GCHandle> _pinnedHandles = new();
    private static readonly List<HttpClient> _httpClients = new();
    private static readonly Lock _sync = new();

    internal static (int arrayCount, int stringCount, long estimatedBytes) AddManaged()
    {
        lock (_sync)
        {
            for (var i = 0; i < 500; i++)
                _managedArrays.Add(new byte[2048]);

            for (var i = 0; i < 200; i++)
                _strings.Add(new string('x', 1024));

            for (var i = 0; i < 10; i++)
                _managedArrays.Add(new byte[92_160]);

            var estimatedBytes = (_managedArrays.Count * 2048L) + (_strings.Count * 1024L) + (10L * 92_160);
            return (_managedArrays.Count, _strings.Count, estimatedBytes);
        }
    }

    internal static (int handleCount, int clientCount) AddUnmanaged()
    {
        lock (_sync)
        {
            for (var i = 0; i < 5; i++)
            {
                var buffer = new byte[262_144];
                _pinnedHandles.Add(GCHandle.Alloc(buffer, GCHandleType.Pinned));
            }

            for (var i = 0; i < 10; i++)
                _httpClients.Add(new HttpClient());

            return (_pinnedHandles.Count, _httpClients.Count);
        }
    }

    internal static void Reset()
    {
        lock (_sync)
        {
            foreach (var handle in _pinnedHandles)
            {
                if (handle.IsAllocated)
                    handle.Free();
            }

            foreach (var client in _httpClients)
                client.Dispose();

            _pinnedHandles.Clear();
            _httpClients.Clear();
            _managedArrays.Clear();
            _strings.Clear();
        }

        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true);
    }
}
