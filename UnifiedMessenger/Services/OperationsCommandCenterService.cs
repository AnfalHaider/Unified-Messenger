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
        string? branchInstanceId = null,
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
            .FilterProfessionalInstances(professionalInstances, branchInstanceId)
            .Where(instance => instance.IsProfessional && !string.IsNullOrWhiteSpace(instance.Id))
            .ToList();

        var normalizedBranchId = DashboardPageHelper.NormalizeBranchInstanceId(branchInstanceId);
        var threadOperations = threadService.BuildSnapshot(filteredInstances, normalizedBranchId);
        var telemetry = DashboardPageHelper.CaptureProfessionalDashboardTelemetry(
            professionalInstances,
            hub,
            normalizedBranchId);

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

        return new OperationsCommandCenterSnapshot
        {
            ScopeLabel = DashboardCardEmptyStateHelper.BuildBranchScopeSubtitle(
                professionalInstances.Where(instance => instance.IsProfessional),
                normalizedBranchId),
            BranchInstanceId = normalizedBranchId,
            FilteredInstances = filteredInstances,
            ThreadOperations = threadOperations,
            Status = BuildStatusSnapshot(threadOperations, analytics, display),
            PlatformIntelligence = BuildPlatformIntelligenceSnapshot(
                googleInstances,
                metaInstances,
                customerTrust,
                metaResponse),
            AnalyticsTrends = BuildAnalyticsTrendSnapshot(analytics),
            AiInsightFeed = insightFeed,
            InstanceHealthChips = DashboardDataHealthHelper.BuildProfessionalHealthChips(
                filteredInstances,
                triage)
        };
    }

    private static OperationsStatusSnapshot BuildStatusSnapshot(
        UnifiedMessengerDashboardSnapshot threadOperations,
        ProfessionalAnalyticsSnapshot analytics,
        ProfessionalDashboardDisplay display) =>
        new()
        {
            OpenThreadCount = threadOperations.OpenThreadCount,
            HangingLeadCount = threadOperations.HangingLeadCount,
            ImmediateActionCount = threadOperations.ImmediateActionCount,
            TotalRevenueAtRisk = threadOperations.TotalRevenueAtRisk,
            AverageReplyTime = display.AverageReplyTime,
            AverageReplyTimeSubtext = display.AverageReplyTimeSubtext,
            SlaBreaches = display.SlaBreaches,
            SlaBreachesNumeric = analytics.SlaBreaches,
            SlaThresholdSubtext = display.SlaThresholdSubtext,
            ResponseRate = display.ResponseRate,
            PeakHour = display.PeakHour,
            DailyTrend = display.DailyTrend,
            SentCount = analytics.SentCount,
            ReceivedCount = analytics.ReceivedCount,
            HasMessageVolume = display.HasMessageVolume,
            HasReplyMetrics = display.HasReplyMetrics,
            PlatformHealth = threadOperations.PlatformHealth
        };

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
        ProfessionalAnalyticsSnapshot analytics) =>
        new()
        {
            WeeklyActivity = analytics.WeeklyActivity,
            Triage = analytics.Triage,
            Highlights = analytics.Highlights,
            SentCount = analytics.SentCount,
            ReceivedCount = analytics.ReceivedCount,
            HasMessageVolume = analytics.HasMessageVolume,
            HasReplyMetrics = analytics.HasReplyMetrics
        };
}
