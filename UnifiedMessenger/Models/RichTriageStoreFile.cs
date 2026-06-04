namespace UnifiedMessenger.Models;

public sealed class RichTriageStoreFile
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public List<MessageTriageItem> Items { get; set; } = [];
}
