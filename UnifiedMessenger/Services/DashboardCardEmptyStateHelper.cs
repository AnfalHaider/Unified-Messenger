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
                "Inbound detected in Meta Business Suite. Open a conversation in the instance for AI triage; reply there to log response time samples.",
            DashboardCardEmptyReason.ConnectedAwaitingScrape =>
                "Open a Meta conversation in the instance for AI triage, then press Refresh to load response telemetry.",
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
        string? selectedBranchKey) =>
        BranchWorkspaceHelper.BuildScopeLabel(
            professionalInstances
                .Where(instance => instance.IsProfessional && !string.IsNullOrWhiteSpace(instance.Id))
                .ToList(),
            selectedBranchKey);

    /// <summary>
    /// True when Meta/Google platform intelligence has actionable alerts worth surfacing in OCC.
    /// </summary>
    public static bool ShouldAutoExpandPlatformIntelligence(OperationsPlatformIntelligenceSnapshot platform)
    {
        if (!platform.HasGoogleInstances && !platform.HasMetaInstances)
        {
            return false;
        }

        if (platform.HasGoogleInstances &&
            (platform.CustomerTrust.TotalUnrepliedReviews > 0 || platform.CustomerTrust.PendingReviews.Count > 0))
        {
            return true;
        }

        if (!platform.HasMetaInstances)
        {
            return false;
        }

        return ResolveMetaResponseEmptyReason(platform.HasMetaInstances, platform.MetaResponse) ==
               DashboardCardEmptyReason.InboundOnlyAwaitingReply;
    }

    /// <summary>
    /// Monotonic signal used to detect new platform alerts after the user collapses the expander.
    /// </summary>
    public static int ComputePlatformIntelligenceAlertSignal(OperationsPlatformIntelligenceSnapshot platform)
    {
        var signal = 0;

        if (platform.HasGoogleInstances)
        {
            signal += platform.CustomerTrust.TotalUnrepliedReviews;
            signal += platform.CustomerTrust.PendingReviews.Count;
        }

        if (platform.HasMetaInstances)
        {
            signal += platform.MetaResponse.ActiveUnreadCount;
            if (HasRecentInbound(platform.MetaResponse))
            {
                signal += 1;
            }
        }

        return signal;
    }

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

    private static bool HasRecentInbound(MetaResponseEfficiencySnapshot snapshot) =>
        !string.IsNullOrWhiteSpace(snapshot.LastInboundDisplay) &&
        !snapshot.LastInboundDisplay.Equals("—", StringComparison.Ordinal);
}
