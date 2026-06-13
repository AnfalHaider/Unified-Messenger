using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Composes thread operations and basic KPIs for the Operations Command Center.
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
        UnifiedMessengerDashboardService? threadDashboardService = null) =>
        BuildSnapshot(
            professionalInstances,
            selectedBranchKey,
            fromUtc: null,
            toUtc: null,
            viewMode: OccViewMode.Live,
            notificationHub,
            triageService,
            threadDashboardService);

    public OperationsCommandCenterSnapshot BuildSnapshot(
        IEnumerable<MessengerInstance> professionalInstances,
        string? selectedBranchKey,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        OccViewMode viewMode = OccViewMode.Live,
        NotificationHub? notificationHub = null,
        MessageTriageService? triageService = null,
        UnifiedMessengerDashboardService? threadDashboardService = null)
    {
        ArgumentNullException.ThrowIfNull(professionalInstances);

        var hub = notificationHub ?? NotificationHub.Instance;
        var triage = triageService ?? MessageTriageService.Instance;
        var threadService = threadDashboardService ?? UnifiedMessengerDashboardService.Instance;

        var filteredInstances = professionalInstances
            .Where(instance => instance.IsProfessional && !string.IsNullOrWhiteSpace(instance.Id))
            .ToList();
        filteredInstances = DashboardPageHelper
            .FilterProfessionalInstances(filteredInstances, selectedBranchKey)
            .ToList();

        var normalizedBranchKey = BranchWorkspaceHelper.NormalizeBranchKey(selectedBranchKey);
        var threadFromUtc = viewMode == OccViewMode.Historical ? fromUtc : null;
        var threadToUtc = viewMode == OccViewMode.Historical ? toUtc : null;
        var threadOperations = threadService.BuildSnapshot(
            filteredInstances,
            normalizedBranchKey,
            threadFromUtc,
            threadToUtc);
        var telemetry = DashboardPageHelper.CaptureProfessionalDashboardTelemetry(
            filteredInstances,
            hub,
            normalizedBranchKey,
            fromUtc,
            toUtc);

        var analytics = telemetry.Snapshot;
        var display = telemetry.Display;
        var activeSlaBreaches = OperationalMetricsHelper.CountActiveSlaBreaches(threadOperations.AllThreads);

        return new OperationsCommandCenterSnapshot
        {
            ScopeLabel = DashboardCardEmptyStateHelper.BuildBranchScopeSubtitle(
                filteredInstances,
                normalizedBranchKey),
            SelectedBranchKey = normalizedBranchKey,
            FilteredInstances = filteredInstances,
            ThreadOperations = threadOperations,
            Status = BuildStatusSnapshot(threadOperations, analytics, display, activeSlaBreaches),
            AnalyticsTrends = new OperationsAnalyticsTrendSnapshot
            {
                WeeklyActivity = analytics.WeeklyActivity,
                SentCount = analytics.SentCount,
                ReceivedCount = analytics.ReceivedCount,
                HasMessageVolume = analytics.HasMessageVolume,
                HasReplyMetrics = analytics.HasReplyMetrics,
                Triage = analytics.Triage,
                Highlights = analytics.Highlights
            },
            InstanceHealthChips = DashboardDataHealthHelper.BuildProfessionalHealthChips(
                filteredInstances,
                triage)
        };
    }

    public OperationsStatusSnapshot BuildStatusOnly(
        IEnumerable<MessengerInstance> professionalInstances,
        string? selectedBranchKey = null,
        NotificationHub? notificationHub = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        OccViewMode viewMode = OccViewMode.Live)
    {
        ArgumentNullException.ThrowIfNull(professionalInstances);

        var hub = notificationHub ?? NotificationHub.Instance;
        var threadService = UnifiedMessengerDashboardService.Instance;
        var normalizedBranchKey = BranchWorkspaceHelper.NormalizeBranchKey(selectedBranchKey);
        var filteredInstances = professionalInstances
            .Where(instance => instance.IsProfessional && !string.IsNullOrWhiteSpace(instance.Id))
            .ToList();
        filteredInstances = DashboardPageHelper
            .FilterProfessionalInstances(filteredInstances, normalizedBranchKey)
            .ToList();

        var threadFromUtc = viewMode == OccViewMode.Historical ? fromUtc : null;
        var threadToUtc = viewMode == OccViewMode.Historical ? toUtc : null;
        var threadOperations = threadService.BuildSnapshot(
            filteredInstances,
            normalizedBranchKey,
            threadFromUtc,
            threadToUtc);
        var telemetry = DashboardPageHelper.CaptureProfessionalDashboardTelemetry(
            filteredInstances,
            hub,
            normalizedBranchKey,
            fromUtc,
            toUtc);

        var activeSlaBreaches = OperationalMetricsHelper.CountActiveSlaBreaches(threadOperations.AllThreads);

        return BuildStatusSnapshot(
            threadOperations,
            telemetry.Snapshot,
            telemetry.Display,
            activeSlaBreaches);
    }

    private static OperationsStatusSnapshot BuildStatusSnapshot(
        UnifiedMessengerDashboardSnapshot threadOperations,
        ProfessionalAnalyticsSnapshot analytics,
        ProfessionalDashboardDisplay display,
        int activeSlaBreaches) =>
        new()
        {
            OpenThreadCount = threadOperations.OpenThreadCount,
            HangingLeadCount = threadOperations.HangingLeadCount,
            ImmediateActionCount = threadOperations.ImmediateActionTotal,
            ImmediateActionQueueCount = threadOperations.ImmediateActionQueue.Count,
            TotalRevenueAtRisk = threadOperations.TotalRevenueAtRisk,
            AverageReplyTime = display.AverageReplyTime,
            AverageReplyTimeSubtext = display.AverageReplyTimeSubtext,
            SlaBreaches = activeSlaBreaches > 0 ? activeSlaBreaches.ToString() : "—",
            SlaBreachesNumeric = activeSlaBreaches,
            SlaThresholdSubtext = display.SlaThresholdSubtext,
            ResponseRate = display.ResponseRate,
            PeakHour = analytics.PeakHourDisplay,
            DailyTrend = analytics.DailyTrendDisplay,
            SentCount = analytics.SentCount,
            ReceivedCount = analytics.ReceivedCount,
            HasMessageVolume = analytics.HasMessageVolume,
            HasReplyMetrics = display.HasReplyMetrics,
            PlatformHealth = threadOperations.PlatformHealth
        };
}
