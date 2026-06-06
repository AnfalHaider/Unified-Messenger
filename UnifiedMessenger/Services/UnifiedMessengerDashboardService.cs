using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Adapters;

namespace UnifiedMessenger.Services;

public sealed class UnifiedMessengerDashboardService
{
    private static readonly Lazy<UnifiedMessengerDashboardService> LazyInstance =
        new(() => new UnifiedMessengerDashboardService());

    private static readonly string[] MonitoredPlatformIds = ["metabusiness", "googlebusiness", "whatsapp", "whatsappbusiness"];

    public static UnifiedMessengerDashboardService Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    public UnifiedMessengerDashboardSnapshot BuildSnapshot(
        IEnumerable<MessengerInstance> professionalInstances,
        string? branchInstanceId = null)
    {
        ThreadRegistryService.Instance.RefreshOperationalFlags();

        var instances = professionalInstances
            .Where(instance => instance.IsProfessional && !string.IsNullOrWhiteSpace(instance.Id))
            .ToList();

        var allowedIds = DashboardPageHelper.FilterProfessionalInstances(instances, branchInstanceId)
            .Select(instance => instance.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var threads = ThreadRegistryService.Instance.GetAllThreads()
            .Where(thread => allowedIds.Contains(thread.InstanceId))
            .OrderByDescending(thread => thread.LastMessageTime)
            .ToList();

        var branchNames = threads
            .Select(thread => thread.BranchName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (branchNames.Count == 0)
        {
            branchNames = instances
                .Select(BranchNameResolver.Resolve)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var branchMetrics = branchNames
            .Select(branch => BuildBranchMetrics(branch, threads))
            .ToList();

        var actionableThreads = threads.Where(thread => !thread.IsSpamOrPromo).ToList();

        var revenueAtRisk = actionableThreads
            .Where(thread => thread.IsRevenueLeakageRisk)
            .Sum(thread => thread.EstimatedValue);

        var immediateQueue = actionableThreads
            .Where(thread => thread.IsImmediateAction && !thread.IsReplied)
            .OrderByDescending(thread => thread.UrgencyScore)
            .ThenByDescending(thread => thread.LatencyMinutes)
            .Take(24)
            .ToList();

        var openThreads = actionableThreads.Count(thread => !thread.IsReplied);
        var hangingLeads = actionableThreads.Count(thread =>
            !thread.IsReplied && thread.KanbanColumn == UnifiedMessengerKanbanColumn.HangingLeads);

        return new UnifiedMessengerDashboardSnapshot
        {
            BranchMetrics = branchMetrics,
            TotalRevenueAtRisk = revenueAtRisk,
            PlatformHealth = BuildPlatformHealth(instances),
            ImmediateActionQueue = immediateQueue,
            AllThreads = threads,
            BranchNames = branchNames,
            OpenThreadCount = openThreads,
            HangingLeadCount = hangingLeads,
            ImmediateActionCount = immediateQueue.Count
        };
    }

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    internal static UnifiedMessengerBranchMetrics BuildBranchMetrics(string branchName, IReadOnlyList<ThreadData> threads)
    {
        var branchThreads = threads
            .Where(thread => thread.BranchName.Equals(branchName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var unresolved = branchThreads
            .Where(thread => !thread.IsReplied && !thread.IsSpamOrPromo)
            .ToList();
        var averageLatency = unresolved.Count == 0
            ? 0
            : unresolved.Average(thread => thread.LatencyMinutes);

        return new UnifiedMessengerBranchMetrics
        {
            BranchName = branchName,
            AverageLatencyMinutes = averageLatency,
            UnresolvedCount = unresolved.Count,
            RevenueAtRisk = unresolved.Where(thread => thread.IsRevenueLeakageRisk).Sum(thread => thread.EstimatedValue),
            LatencyColor = ResolveLatencyColor(averageLatency)
        };
    }

    internal static string ResolveLatencyColor(double averageLatencyMinutes) =>
        averageLatencyMinutes switch
        {
            <= 15 => "Green",
            <= 30 => "Amber",
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
