using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Canonical branch identity and aggregation for the Operations Command Center.
/// A branch groups multiple professional inboxes (WhatsApp, Meta, Google, etc.).
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

    /// <summary>Tab tag value for the aggregate (all branches) workspace.</summary>
    public const string AllBranchesWorkspaceTag = "";

    public static string? ResolveWorkspaceBranchKeyFromTabTag(object? tabTag)
    {
        if (tabTag is not string branchTag || string.IsNullOrWhiteSpace(branchTag))
        {
            return null;
        }

        return branchTag.Trim();
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

    public static IReadOnlyList<string> CollectBranchKeys(
        IEnumerable<MessengerInstance> instances,
        IEnumerable<ThreadData> threads)
    {
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(threads);

        return instances
            .Select(ResolveBranchKey)
            .Concat(threads.Select(thread => thread.BranchName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
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

        if (openCountsByPlatform.TryGetValue("metabusiness", out var metaCount) && metaCount > 0)
        {
            parts.Add($"Meta {metaCount}");
        }

        if (openCountsByPlatform.TryGetValue("googlebusiness", out var googleCount) && googleCount > 0)
        {
            parts.Add($"Google {googleCount}");
        }

        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "whatsapp",
            "whatsappbusiness",
            "metabusiness",
            "googlebusiness"
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

    public sealed record BranchTabCounts(int OpenCount, int ImmediateCount);

    public static IReadOnlyDictionary<string, BranchTabCounts> ComputeBranchTabCounts(
        IEnumerable<ThreadData> threads)
    {
        ArgumentNullException.ThrowIfNull(threads);

        return threads
            .Where(thread => !string.IsNullOrWhiteSpace(thread.BranchName))
            .GroupBy(thread => thread.BranchName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new BranchTabCounts(
                    OpenCount: group.Count(thread => !thread.IsReplied && !thread.IsSpamOrPromo),
                    ImmediateCount: group.Count(thread => thread.IsImmediateAction && !thread.IsReplied)),
                StringComparer.OrdinalIgnoreCase);
    }

    public static BranchTabCounts SumBranchTabCounts(IReadOnlyDictionary<string, BranchTabCounts> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        if (counts.Count == 0)
        {
            return new BranchTabCounts(0, 0);
        }

        return new BranchTabCounts(
            counts.Values.Sum(count => count.OpenCount),
            counts.Values.Sum(count => count.ImmediateCount));
    }

    public static string FormatBranchPillLabel(string branchLabel) =>
        string.IsNullOrWhiteSpace(branchLabel) ? string.Empty : branchLabel.Trim();

    public static string FormatBranchPillBadge(BranchTabCounts counts) =>
        counts.OpenCount > 0 ? counts.OpenCount.ToString() : string.Empty;

    public static string FormatBranchPillTooltip(string branchLabel, BranchTabCounts counts) =>
        FormatBranchTabHeader(branchLabel, counts);

    public static string FormatBranchTabHeader(string branchLabel, BranchTabCounts counts)
    {
        if (string.IsNullOrWhiteSpace(branchLabel))
        {
            return string.Empty;
        }

        if (counts.OpenCount == 0 && counts.ImmediateCount == 0)
        {
            return branchLabel.Trim();
        }

        var parts = new List<string>
        {
            counts.OpenCount == 1 ? "1 open" : $"{counts.OpenCount} open"
        };

        if (counts.ImmediateCount > 0)
        {
            parts.Add(counts.ImmediateCount == 1 ? "1 urgent" : $"{counts.ImmediateCount} urgent");
        }

        return $"{branchLabel.Trim()} ({string.Join(" · ", parts)})";
    }

    internal static UnifiedMessengerBranchMetrics BuildBranchMetrics(
        string branchName,
        IReadOnlyList<ThreadData> threads,
        IReadOnlyList<MessengerInstance> instances)
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
