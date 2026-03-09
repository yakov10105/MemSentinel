namespace MemSentinel.Core.Providers;

public readonly record struct HeapMetadata(
    long Gen0Bytes,
    long Gen1Bytes,
    long Gen2Bytes,
    long LohBytes,
    long PohBytes,
    DateTimeOffset CapturedAt);
