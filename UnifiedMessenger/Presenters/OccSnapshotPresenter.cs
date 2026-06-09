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

    public string ImmediateActionCount { get; init; } = "0";

    public string PeakHour { get; init; } = "—";

    public string DailyTrend { get; init; } = "—";

    public string? ImmediateActionTooltip { get; init; }
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

        string? tooltip = null;
        if (status.ImmediateActionTotal > status.ImmediateActionQueueCount &&
            status.ImmediateActionQueueCount > 0)
        {
            tooltip =
                $"{status.ImmediateActionTotal} urgent threads in scope. The action lane shows the top {status.ImmediateActionQueueCount}.";
        }
        else
        {
            tooltip = "All urgent threads in scope. The action lane shows the top 24 by urgency.";
        }

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
            ImmediateActionCount = status.ImmediateActionTotal.ToString(),
            PeakHour = status.PeakHour,
            DailyTrend = status.DailyTrend,
            ImmediateActionTooltip = tooltip
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
