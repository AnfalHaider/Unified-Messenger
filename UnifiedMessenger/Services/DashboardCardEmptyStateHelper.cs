using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class DashboardCardEmptyStateHelper
{
    public const int DefaultUrgentScoreThreshold = 30;

    public const int RecentInboundMaxItems = 8;

    public static int GetUrgentScoreThreshold() =>
        Math.Clamp(AppSettingsService.Instance.Settings.DashboardUrgencyThreshold, 15, 50);

    public static bool IsUrgentQueueItem(MessageTriageItem item) =>
        !item.IsSpamOrPromo &&
        (item.OperationalUrgency >= 4 ||
         item.ClientSentiment.Equals(ClientSentimentLabel.Critical, StringComparison.OrdinalIgnoreCase));

    public static DashboardCardEmptyReason ResolveUrgencyEmptyReason(MessageTriageDashboardSnapshot triage)
    {
        var total = triage.PositiveCount + triage.NeutralCount + triage.NegativeCount;
        if (total == 0)
        {
            return DashboardCardEmptyReason.NoTriageItems;
        }

        if (triage.UrgentQueue.Count > 0)
        {
            return DashboardCardEmptyReason.HasData;
        }

        if (triage.RecentInbound.Count > 0)
        {
            return DashboardCardEmptyReason.OnlyLowUrgencyItems;
        }

        return DashboardCardEmptyReason.NoTriageItems;
    }

    public static string FormatUrgencyEmptyMessage(DashboardCardEmptyReason reason) =>
        reason switch
        {
            DashboardCardEmptyReason.NoTriageItems =>
                "Inbound professional messages will be scored and ranked here after triage runs.",
            DashboardCardEmptyReason.OnlyLowUrgencyItems =>
                "No urgent items right now. See Recent inbound below.",
            _ => string.Empty
        };

    public static DashboardCardEmptyReason ResolveExecutiveInsightsEmptyReason(
        int triageItemCount,
        int insightCardCount)
    {
        if (insightCardCount > 0)
        {
            return DashboardCardEmptyReason.HasData;
        }

        return triageItemCount == 0
            ? DashboardCardEmptyReason.NoTriageItems
            : DashboardCardEmptyReason.ConnectedNoData;
    }

    public static string FormatExecutiveInsightsEmptyMessage(DashboardCardEmptyReason reason) =>
        reason switch
        {
            DashboardCardEmptyReason.NoTriageItems =>
                "When professional inbound messages are triaged, customer details will appear here.",
            DashboardCardEmptyReason.ConnectedNoData =>
                "No executive insight cards yet for the current triage queue.",
            _ => string.Empty
        };

    public static string BuildBranchScopeSubtitle(
        IEnumerable<MessengerInstance> professionalInstances,
        string? selectedBranchKey) =>
        BranchWorkspaceHelper.BuildScopeLabel(
            professionalInstances
                .Where(instance => instance.IsProfessional && !string.IsNullOrWhiteSpace(instance.Id))
                .ToList(),
            selectedBranchKey);

    /// <summary>
    /// True when analytics trends have actionable SLA, urgency, or sentiment signals worth surfacing.
    /// </summary>
    public static bool ShouldAutoExpandAnalyticsTrends(
        OperationsStatusSnapshot status,
        OperationsAnalyticsTrendSnapshot analytics)
    {
        if (status.ImmediateActionCount > 0)
        {
            return true;
        }

        if (status.OpenThreadCount > 0 && status.SlaBreachesNumeric > 0)
        {
            return true;
        }

        if (analytics.Highlights.Count > 0)
        {
            return true;
        }

        var triage = analytics.Triage;
        return triage.NegativeCount > 0 && triage.TotalTriageCount >= 2;
    }

    /// <summary>
    /// Monotonic signal used to detect new analytics alerts after the user collapses the expander.
    /// </summary>
    public static int ComputeAnalyticsTrendsAlertSignal(
        OperationsStatusSnapshot status,
        OperationsAnalyticsTrendSnapshot analytics)
    {
        var signal = status.ImmediateActionCount;
        signal += status.SlaBreachesNumeric * 10;
        signal += analytics.Highlights.Count;
        signal += analytics.Triage.NegativeCount;
        return signal;
    }

    /// <summary>
    /// True when the live alert signal exceeds the persisted dismissed signal.
    /// </summary>
    public static bool ShouldExpandOccSection(int alertSignal, int dismissedSignal) =>
        alertSignal > 0 && alertSignal > dismissedSignal;
}
