using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Presenters;

public sealed class OccShellPresentation
{
    public bool ShowEmptyState { get; init; }

    public bool ShowMainContent { get; init; }

    public string ScopeLabel { get; init; } = string.Empty;

    public string LastRefreshedText { get; init; } = string.Empty;
}

public sealed class OccStatusKpiPresentation
{
    public string OpenThreadCount { get; init; } = "0";

    public string HangingLeadCount { get; init; } = "0";

    public string RevenueAtRisk { get; init; } = "—";

    public string AverageReplyTime { get; init; } = "—";

    public string AverageReplyTimeSubtext { get; init; } = string.Empty;

    public string SlaBreaches { get; init; } = "—";

    public string SlaThresholdSubtext { get; init; } = string.Empty;

    public string ResponseRate { get; init; } = "—";

    public string UrgentCount { get; init; } = "0";

    public string? UrgentTooltip { get; init; }

    public string? UrgentSubtext { get; init; }

    public string? SlaBreachesTooltip { get; init; }

    public string? OpenThreadsSubtext { get; init; }

    public string PeakHour { get; init; } = "—";

    public string DailyTrend { get; init; } = "—";

    [Obsolete("Use UrgentCount")]
    public string ImmediateActionCount => UrgentCount;

    [Obsolete("Use UrgentTooltip")]
    public string? ImmediateActionTooltip => UrgentTooltip;
}

public sealed class OccPillBarPresentation
{
    public string Signature { get; init; } = string.Empty;

    public IReadOnlyList<BranchWorkspacePillItem> Items { get; init; } = [];
}

public sealed class OccImmediateQueuePresentation
{
    public bool ShowEmptyState { get; init; }

    public bool ShowFooter { get; init; }

    public string? FooterText { get; init; }
}

public sealed class OccWorkQueuePresentation
{
    public bool ShowEmptyState { get; init; }

    public string EmptyHint { get; init; } = "No threads match this filter.";
}

public static class OccSnapshotPresenter
{
    public static OccShellPresentation BuildShellPresentation(
        bool hasProfessionalInstances,
        string scopeLabel,
        DateTime refreshedAtLocal) =>
        new()
        {
            ShowEmptyState = !hasProfessionalInstances,
            ShowMainContent = hasProfessionalInstances,
            ScopeLabel = scopeLabel,
            LastRefreshedText = $"Updated {refreshedAtLocal:t}"
        };

    public static OccStatusKpiPresentation BuildStatusKpis(OperationsStatusSnapshot status)
    {
        ArgumentNullException.ThrowIfNull(status);

        var slaThreshold = OperationalThresholds.GetSlaThresholdMinutes();
        string? overlapSubtext = null;
        if (status.OpenThreadCount == status.SlaBreachesNumeric &&
            status.OpenThreadCount > status.UrgentTotal &&
            status.OpenThreadCount > 0)
        {
            overlapSubtext = $"All open exceed SLA ({slaThreshold}m)";
        }

        var urgentTooltip =
            "Urgent threads (high urgency or critical sentiment), excluding SLA-only breaches.";

        return new OccStatusKpiPresentation
        {
            OpenThreadCount = status.OpenThreadCount.ToString(),
            HangingLeadCount = status.HangingLeadCount.ToString(),
            RevenueAtRisk = UnifiedMessengerDashboardPresentationHelper.FormatRevenue(status.TotalRevenueAtRisk),
            AverageReplyTime = status.AverageReplyTime,
            AverageReplyTimeSubtext = status.AverageReplyTimeSubtext,
            SlaBreaches = status.SlaBreaches,
            SlaThresholdSubtext = status.SlaThresholdSubtext,
            ResponseRate = status.ResponseRate,
            UrgentCount = status.UrgentTotal.ToString(),
            PeakHour = status.PeakHour,
            DailyTrend = status.DailyTrend,
            UrgentTooltip = urgentTooltip,
            UrgentSubtext = overlapSubtext is not null && status.UrgentTotal < status.OpenThreadCount
                ? $"{status.UrgentTotal} high-urgency (excl. SLA-only)"
                : null,
            OpenThreadsSubtext = overlapSubtext,
            SlaBreachesTooltip = status.SlaBreachesNumeric <= 0
                ? "No SLA breaches in live workload."
                : "Open the worst SLA breach by wait time."
        };
    }

    public static OccPillBarPresentation BuildPillBar(
        IReadOnlyDictionary<string, BranchWorkspaceHelper.BranchTabCounts> branchTabCounts,
        IReadOnlyList<string> branchNames)
    {
        ArgumentNullException.ThrowIfNull(branchTabCounts);
        ArgumentNullException.ThrowIfNull(branchNames);

        return new OccPillBarPresentation
        {
            Signature = BuildPillBarSignature(branchTabCounts, branchNames),
            Items = BuildBranchPillItems(branchTabCounts, branchNames)
        };
    }

    public static OccImmediateQueuePresentation BuildImmediateQueuePresentation(
        UnifiedMessengerDashboardSnapshot threadOps)
    {
        ArgumentNullException.ThrowIfNull(threadOps);

        var shownCount = threadOps.ImmediateActionQueueCount;
        var totalCount = threadOps.ImmediateActionTotal;
        var showFooter = totalCount > shownCount && shownCount > 0;

        return new OccImmediateQueuePresentation
        {
            ShowEmptyState = threadOps.ImmediateActionQueue.Count == 0,
            ShowFooter = showFooter,
            FooterText = showFooter
                ? $"Showing top {shownCount} of {totalCount} urgent threads"
                : null
        };
    }

    public static OccWorkQueuePresentation BuildWorkQueuePresentation(
        IReadOnlyList<ThreadData> queue,
        OccQueueFilter filter)
    {
        ArgumentNullException.ThrowIfNull(queue);

        var emptyHint = filter switch
        {
            OccQueueFilter.Urgent => "No urgent threads in the current scope.",
            OccQueueFilter.SlaBreach => "No SLA breaches in the current scope.",
            OccQueueFilter.Hanging => "No hanging leads in the current scope.",
            OccQueueFilter.Resolved => "No resolved threads in the current scope.",
            _ => "No open threads in the current scope."
        };

        return new OccWorkQueuePresentation
        {
            ShowEmptyState = queue.Count == 0,
            EmptyHint = emptyHint
        };
    }

    internal static string BuildPillBarSignature(
        IReadOnlyDictionary<string, BranchWorkspaceHelper.BranchTabCounts> branchTabCounts,
        IReadOnlyList<string> branchNames)
    {
        var allBranchesCounts = BranchWorkspaceHelper.SumBranchTabCounts(branchTabCounts);
        var parts = new List<string>
        {
            $"all:{allBranchesCounts.OpenCount}:{allBranchesCounts.ImmediateCount}"
        };

        foreach (var branch in branchNames)
        {
            var counts = branchTabCounts.GetValueOrDefault(branch, new BranchWorkspaceHelper.BranchTabCounts(0, 0));
            parts.Add($"{branch}:{counts.OpenCount}:{counts.ImmediateCount}");
        }

        return string.Join("|", parts);
    }

    internal static IReadOnlyList<BranchWorkspacePillItem> BuildBranchPillItems(
        IReadOnlyDictionary<string, BranchWorkspaceHelper.BranchTabCounts> branchTabCounts,
        IReadOnlyList<string> branchNames)
    {
        var items = new List<BranchWorkspacePillItem>();
        var allBranchesCounts = BranchWorkspaceHelper.SumBranchTabCounts(branchTabCounts);
        items.Add(CreateBranchPillItem("All branches", null, allBranchesCounts));

        foreach (var branch in branchNames)
        {
            var counts = branchTabCounts.GetValueOrDefault(branch, new BranchWorkspaceHelper.BranchTabCounts(0, 0));
            items.Add(CreateBranchPillItem(branch, branch, counts));
        }

        return items;
    }

    private static BranchWorkspacePillItem CreateBranchPillItem(
        string label,
        string? branchKey,
        BranchWorkspaceHelper.BranchTabCounts counts) =>
        new()
        {
            BranchLabel = BranchWorkspaceHelper.FormatBranchPillLabel(label),
            BranchKey = branchKey,
            OpenCount = counts.OpenCount,
            UrgentCount = counts.ImmediateCount,
            BadgeText = BranchWorkspaceHelper.FormatBranchPillBadge(counts),
            TooltipText = BranchWorkspaceHelper.FormatBranchPillTooltip(label, counts)
        };
}
