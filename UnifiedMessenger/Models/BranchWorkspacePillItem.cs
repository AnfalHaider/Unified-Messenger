namespace UnifiedMessenger.Models;

public sealed class BranchWorkspacePillItem
{
    public required string BranchLabel { get; init; }

    /// <summary>Null when this pill represents all branches.</summary>
    public string? BranchKey { get; init; }

    public int OpenCount { get; init; }

    public int UrgentCount { get; init; }

    public string BadgeText { get; init; } = string.Empty;

    public string TooltipText { get; init; } = string.Empty;

    public bool HasUrgent => UrgentCount > 0;
}
