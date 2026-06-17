using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Pure builder for the oversight command center. Given the live threads, the instances, and a
/// grouping mode, it produces per-entity health (account or location) sorted worst-first plus a
/// cross-entity "needs attention" summary. Pure and injectable (SLA threshold + staleness are passed
/// in) so it is fully unit-testable without touching live services or the UI.
/// </summary>
public static class OversightRollupBuilder
{
    public static OversightCommandCenterSnapshot Build(
        IReadOnlyList<ThreadData> threads,
        IReadOnlyList<MessengerInstance> instances,
        OversightGrouping grouping,
        Func<string?, double> slaThresholdMinutes,
        Func<string, bool>? isStale = null,
        DateTimeOffset? nowUtc = null,
        Func<string, string>? locationForInstance = null,
        DateTimeOffset? windowStartUtc = null,
        DateTimeOffset? windowEndUtc = null,
        Func<string, (int Active, int CaughtUp)?>? chatSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(threads);
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(slaThresholdMinutes);

        var today = (nowUtc ?? DateTimeOffset.UtcNow).UtcDateTime.Date;

        var nameByInstance = instances
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().DisplayName, StringComparer.OrdinalIgnoreCase);

        var actionable = threads.Where(t => !t.IsSpamOrPromo).ToList();

        bool InWindow(ThreadData thread) =>
            (windowStartUtc is null || thread.LastMessageTime >= windowStartUtc.Value) &&
            (windowEndUtc is null || thread.LastMessageTime <= windowEndUtc.Value);

        // Group locations PER INSTANCE (each account lands in exactly one location) rather than per
        // thread — a single account's threads can carry inconsistent BranchName values, which would
        // otherwise split one account across buckets and leak raw branch ids as location names.
        Func<ThreadData, string> locationKey = locationForInstance is not null
            ? t => Friendly(locationForInstance(t.InstanceId), t)
            : LocationKey;

        var groups = grouping == OversightGrouping.ByLocation
            ? actionable.GroupBy(locationKey, StringComparer.OrdinalIgnoreCase)
            : actionable.GroupBy(t => t.InstanceId, StringComparer.OrdinalIgnoreCase);

        var entities = new List<OversightEntityHealth>();
        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.Key))
            {
                continue;
            }

            var list = group.ToList();
            var open = list.Where(t => !t.IsReplied).ToList();
            var replied = list.Where(t => t.IsReplied).ToList();

            var threshold = slaThresholdMinutes(
                grouping == OversightGrouping.ByLocation ? group.Key : FirstBranch(list));

            // On-time % measures responsiveness within the selected date window (default: today),
            // including conversations that arrived before the account was connected today. Open
            // conversations OLDER than the window are carried backlog ("from history") — surfaced
            // separately rather than saturating today's number.
            var measuredReplied = replied.Where(InWindow).ToList();
            var measuredOpen = open.Where(InWindow).ToList();
            var measuredCount = measuredReplied.Count + measuredOpen.Count;

            var onTimeCount = measuredReplied.Count(t => t.ReplyLatencyMinutes <= threshold)
                + measuredOpen.Count(t => !t.IsSlaBreached);
            var onTimePercent = measuredCount > 0
                ? (int)Math.Round((double)onTimeCount / measuredCount * 100)
                : 100;
            var historicalOpenCount = windowStartUtc is null
                ? 0
                : open.Count(t => t.LastMessageTime < windowStartUtc.Value);

            var instanceIds = list
                .Select(t => t.InstanceId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Prefer WhatsApp's own unread signal when we have it: on-time = caught-up % across this
            // entity's instances. Reliable for every chat, no name matching or message history needed.
            if (chatSnapshot is not null)
            {
                var snapActive = 0;
                var snapCaught = 0;
                foreach (var id in instanceIds)
                {
                    if (chatSnapshot(id) is { } snap)
                    {
                        snapActive += snap.Active;
                        snapCaught += snap.CaughtUp;
                    }
                }

                if (snapActive > 0)
                {
                    measuredCount = snapActive;
                    onTimePercent = (int)Math.Round((double)snapCaught / snapActive * 100);
                    historicalOpenCount = 0;
                }
            }

            var stale = isStale is not null
                && instanceIds.Count > 0
                && instanceIds.All(id => isStale(id));

            var displayName = grouping == OversightGrouping.ByLocation
                ? group.Key
                : nameByInstance.TryGetValue(group.Key, out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : list[0].InstanceDisplayName;

            entities.Add(new OversightEntityHealth
            {
                Key = group.Key,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? group.Key : displayName,
                Kind = grouping == OversightGrouping.ByLocation ? OversightEntityKind.Location : OversightEntityKind.Instance,
                AccountCount = grouping == OversightGrouping.ByLocation ? Math.Max(1, instanceIds.Count) : 1,
                OpenCount = open.Count,
                MeasuredCount = measuredCount,
                HistoricalOpenCount = historicalOpenCount,
                OnTimePercent = onTimePercent,
                UrgentCount = open.Count(t => t.IsUrgent),
                DroppedCount = open.Count(t => t.IsRevenueLeakageRisk),
                IsStale = stale,
                LastActivityUtc = list.Count > 0 ? list.Max(t => t.LastMessageTime) : null,
                MemberInstanceIds = instanceIds,
                TrendCounts = BuildTrend(list, today)
            });
        }

        var sorted = entities
            .OrderByDescending(e => e.UrgentCount)
            .ThenBy(e => e.OnTimePercent)
            .ThenByDescending(e => e.DroppedCount)
            .ToList();

        var totalUrgent = sorted.Sum(e => e.UrgentCount);
        var totalDropped = sorted.Sum(e => e.DroppedCount);
        var worst = sorted.FirstOrDefault(e => e.UrgentCount > 0 || e.OnTimePercent < 100);

        var summary = totalUrgent > 0 && worst is not null
            ? $"{totalUrgent} customer{(totalUrgent == 1 ? "" : "s")} need a reply now — most urgent at {worst.DisplayName}"
            : totalDropped > 0
                ? $"{totalDropped} customer{(totalDropped == 1 ? "" : "s")} may have been dropped"
                : "All caught up.";

        return new OversightCommandCenterSnapshot
        {
            Entities = sorted,
            TotalUrgent = totalUrgent,
            TotalDropped = totalDropped,
            WorstEntityKey = worst?.Key,
            AttentionSummary = summary
        };
    }

    private const int TrendDays = 7;

    /// <summary>
    /// Bucket actionable threads into the last <see cref="TrendDays"/> days by their last-activity day
    /// (oldest → newest). Threads outside the window are ignored; the result is always 7 values.
    /// </summary>
    private static IReadOnlyList<int> BuildTrend(IReadOnlyList<ThreadData> list, DateTime today)
    {
        var buckets = new int[TrendDays];
        foreach (var thread in list)
        {
            var daysAgo = (today - thread.LastMessageTime.UtcDateTime.Date).Days;
            if (daysAgo is >= 0 and < TrendDays)
            {
                buckets[TrendDays - 1 - daysAgo]++;
            }
        }

        return buckets;
    }

    /// <summary>
    /// Final guard against a raw branch id (GUID) reaching the UI as a location name: if the resolved
    /// key is empty or parses as a GUID, fall back to the account's display name (so a lone unassigned
    /// account becomes its own friendly location instead of "28100b95…").
    /// </summary>
    private static string Friendly(string? key, ThreadData thread)
    {
        if (!string.IsNullOrWhiteSpace(key) && !Guid.TryParse(key, out _))
        {
            return key.Trim();
        }

        return !string.IsNullOrWhiteSpace(thread.InstanceDisplayName)
            ? thread.InstanceDisplayName
            : "Unassigned";
    }

    private static string LocationKey(ThreadData thread) =>
        !string.IsNullOrWhiteSpace(thread.BranchName)
            ? thread.BranchName
            : !string.IsNullOrWhiteSpace(thread.InstanceDisplayName)
                ? thread.InstanceDisplayName
                : thread.InstanceId;

    private static string? FirstBranch(IReadOnlyList<ThreadData> list) =>
        list.Select(t => t.BranchName).FirstOrDefault(b => !string.IsNullOrWhiteSpace(b));
}
