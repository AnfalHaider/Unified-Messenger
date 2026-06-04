using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class DashboardCardEmptyStateHelper
{
    public const int DefaultUrgentScoreThreshold = 30;

    public const int RecentInboundMaxItems = 8;

    public static int GetUrgentScoreThreshold() =>
        Math.Clamp(AppSettingsService.Instance.Settings.DashboardUrgencyThreshold, 15, 50);

    public static DashboardCardEmptyReason ResolveGoogleTrustEmptyReason(
        bool hasGoogleInstances,
        CustomerTrustSnapshot trust)
    {
        if (!hasGoogleInstances)
        {
            return DashboardCardEmptyReason.NoPlatformInstance;
        }

        if (trust.PendingReviews.Count > 0 || trust.TotalUnrepliedReviews > 0)
        {
            return DashboardCardEmptyReason.HasData;
        }

        return DashboardCardEmptyReason.ConnectedAwaitingScrape;
    }

    public static string FormatGoogleTrustEmptyMessage(DashboardCardEmptyReason reason) =>
        reason switch
        {
            DashboardCardEmptyReason.NoPlatformInstance =>
                "Add a Google Business Profile professional instance to track unreplied reviews.",
            DashboardCardEmptyReason.ConnectedAwaitingScrape =>
                "No unreplied reviews right now. Open Reviews in the instance, then press Refresh on the dashboard.",
            DashboardCardEmptyReason.ConnectedNoData =>
                "No unreplied reviews — you're caught up across connected locations.",
            _ => string.Empty
        };

    public static DashboardCardEmptyReason ResolveMetaResponseEmptyReason(
        bool hasMetaInstances,
        MetaResponseEfficiencySnapshot snapshot)
    {
        if (!hasMetaInstances)
        {
            return DashboardCardEmptyReason.NoPlatformInstance;
        }

        if (snapshot.SampleCount > 0)
        {
            return DashboardCardEmptyReason.HasData;
        }

        if (HasRecentInbound(snapshot))
        {
            return DashboardCardEmptyReason.InboundOnlyAwaitingReply;
        }

        if (snapshot.ActiveUnreadCount > 0)
        {
            return DashboardCardEmptyReason.InboundOnlyAwaitingReply;
        }

        return DashboardCardEmptyReason.ConnectedAwaitingScrape;
    }

    public static string FormatMetaResponseEmptyMessage(DashboardCardEmptyReason reason) =>
        reason switch
        {
            DashboardCardEmptyReason.NoPlatformInstance =>
                "Add a Meta Business Suite professional instance to begin measuring response efficiency.",
            DashboardCardEmptyReason.InboundOnlyAwaitingReply =>
                "Inbound detected. Reply in Meta Business Suite to log response time and efficiency samples.",
            DashboardCardEmptyReason.ConnectedAwaitingScrape =>
                "Open the Meta inbox in the instance, then press Refresh to load response telemetry.",
            DashboardCardEmptyReason.ConnectedNoData =>
                "No Meta response samples yet. Activity will appear after inbox traffic and replies.",
            _ => string.Empty
        };

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
        bool enableLocalAi,
        int triageItemCount,
        int insightCardCount)
    {
        if (insightCardCount > 0)
        {
            return DashboardCardEmptyReason.HasData;
        }

        if (triageItemCount == 0)
        {
            return DashboardCardEmptyReason.NoTriageItems;
        }

        if (!enableLocalAi)
        {
            return DashboardCardEmptyReason.AwaitingLocalAi;
        }

        return DashboardCardEmptyReason.AwaitingLocalAi;
    }

    public static string FormatExecutiveInsightsEmptyMessage(DashboardCardEmptyReason reason) =>
        reason switch
        {
            DashboardCardEmptyReason.NoTriageItems =>
                "When professional inbound messages are triaged, extracted customer details will appear here.",
            DashboardCardEmptyReason.AwaitingLocalAi =>
                "Enable Local AI in Settings and ensure Ollama is running for rich Executive Insights. Heuristic triage may still populate other cards.",
            _ => string.Empty
        };

    public static string BuildBranchScopeSubtitle(
        IEnumerable<MessengerInstance> professionalInstances,
        string? branchInstanceId)
    {
        var list = professionalInstances.ToList();
        var normalizedId = DashboardPageHelper.NormalizeBranchInstanceId(branchInstanceId);
        if (normalizedId is null)
        {
            var count = list.Count;
            return count == 0
                ? "Showing: no professional accounts"
                : $"Showing: All Branches ({count})";
        }

        var branch = list.FirstOrDefault(instance =>
            instance.Id.Equals(normalizedId, StringComparison.OrdinalIgnoreCase));

        return branch is null
            ? "Showing: selected branch"
            : $"Showing: {branch.DisplayName.Trim()}";
    }

    private static bool HasRecentInbound(MetaResponseEfficiencySnapshot snapshot) =>
        !string.IsNullOrWhiteSpace(snapshot.LastInboundDisplay) &&
        !snapshot.LastInboundDisplay.Equals("—", StringComparison.Ordinal);
}
