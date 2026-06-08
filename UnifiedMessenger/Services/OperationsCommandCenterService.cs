using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Facade that composes thread operations, analytics, platform intelligence, and deduped AI insights
/// for the Operations Command Center dashboard.
/// </summary>
public sealed class OperationsCommandCenterService
{
    private static readonly Lazy<OperationsCommandCenterService> LazyInstance =
        new(() => new OperationsCommandCenterService());

    public static OperationsCommandCenterService Instance => LazyInstance.Value;

    public OperationsCommandCenterSnapshot BuildSnapshot(
        IEnumerable<MessengerInstance> professionalInstances,
        string? selectedBranchKey = null,
        NotificationHub? notificationHub = null,
        MessageTriageService? triageService = null,
        UnifiedMessengerDashboardService? threadDashboardService = null,
        ProfessionalWorkspaceService? professionalWorkspaceService = null,
        bool? includeHeuristicInsights = null)
    {
        ArgumentNullException.ThrowIfNull(professionalInstances);

        var hub = notificationHub ?? NotificationHub.Instance;
        var triage = triageService ?? MessageTriageService.Instance;
        var threadService = threadDashboardService ?? UnifiedMessengerDashboardService.Instance;
        var workspace = professionalWorkspaceService ?? ProfessionalWorkspaceService.Instance;

        var filteredInstances = DashboardPageHelper
            .FilterProfessionalInstances(professionalInstances, selectedBranchKey)
            .Where(instance => instance.IsProfessional && !string.IsNullOrWhiteSpace(instance.Id))
            .ToList();

        var normalizedBranchKey = BranchWorkspaceHelper.NormalizeBranchKey(selectedBranchKey);
        var threadOperations = threadService.BuildSnapshot(filteredInstances, normalizedBranchKey);
        var telemetry = DashboardPageHelper.CaptureProfessionalDashboardTelemetry(
            professionalInstances,
            hub,
            normalizedBranchKey);

        var analytics = telemetry.Snapshot;
        var display = telemetry.Display;

        var googleInstances = filteredInstances
            .Where(instance => instance.Platform.Equals("googlebusiness", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var metaInstances = filteredInstances
            .Where(instance => instance.Platform.Equals("metabusiness", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var customerTrust = workspace.CaptureCustomerTrust(googleInstances);
        var metaResponse = workspace.CaptureMetaResponseEfficiency(metaInstances);

        var allowedIds = filteredInstances
            .Select(instance => instance.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var triageItems = triage.GetAllItems()
            .Where(item => allowedIds.Contains(item.InstanceId))
            .ToList();

        var showHeuristic = includeHeuristicInsights ??
                            AppSettingsService.Instance.Settings.ShowHeuristicExecutiveInsights;

        var insightFeed = OperationsCommandCenterInsightFeedBuilder.Build(
            threadOperations,
            triageItems,
            showHeuristic);

        var activeSlaBreaches = OperationalMetricsHelper.CountActiveSlaBreaches(threadOperations.AllThreads);
        var mergedHighlights = OperationalMetricsHelper.BuildHighlights(
            filteredInstances,
            threadOperations.AllThreads,
            analytics.Highlights);

        return new OperationsCommandCenterSnapshot
        {
            ScopeLabel = DashboardCardEmptyStateHelper.BuildBranchScopeSubtitle(
                professionalInstances.Where(instance => instance.IsProfessional),
                normalizedBranchKey),
            SelectedBranchKey = normalizedBranchKey,
            FilteredInstances = filteredInstances,
            ThreadOperations = threadOperations,
            Status = BuildStatusSnapshot(threadOperations, analytics, display, activeSlaBreaches),
            PlatformIntelligence = BuildPlatformIntelligenceSnapshot(
                googleInstances,
                metaInstances,
                customerTrust,
                metaResponse),
            AnalyticsTrends = BuildAnalyticsTrendSnapshot(analytics, mergedHighlights),
            AiInsightFeed = insightFeed,
            InstanceHealthChips = DashboardDataHealthHelper.BuildProfessionalHealthChips(
                filteredInstances,
                triage)
        };
    }

    private static OperationsStatusSnapshot BuildStatusSnapshot(
        UnifiedMessengerDashboardSnapshot threadOperations,
        ProfessionalAnalyticsSnapshot analytics,
        ProfessionalDashboardDisplay display,
        int activeSlaBreaches)
    {
        var hasOpenThreads = threadOperations.OpenThreadCount > 0;
        var slaDisplay = hasOpenThreads
            ? activeSlaBreaches.ToString()
            : display.SlaBreaches;
        var slaThreshold = AppSettingsService.Instance.Settings.SlaThresholdMinutes;
        var slaSubtext = hasOpenThreads
            ? $"Active waiting threads · threshold: {slaThreshold} min"
            : display.SlaThresholdSubtext;

        return new OperationsStatusSnapshot
        {
            OpenThreadCount = threadOperations.OpenThreadCount,
            HangingLeadCount = threadOperations.HangingLeadCount,
            ImmediateActionCount = threadOperations.ImmediateActionCount,
            ImmediateActionQueueCount = threadOperations.ImmediateActionQueueCount,
            TotalRevenueAtRisk = threadOperations.TotalRevenueAtRisk,
            AverageReplyTime = display.AverageReplyTime,
            AverageReplyTimeSubtext = display.AverageReplyTimeSubtext,
            SlaBreaches = slaDisplay,
            SlaBreachesNumeric = hasOpenThreads ? activeSlaBreaches : analytics.SlaBreaches,
            SlaThresholdSubtext = slaSubtext,
            ResponseRate = display.ResponseRate,
            PeakHour = display.PeakHour,
            DailyTrend = display.DailyTrend,
            SentCount = analytics.SentCount,
            ReceivedCount = analytics.ReceivedCount,
            HasMessageVolume = display.HasMessageVolume,
            HasReplyMetrics = display.HasReplyMetrics,
            PlatformHealth = threadOperations.PlatformHealth
        };
    }

    private static OperationsPlatformIntelligenceSnapshot BuildPlatformIntelligenceSnapshot(
        IReadOnlyList<MessengerInstance> googleInstances,
        IReadOnlyList<MessengerInstance> metaInstances,
        CustomerTrustSnapshot customerTrust,
        MetaResponseEfficiencySnapshot metaResponse) =>
        new()
        {
            CustomerTrust = customerTrust,
            CustomerTrustDisplay = DashboardPageHelper.BuildCustomerTrustDisplay(customerTrust),
            MetaResponse = metaResponse,
            MetaResponseDisplay = DashboardPageHelper.BuildMetaResponseDisplay(metaResponse),
            HasGoogleInstances = googleInstances.Count > 0,
            HasMetaInstances = metaInstances.Count > 0,
            GoogleInstanceIds = googleInstances.Select(instance => instance.Id).ToList(),
            MetaInstanceIds = metaInstances.Select(instance => instance.Id).ToList()
        };

    private static OperationsAnalyticsTrendSnapshot BuildAnalyticsTrendSnapshot(
        ProfessionalAnalyticsSnapshot analytics,
        IReadOnlyList<OperationalHighlightItem> highlights) =>
        new()
        {
            WeeklyActivity = analytics.WeeklyActivity,
            Triage = analytics.Triage,
            Highlights = highlights,
            SentCount = analytics.SentCount,
            ReceivedCount = analytics.ReceivedCount,
            HasMessageVolume = analytics.HasMessageVolume,
            HasReplyMetrics = analytics.HasReplyMetrics
        };
}
