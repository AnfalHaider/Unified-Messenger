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
            var waitLabel = thread.LatencyMinutes < 1
                ? "< 1 min"
                : $"{Math.Round(thread.LatencyMinutes, 0)} min";

            merged.Add(new OperationalHighlightItem
            {
                Title = thread.CustomerName,
                Subtitle = thread.IsSlaBreached
                    ? $"SLA breached · waiting {waitLabel} · {branch}"
                    : $"Waiting {waitLabel} · {branch}",
                InstanceDisplayName = instanceNames.TryGetValue(thread.InstanceId, out var name)
                    ? name
                    : thread.InstanceDisplayName,
                InstanceId = thread.InstanceId
            });
        }

        merged.AddRange(outboundHighlights);
        return merged
            .Take(8)
            .ToList();
    }
}
