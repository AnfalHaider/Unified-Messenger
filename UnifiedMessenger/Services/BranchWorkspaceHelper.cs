using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Canonical branch identity and aggregation for the Operations Command Center.
/// A branch groups multiple professional WhatsApp inboxes under one workspace key.
/// </summary>
public static class BranchWorkspaceHelper
{
    public static string ResolveBranchKey(MessengerInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return ResolveBranchKey(instance.BranchKey, instance.DisplayName);
    }

    public static string ResolveBranchKey(string? branchKey, string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(branchKey))
        {
            return branchKey.Trim();
        }

        return BranchNameResolver.Resolve(displayName);
    }

    public static IEnumerable<MessengerInstance> FilterByBranchKey(
        IEnumerable<MessengerInstance> instances,
        string? selectedBranchKey)
    {
        ArgumentNullException.ThrowIfNull(instances);

        if (string.IsNullOrWhiteSpace(selectedBranchKey))
        {
            return instances;
        }

        var key = selectedBranchKey.Trim();
        return instances.Where(instance =>
            ResolveBranchKey(instance).Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Canonical branch for a thread: triage-assigned <see cref="ThreadData.BranchName"/> when set,
    /// otherwise the hosting instance branch.
    /// </summary>
    public static string ResolveEffectiveBranchKey(ThreadData thread, MessengerInstance instance)
    {
        ArgumentNullException.ThrowIfNull(thread);
        ArgumentNullException.ThrowIfNull(instance);

        if (!string.IsNullOrWhiteSpace(thread.BranchName))
        {
            return thread.BranchName.Trim();
        }

        return ResolveBranchKey(instance);
    }

    /// <summary>
    /// Whether a thread belongs to the selected branch workspace (OCC kanban, pulse, metrics).
    /// </summary>
    public static bool ThreadBelongsToBranchWorkspace(
        ThreadData thread,
        MessengerInstance instance,
        string? selectedBranchKey)
    {
        ArgumentNullException.ThrowIfNull(thread);
        ArgumentNullException.ThrowIfNull(instance);

        if (string.IsNullOrWhiteSpace(selectedBranchKey))
        {
            return true;
        }

        return ResolveEffectiveBranchKey(thread, instance)
            .Equals(selectedBranchKey.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<ThreadData> FilterThreadsForBranchWorkspace(
        IEnumerable<ThreadData> threads,
        IReadOnlyDictionary<string, MessengerInstance> instancesById,
        string? selectedBranchKey)
    {
        ArgumentNullException.ThrowIfNull(threads);
        ArgumentNullException.ThrowIfNull(instancesById);

        if (string.IsNullOrWhiteSpace(selectedBranchKey))
        {
            return threads.Where(thread =>
                instancesById.ContainsKey(thread.InstanceId));
        }

        return threads.Where(thread =>
        {
            if (!instancesById.TryGetValue(thread.InstanceId, out var instance))
            {
                return false;
            }

            return ThreadBelongsToBranchWorkspace(thread, instance, selectedBranchKey);
        });
    }

    public static IReadOnlyList<string> CollectBranchKeys(
        IEnumerable<MessengerInstance> instances,
        IEnumerable<ThreadData> threads)
    {
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(threads);

        var instanceList = instances.ToList();
        var instanceById = instanceList.ToDictionary(
            instance => instance.Id,
            StringComparer.OrdinalIgnoreCase);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var instance in instanceList)
        {
            keys.Add(ResolveBranchKey(instance));
        }

        foreach (var thread in threads)
        {
            if (!instanceById.TryGetValue(thread.InstanceId, out var instance))
            {
                continue;
            }

            keys.Add(ResolveEffectiveBranchKey(thread, instance));
        }

        return keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? NormalizeBranchKey(string? selectedBranchKey)
    {
        if (string.IsNullOrWhiteSpace(selectedBranchKey))
        {
            return null;
        }

        return selectedBranchKey.Trim();
    }

    public static string BuildScopeLabel(
        IReadOnlyList<MessengerInstance> professionalInstances,
        string? selectedBranchKey)
    {
        var instances = professionalInstances
            .Where(instance => instance.IsProfessional && !string.IsNullOrWhiteSpace(instance.Id))
            .ToList();

        if (instances.Count == 0)
        {
            return "Showing: no professional accounts";
        }

        var branchKeys = CollectBranchKeys(instances, []);
        if (string.IsNullOrWhiteSpace(selectedBranchKey))
        {
            return branchKeys.Count == 0
                ? $"Showing: All Branches ({instances.Count} inbox{(instances.Count == 1 ? "" : "es")})"
                : $"Showing: All Branches ({instances.Count} inbox{(instances.Count == 1 ? "" : "es")} · {branchKeys.Count} branch location{(branchKeys.Count == 1 ? "" : "s")})";
        }

        var branchInstances = FilterByBranchKey(instances, selectedBranchKey).ToList();
        var inboxCount = branchInstances.Count;
        return inboxCount == 0
            ? $"Showing: {selectedBranchKey.Trim()}"
            : $"Showing: {selectedBranchKey.Trim()} ({inboxCount} inbox{(inboxCount == 1 ? "" : "es")})";
    }

    public static string FormatPlatformBreakdown(IReadOnlyDictionary<string, int> openCountsByPlatform)
    {
        if (openCountsByPlatform.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        var whatsAppCount = openCountsByPlatform.GetValueOrDefault("whatsapp") +
                            openCountsByPlatform.GetValueOrDefault("whatsappbusiness");
        if (whatsAppCount > 0)
        {
            parts.Add($"WA {whatsAppCount}");
        }

        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "whatsapp",
            "whatsappbusiness"
        };

        var other = openCountsByPlatform
            .Where(pair => !known.Contains(pair.Key))
            .Sum(pair => pair.Value);
        if (other > 0)
        {
            parts.Add($"Other {other}");
        }

        return parts.Count == 0 ? string.Empty : string.Join(" · ", parts);
    }

    public static IReadOnlyDictionary<string, int> CountOpenThreadsByPlatform(IEnumerable<ThreadData> threads) =>
        threads
            .Where(thread => !thread.IsReplied && !thread.IsSpamOrPromo)
            .GroupBy(thread => PlatformDefinition.NormalizePlatformId(thread.Platform), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

    internal static UnifiedMessengerBranchMetrics BuildBranchMetrics(
        string branchName,
        IReadOnlyList<ThreadData> threads,
        IReadOnlyList<MessengerInstance> instances)
    {
        var instanceById = instances.ToDictionary(
            instance => instance.Id,
            StringComparer.OrdinalIgnoreCase);

        var branchThreads = FilterThreadsForBranchWorkspace(threads, instanceById, branchName).ToList();

        var unresolved = branchThreads
            .Where(thread => !thread.IsReplied && !thread.IsSpamOrPromo)
            .ToList();

        var averageLatency = unresolved.Count == 0
            ? 0
            : unresolved.Average(thread => thread.LatencyMinutes);

        var inboxCount = instances.Count(instance =>
            ResolveBranchKey(instance).Equals(branchName, StringComparison.OrdinalIgnoreCase));

        var openByPlatform = CountOpenThreadsByPlatform(branchThreads);

        return new UnifiedMessengerBranchMetrics
        {
            BranchName = branchName,
            AverageLatencyMinutes = averageLatency,
            UnresolvedCount = unresolved.Count,
            RevenueAtRisk = unresolved.Where(thread => thread.IsRevenueLeakageRisk).Sum(thread => thread.EstimatedValue),
            LatencyColor = UnifiedMessengerDashboardService.ResolveLatencyColor(averageLatency),
            InboxCount = inboxCount,
            SlaBreachCount = unresolved.Count(thread => thread.IsSlaBreached),
            PlatformBreakdown = FormatPlatformBreakdown(openByPlatform)
        };
    }
}
