namespace UnifiedMessenger.Models;

/// <summary>
/// Branch and platform metadata persisted alongside triage items in <c>triage_v2.json</c>.
/// </summary>
public sealed class UnifiedMessengerStoreMetadata
{
    public DateTimeOffset SavedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<UnifiedMessengerBranchRecord> Branches { get; set; } = [];
}

public sealed class UnifiedMessengerBranchRecord
{
    public required string BranchName { get; set; }

    public required string Platform { get; set; }

    public required string InstanceId { get; set; }

    public string InstanceDisplayName { get; set; } = string.Empty;
}
