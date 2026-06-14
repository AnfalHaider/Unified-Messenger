using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Services;

public sealed class UnifiedMessengerDashboardService
{
    private static readonly Lazy<UnifiedMessengerDashboardService> LazyInstance =
        new(() => new UnifiedMessengerDashboardService());

    private static readonly string[] MonitoredPlatformIds = ["whatsapp", "whatsappbusiness"];

    public const int ImmediateActionQueueDisplayLimit = 24;

    public static UnifiedMessengerDashboardService Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    public UnifiedMessengerDashboardSnapshot BuildSnapshot(
        IEnumerable<MessengerInstance> professionalInstances,
        string? selectedBranchKey = null) =>
        BuildSnapshot(professionalInstances, selectedBranchKey, fromUtc: null, toUtc: null);

    public UnifiedMessengerDashboardSnapshot BuildSnapshot(
        IEnumerable<MessengerInstance> professionalInstances,
        string? selectedBranchKey,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        ThreadRegistryService.Instance.RefreshOperationalFlags(raiseChanged: false);

        var instances = professionalInstances
            .Where(instance => instance.IsProfessional && !string.IsNullOrWhiteSpace(instance.Id))
            .Where(instance => PlatformModuleSettingsHelper.IsPlatformModuleEnabled(instance.Platform))
            .ToList();

        var scopedInstances = DashboardPageHelper
            .FilterProfessionalInstances(instances, selectedBranchKey)
            .ToList();

        var instanceById = instances.ToDictionary(
            instance => instance.Id,
            StringComparer.OrdinalIgnoreCase);

        List<ThreadData> threads = BranchWorkspaceHelper
            .FilterThreadsForBranchWorkspace(
                ThreadRegistryService.Instance.GetAllThreads(),
                instanceById,
                selectedBranchKey)
            .ToList();

        if (fromUtc is not null || toUtc is not null)
        {
            threads = OccDateRangeFilterHelper
                .FilterByTimestamp(threads, thread => thread.LastMessageTime, fromUtc, toUtc)
                .ToList();
        }

        threads = threads
            .OrderByDescending(thread => thread.LastMessageTime)
            .ToList();

        var displayOrder = ThreadDisplayOrderService.Instance;

        var branchNames = BranchWorkspaceHelper.CollectBranchKeys(scopedInstances, threads);

        var branchMetrics = branchNames
            .Select(branch => BranchWorkspaceHelper.BuildBranchMetrics(branch, threads, instances))
            .ToList();

        var actionableThreads = threads.Where(thread => !thread.IsSpamOrPromo).ToList();

        var revenueAtRisk = actionableThreads
            .Where(thread => thread.IsRevenueLeakageRisk)
            .Sum(thread => thread.EstimatedValue);

        var immediateQueue = displayOrder
            .SortImmediateQueue(
                actionableThreads.Where(thread => thread.IsImmediateAction && !thread.IsReplied))
            .Take(ImmediateActionQueueDisplayLimit)
            .ToList();

        var workQueue = BuildWorkQueue(actionableThreads, OccQueueFilter.AllOpen, displayOrder);

        var orderedThreads = threads
            .GroupBy(thread => thread.KanbanColumn)
            .SelectMany(group => displayOrder.SortThreadsForKanbanColumn(group, group.Key))
            .ToList();

        var openThreads = actionableThreads.Count(thread => !thread.IsReplied);
        var hangingLeads = actionableThreads.Count(thread =>
            !thread.IsReplied && thread.KanbanColumn == UnifiedMessengerKanbanColumn.HangingLeads);
        var urgentCount = actionableThreads.Count(thread => thread.IsUrgent && !thread.IsReplied);
        var immediateCount = actionableThreads.Count(thread => thread.IsImmediateAction && !thread.IsReplied);

        return new UnifiedMessengerDashboardSnapshot
        {
            BranchMetrics = branchMetrics,
            TotalRevenueAtRisk = revenueAtRisk,
            PlatformHealth = BuildPlatformHealth(scopedInstances),
            ImmediateActionQueue = immediateQueue,
            WorkQueue = workQueue,
            AllThreads = orderedThreads,
            BranchNames = branchNames,
            OpenThreadCount = openThreads,
            HangingLeadCount = hangingLeads,
            ImmediateActionCount = immediateCount,
            UrgentCount = urgentCount,
            ImmediateActionQueueCount = immediateQueue.Count
        };
    }

    public IReadOnlyList<ThreadData> BuildWorkQueue(
        IEnumerable<ThreadData> actionableThreads,
        OccQueueFilter filter,
        ThreadDisplayOrderService? displayOrder = null)
    {
        ArgumentNullException.ThrowIfNull(actionableThreads);

        var order = displayOrder ?? ThreadDisplayOrderService.Instance;
        var filtered = filter switch
        {
            OccQueueFilter.AllOpen => actionableThreads.Where(thread => !thread.IsReplied),
            OccQueueFilter.Urgent => actionableThreads.Where(thread => thread.IsUrgent && !thread.IsReplied),
            OccQueueFilter.SlaBreach => actionableThreads.Where(thread => thread.IsSlaBreached),
            OccQueueFilter.Hanging => actionableThreads.Where(thread =>
                !thread.IsReplied && thread.KanbanColumn == UnifiedMessengerKanbanColumn.HangingLeads),
            OccQueueFilter.Resolved => actionableThreads.Where(thread => thread.IsReplied || thread.IsSpamOrPromo),
            _ => actionableThreads.Where(thread => !thread.IsReplied)
        };

        return order.SortWorkQueue(filtered).ToList();
    }

    /// <summary>
    /// Computes thread KPI counts without building kanban lists, branch metrics, or platform health.
    /// </summary>
    public UnifiedMessengerThreadMetrics BuildThreadMetricsOnly(
        IEnumerable<MessengerInstance> professionalInstances,
        string? selectedBranchKey = null)
    {
        ThreadRegistryService.Instance.RefreshOperationalFlags(raiseChanged: false);

        var instances = professionalInstances
            .Where(instance => instance.IsProfessional && !string.IsNullOrWhiteSpace(instance.Id))
            .Where(instance => PlatformModuleSettingsHelper.IsPlatformModuleEnabled(instance.Platform))
            .ToList();

        var instanceById = instances.ToDictionary(
            instance => instance.Id,
            StringComparer.OrdinalIgnoreCase);

        var actionableThreads = BranchWorkspaceHelper
            .FilterThreadsForBranchWorkspace(
                ThreadRegistryService.Instance.GetAllThreads(),
                instanceById,
                selectedBranchKey)
            .Where(thread => !thread.IsSpamOrPromo)
            .ToList();

        var urgentCount = actionableThreads.Count(thread => thread.IsUrgent && !thread.IsReplied);
        var immediateCount = actionableThreads.Count(thread => thread.IsImmediateAction && !thread.IsReplied);

        return new UnifiedMessengerThreadMetrics
        {
            OpenThreadCount = actionableThreads.Count(thread => !thread.IsReplied),
            HangingLeadCount = actionableThreads.Count(thread =>
                !thread.IsReplied && thread.KanbanColumn == UnifiedMessengerKanbanColumn.HangingLeads),
            ImmediateActionCount = immediateCount,
            UrgentCount = urgentCount,
            ImmediateActionQueueCount = Math.Min(immediateCount, ImmediateActionQueueDisplayLimit),
            TotalRevenueAtRisk = actionableThreads
                .Where(thread => thread.IsRevenueLeakageRisk)
                .Sum(thread => thread.EstimatedValue),
            ThreadsForSla = actionableThreads
        };
    }

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    internal static string ResolveLatencyColor(double averageLatencyMinutes) =>
        averageLatencyMinutes switch
        {
            _ when averageLatencyMinutes <= OperationalThresholds.GetBranchLatencyGreenMaxMinutes() => "Green",
            _ when averageLatencyMinutes <= OperationalThresholds.GetBranchLatencyAmberMaxMinutes() => "Amber",
            _ => "Red"
        };

    private static IReadOnlyList<UnifiedMessengerPlatformHealthIndicator> BuildPlatformHealth(
        IReadOnlyList<MessengerInstance> instances)
    {
        var healthMonitor = AdapterHealthMonitor.Instance;
        var connectionService = InstanceConnectionStatusService.Instance;
        var indicators = new List<UnifiedMessengerPlatformHealthIndicator>();

        foreach (var platformId in MonitoredPlatformIds)
        {
            if (!PlatformModuleSettingsHelper.IsPlatformModuleEnabled(platformId))
            {
                continue;
            }

            var platform = PlatformDefinition.FindById(platformId);
            var platformInstances = instances
                .Where(instance =>
                    PlatformDefinition.NormalizePlatformId(instance.Platform)
                        .Equals(platformId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (platformInstances.Count == 0)
            {
                indicators.Add(new UnifiedMessengerPlatformHealthIndicator
                {
                    PlatformId = platformId,
                    DisplayName = platform?.DisplayName ?? platformId,
                    IsSynced = false,
                    StatusText = "Not configured"
                });
                continue;
            }

            var syncedCount = platformInstances.Count(instance =>
            {
                var connection = connectionService.GetStatus(instance.Id);
                var adapter = healthMonitor.GetStatus(instance.Id);
                return connection == InstanceConnectionStatus.Connected &&
                       adapter.State == AdapterHealthState.Ready;
            });

            var allSynced = syncedCount == platformInstances.Count;
            indicators.Add(new UnifiedMessengerPlatformHealthIndicator
            {
                PlatformId = platformId,
                DisplayName = platform?.DisplayName ?? platformId,
                IsSynced = allSynced,
                StatusText = allSynced
                    ? "Synced"
                    : syncedCount == 0
                        ? "Re-authentication required"
                        : $"{syncedCount}/{platformInstances.Count} sessions active"
            });
        }

        return indicators;
    }
}
