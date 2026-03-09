namespace MemSentinel.Core.Providers;

public readonly record struct RssMemoryReading(
    long RssBytes,
    long PssBytes,
    long VmSizeBytes,
    DateTimeOffset CapturedAt);
