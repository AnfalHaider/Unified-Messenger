namespace UnifiedMessenger.Models;

public sealed class RichTriageStoreFile
{
    public const int CurrentVersion = 4;

    public int Version { get; set; } = CurrentVersion;

    public List<MessageTriageItem> Items { get; set; } = [];

    public List<ThreadData> Threads { get; set; } = [];

    public List<ThreadDisplayOrderEntry> DisplayOrders { get; set; } = [];

    public UnifiedMessengerStoreMetadata Metadata { get; set; } = new();
}
