namespace UnifiedMessenger.Models;

public sealed class RichTriageStoreFile
{
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;

    public List<MessageTriageItem> Items { get; set; } = [];

    public List<ThreadData> Threads { get; set; } = [];

    public UnifiedMessengerStoreMetadata Metadata { get; set; } = new();
}
