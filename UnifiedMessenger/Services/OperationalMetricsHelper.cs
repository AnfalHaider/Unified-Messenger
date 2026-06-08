using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class OperationalMetricsHelper
{
    public static int CountActiveSlaBreaches(IEnumerable<ThreadData> threads) =>
        threads.Count(thread => thread.IsSlaBreached);

    public static IReadOnlyList<OperationalHighlightItem> BuildHighlights(
        IReadOnlyList<MessengerInstance> instances,
        IReadOnlyList<ThreadData> threads,
        IReadOnlyList<OperationalHighlightItem> outboundHighlights)
    {
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(threads);
        ArgumentNullException.ThrowIfNull(outboundHighlights);

        var instanceNames = instances.ToDictionary(
            instance => instance.Id,
            instance => instance.DisplayName,
            StringComparer.OrdinalIgnoreCase);

        var merged = new List<OperationalHighlightItem>();

        foreach (var thread in threads
                     .Where(thread => !thread.IsReplied && !thread.IsSpamOrPromo)
                     .OrderByDescending(thread => thread.LatencyMinutes)
                     .ThenByDescending(thread => thread.UrgencyScore)
                     .Take(5))
        {
            var branch = string.IsNullOrWhiteSpace(thread.BranchName)
                ? thread.InstanceDisplayName
                : thread.BranchName;
            var platformLabel = PlatformDefinition.FindById(thread.Platform)?.DisplayName ?? thread.Platform;
            var waitLabel = thread.LatencyMinutes < 1
                ? "< 1 min"
                : $"{Math.Round(thread.LatencyMinutes, 0)} min";

            merged.Add(new OperationalHighlightItem
            {
                Title = thread.CustomerName,
                Subtitle = thread.IsSlaBreached
                    ? $"SLA breached · waiting {waitLabel} · {branch} · {platformLabel}"
                    : $"Waiting {waitLabel} · {branch} · {platformLabel}",
                InstanceDisplayName = instanceNames.TryGetValue(thread.InstanceId, out var name)
                    ? name
                    : thread.InstanceDisplayName,
                InstanceId = thread.InstanceId,
                BranchName = branch,
                ConversationKey = string.IsNullOrWhiteSpace(thread.ConversationKey)
                    ? null
                    : thread.ConversationKey
            });
        }

        merged.AddRange(EnrichOutboundHighlights(outboundHighlights, instances));
        return merged
            .Take(8)
            .ToList();
    }

    private static IEnumerable<OperationalHighlightItem> EnrichOutboundHighlights(
        IEnumerable<OperationalHighlightItem> outboundHighlights,
        IReadOnlyList<MessengerInstance> instances)
    {
        var branchByInstance = instances.ToDictionary(
            instance => instance.Id,
            instance => BranchWorkspaceHelper.ResolveBranchKey(instance),
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in outboundHighlights)
        {
            if (!string.IsNullOrWhiteSpace(item.BranchName) ||
                string.IsNullOrWhiteSpace(item.InstanceId) ||
                !branchByInstance.TryGetValue(item.InstanceId, out var branchName))
            {
                yield return item;
                continue;
            }

            yield return new OperationalHighlightItem
            {
                Title = item.Title,
                Subtitle = item.Subtitle,
                InstanceDisplayName = item.InstanceDisplayName,
                InstanceId = item.InstanceId,
                BranchName = branchName,
                ConversationKey = item.ConversationKey
            };
        }
    }
}
