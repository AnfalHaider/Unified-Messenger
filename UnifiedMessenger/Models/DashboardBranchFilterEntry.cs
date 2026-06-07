namespace UnifiedMessenger.Models;

/// <summary>
/// ComboBox item for dashboard branch filtering. "All Branches" aggregates every professional inbox.
/// </summary>
public sealed class DashboardBranchFilterEntry
{
    public bool IsAllBranches { get; set; }

    public string BranchKey { get; set; } = string.Empty;

    public int InboxCount { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public static DashboardBranchFilterEntry CreateAllBranches(int inboxCount = 0) =>
        new()
        {
            IsAllBranches = true,
            InboxCount = inboxCount,
            DisplayName = inboxCount <= 0
                ? "All Branches"
                : $"All Branches ({inboxCount} inbox{(inboxCount == 1 ? "" : "es")})"
        };

    public static DashboardBranchFilterEntry FromBranch(string branchKey, int inboxCount) =>
        new()
        {
            IsAllBranches = false,
            BranchKey = branchKey.Trim(),
            InboxCount = inboxCount,
            DisplayName = inboxCount <= 1
                ? branchKey.Trim()
                : $"{branchKey.Trim()} ({inboxCount} inboxes)"
        };
}
