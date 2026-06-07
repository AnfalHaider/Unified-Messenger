namespace UnifiedMessenger.Services;

public enum RichTriageStoreLoadStatus
{
    Loaded,
    CreatedEmpty,
    CorruptRecovered,
    Failed
}

public sealed class RichTriageStoreLoadResult
{
    public RichTriageStoreLoadStatus Status { get; init; }

    public string? UserMessage { get; init; }

    public string? BackupPath { get; init; }
}
