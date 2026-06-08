using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Services;

public sealed class UnifiedMessengerDashboardService
{
    private static readonly Lazy<UnifiedMessengerDashboardService> LazyInstance =
        new(() => new UnifiedMessengerDashboardService());

    private static readonly string[] MonitoredPlatformIds = ["metabusiness", "googlebusiness", "whatsapp", "whatsappbusiness"];

    public const int ImmediateActionQueueDisplayLimit = 24;

    public static UnifiedMessengerDashboardService Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    public UnifiedMessengerDashboardSnapshot BuildSnapshot(
        IEnumerable<MessengerInstance> professionalInstances,
        string? selectedBranchKey = null)
    {
        ThreadRegistryService.Instance.RefreshOperationalFlags();

        var instances = professionalInstances
            .Where(instance => instance.IsProfessional && !string.IsNullOrWhiteSpace(instance.Id))
            .ToList();

        var scopedInstances = DashboardPageHelper
            .FilterProfessionalInstances(instances, selectedBranchKey)
            .ToList();

        var allowedIds = scopedInstances
            .Select(instance => instance.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var threads = ThreadRegistryService.Instance.GetAllThreads()
            .Where(thread => allowedIds.Contains(thread.InstanceId))
            .OrderByDescending(thread => thread.LastMessageTime)
            .ToList();

        var branchNames = BranchWorkspaceHelper.CollectBranchKeys(scopedInstances, threads);

        var branchMetrics = branchNames
            .Select(branch => BranchWorkspaceHelper.BuildBranchMetrics(branch, threads, scopedInstances))
            .ToList();

        var actionableThreads = threads.Where(thread => !thread.IsSpamOrPromo).ToList();

        var revenueAtRisk = actionableThreads
            .Where(thread => thread.IsRevenueLeakageRisk)
            .Sum(thread => thread.EstimatedValue);

        var immediateQueue = actionableThreads
            .Where(thread => thread.IsImmediateAction && !thread.IsReplied)
            .OrderByDescending(thread => thread.UrgencyScore)
            .ThenByDescending(thread => thread.LatencyMinutes)
            .Take(ImmediateActionQueueDisplayLimit)
            .ToList();

        var openThreads = actionableThreads.Count(thread => !thread.IsReplied);
        var hangingLeads = actionableThreads.Count(thread =>
            !thread.IsReplied && thread.KanbanColumn == UnifiedMessengerKanbanColumn.HangingLeads);

        return new UnifiedMessengerDashboardSnapshot
        {
            BranchMetrics = branchMetrics,
            TotalRevenueAtRisk = revenueAtRisk,
            PlatformHealth = BuildPlatformHealth(scopedInstances),
            ImmediateActionQueue = immediateQueue,
            AllThreads = threads,
            BranchNames = branchNames,
            OpenThreadCount = openThreads,
            HangingLeadCount = hangingLeads,
            ImmediateActionCount = actionableThreads.Count(thread => thread.IsImmediateAction && !thread.IsReplied),
            ImmediateActionQueueCount = immediateQueue.Count
        };
    }

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    internal static UnifiedMessengerBranchMetrics BuildBranchMetrics(
        string branchName,
        IReadOnlyList<ThreadData> threads,
        IReadOnlyList<MessengerInstance> instances) =>
        BranchWorkspaceHelper.BuildBranchMetrics(branchName, threads, instances);

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
