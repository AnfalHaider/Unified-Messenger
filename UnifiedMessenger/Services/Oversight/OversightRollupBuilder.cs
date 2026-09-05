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
    /// <summary>
    /// Fallback capabilities when no resolver is supplied: assume the channel can supply everything, which
    /// reproduces the behaviour from before capabilities existed. Only the flags this builder reads matter.
    /// </summary>
    private static readonly PlatformCapabilities FullyMeasurable = new()
    {
        IsMessageChannel = true,
        CanReadUnread = true,
        CanReadPreview = true,
        CanReadTimestamps = true,
        CanReadContactIdentity = true,
        SupportsFrt = true
    };

    public static OversightCommandCenterSnapshot Build(
        IReadOnlyList<ThreadData> threads,
        IReadOnlyList<MessengerInstance> instances,
        OversightGrouping grouping,
        Func<string?, double> slaThresholdMinutes,
        Func<string, bool>? isStale = null,
        Func<string, bool>? readFailed = null,
        Func<string, bool>? isSignedOut = null,
        DateTimeOffset? nowUtc = null,
        Func<string, string>? locationForInstance = null,
        DateTimeOffset? windowStartUtc = null,
        DateTimeOffset? windowEndUtc = null,
        Func<string, (int Active, int CaughtUp)?>? chatSnapshot = null,
        Func<string, PlatformCapabilities>? capabilitiesForInstance = null,
        TimeZoneInfo? zone = null)
    {
        ArgumentNullException.ThrowIfNull(threads);
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(slaThresholdMinutes);

        // Null resolver => every instance is treated as fully measurable, which is exactly the pre-capability
        // behaviour. Callers that mix channels pass a real resolver so a channel that cannot supply reply
        // timing is dropped from the on-time DENOMINATOR rather than scored as a miss.
        var capabilities = capabilitiesForInstance ?? (_ => FullyMeasurable);

        // LOCAL day, not UTC. Every other daily figure in the product keys locally — MessageAnalyticsService
        // buckets on receivedAtUtc.LocalDateTime and prunes with DateTime.Now.Date, and KpiTrendStore keys
        // with LocalDateTime. Keying this one in UTC put every message between local midnight and the UTC
        // offset into the previous day's bucket, so at UTC+5 the card's sparkline disagreed with the
        // Analytics chart for the same account until 05:00 every morning.
        //
        // `zone` defaults to the machine's and exists only so this can be tested against a zone that
        // observes DST — this machine is UTC+5, which never transitions, so a local-only test would pass
        // vacuously on both correct and broken code.
        var today = LocalDayBoundary.LocalDate(nowUtc ?? DateTimeOffset.UtcNow, zone);

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

            var instanceIds = list
                .Select(t => t.InstanceId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var threshold = slaThresholdMinutes(
                grouping == OversightGrouping.ByLocation ? group.Key : FirstBranch(list));

            // On-time % measures responsiveness within the selected date window (default: today),
            // including conversations that arrived before the account was connected today. Open
            // conversations OLDER than the window are carried backlog ("from history") — surfaced
            // separately rather than saturating today's number.
            var measuredReplied = replied.Where(InWindow).ToList();
            var measuredOpen = open.Where(InWindow).ToList();

            // Reply-timing metrics are only defined for channels whose adapter can read message timestamps
            // and direction. A Meta channel, for instance, can report an unread BADGE but must never be
            // asked for per-conversation timing (opening a thread there marks it read and notifies the
            // customer). Such threads are excluded from BOTH sides of the on-time fraction — counting them
            // as breaches would invent failures, and counting them as on-time would invent successes.
            var timingReplied = measuredReplied.Where(t => capabilities(t.InstanceId).SupportsFrt).ToList();
            var timingOpen = measuredOpen.Where(t => capabilities(t.InstanceId).SupportsFrt).ToList();
            var supportsTiming = instanceIds.Count == 0 || instanceIds.Any(id => capabilities(id).SupportsFrt);
            var measuredCount = timingReplied.Count + timingOpen.Count;

            // §8 business-hours SLA breach count — open in-window threads past their reply-latency SLA.
            // Independent of the unread-based caught-up % below, so the plan's "on-time" signal is preserved
            // even when the headline % comes from WhatsApp's unread snapshot. 0 when there's no thread data.
            var slaBreachedCount = timingOpen.Count(t => t.IsSlaBreached);

            var onTimeCount = timingReplied.Count(t => t.ReplyLatencyMinutes <= threshold)
                + timingOpen.Count(t => !t.IsSlaBreached);
            var onTimePercent = measuredCount > 0
                ? MetricMath.HonestPercent(onTimeCount, measuredCount)
                : 100;
            var historicalOpenCount = windowStartUtc is null
                ? 0
                : open.Count(t => t.LastMessageTime < windowStartUtc.Value);
            var awaitingCount = measuredOpen.Count;

            // Prefer WhatsApp's own unread signal when we have it: on-time = caught-up % across this
            // entity's instances, over chats active in the window. Reliable for every chat, no name
            // matching or message history needed. Authoritative when a snapshot exists — so an account
            // with no chats active in the window reads "no activity", not a stale thread-breach number.
            var hasChatData = true;
            if (chatSnapshot is not null)
            {
                var hasSnapshot = false;
                var snapActive = 0;
                var snapCaught = 0;
                foreach (var id in instanceIds)
                {
                    if (chatSnapshot(id) is { } snap)
                    {
                        hasSnapshot = true;
                        snapActive += snap.Active;
                        snapCaught += snap.CaughtUp;
                    }
                }

                hasChatData = hasSnapshot;
                if (hasSnapshot)
                {
                    measuredCount = snapActive;
                    onTimePercent = snapActive > 0 ? MetricMath.HonestPercent(snapCaught, snapActive) : 100;
                    historicalOpenCount = 0;
                    awaitingCount = Math.Max(0, snapActive - snapCaught);
                }
                else
                {
                    // No unread data yet — don't show stale thread numbers the awaiting list can't back up.
                    measuredCount = 0;
                    onTimePercent = 100;
                    historicalOpenCount = 0;
                    awaitingCount = 0;
                }
            }

            var stale = isStale is not null
                && instanceIds.Count > 0
                && instanceIds.All(id => isStale(id));

            // ANY failed member, not ALL — unlike `stale` above. A location whose three branch accounts
            // include one the app cannot read is reporting incomplete numbers, and the owner needs to know
            // that before acting on them. Requiring all three to fail would hide exactly the case that
            // matters: one branch quietly dropping out of the rollup.
            var couldNotRead = readFailed is not null
                && instanceIds.Count > 0
                && instanceIds.Any(id => readFailed(id));

            // ALL members, not ANY — the opposite of `couldNotRead` directly above, and deliberately so.
            // Suppressing every figure is the right response only when there is nothing behind them. A
            // location with one signed-out account among three still has real numbers from the other two,
            // and blanking that card would hide more than it protects; ChannelCoverage names the gap
            // instead.
            var signedOut = isSignedOut is not null
                && instanceIds.Count > 0
                && instanceIds.All(id => isSignedOut(id));

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
                AwaitingCount = awaitingCount,
                HasChatData = hasChatData,
                OnTimePercent = onTimePercent,
                SupportsResponseTiming = supportsTiming,
                UrgentCount = open.Count(t => t.IsUrgent),
                DroppedCount = open.Count(t => t.IsRevenueLeakageRisk),
                SlaBreachedCount = slaBreachedCount,
                IsStale = stale,
                IsSignedOut = signedOut,
                ReadFailed = couldNotRead,
                LastActivityUtc = list.Count > 0 ? list.Max(t => t.LastMessageTime) : null,
                MemberInstanceIds = instanceIds,
                TrendCounts = BuildTrend(list, today, zone)
            });
        }

        AddSnapshotOnlyAccounts(
            entities, instances, grouping, chatSnapshot, capabilities,
            locationForInstance, isStale, isSignedOut, readFailed, nameByInstance);

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
    /// <summary>
    /// Emits entities for accounts that produce chat-snapshot data but no <see cref="ThreadData"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The defect this closes.</b> The grouping above is driven entirely by <c>ThreadRegistryService</c>
    /// rows, which only the WhatsApp ingress pipeline writes. Instagram (A13) reads its own store and
    /// writes <i>only</i> the chat snapshot, so it produced no group, no entity and therefore no card. Its
    /// waiting customers still reached the needs-a-reply queue, which reads the snapshot directly — so the
    /// account was contributing to the queue while being absent from the account list above it. That is
    /// worse than either failure alone, because the two figures cannot be reconciled by eye and the owner
    /// has no way to tell which one is wrong.
    /// </para>
    /// <para>
    /// <b>Why a second pass rather than seeding the groups.</b> Injecting synthetic <c>ThreadData</c> would
    /// put rows into a registry that other surfaces treat as message history, inventing threads that were
    /// never read. This adds an entity and nothing else.
    /// </para>
    /// <para>
    /// Under location grouping the account joins an existing location when one is already present, rather
    /// than creating a duplicate row with the same name — a location must appear once.
    /// </para>
    /// </remarks>
    private static void AddSnapshotOnlyAccounts(
        List<OversightEntityHealth> entities,
        IReadOnlyList<MessengerInstance> instances,
        OversightGrouping grouping,
        Func<string, (int Active, int CaughtUp)?>? chatSnapshot,
        Func<string, PlatformCapabilities> capabilities,
        Func<string, string>? locationForInstance,
        Func<string, bool>? isStale,
        Func<string, bool>? isSignedOut,
        Func<string, bool>? readFailed,
        IReadOnlyDictionary<string, string> nameByInstance)
    {
        if (chatSnapshot is null)
        {
            return;
        }

        var covered = entities
            .SelectMany(e => e.MemberInstanceIds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var instance in instances)
        {
            var id = instance?.Id;
            if (string.IsNullOrWhiteSpace(id) || covered.Contains(id!))
            {
                continue;
            }

            if (chatSnapshot(id!) is not { } snap)
            {
                // No snapshot either. The account genuinely has nothing read, and inventing a zeroed card
                // for it would be the false calm the sign-in gate exists to prevent.
                continue;
            }

            covered.Add(id!);

            var awaiting = Math.Max(0, snap.Active - snap.CaughtUp);
            var key = grouping == OversightGrouping.ByLocation
                ? (locationForInstance?.Invoke(id!) ?? string.Empty)
                : id!;

            if (string.IsNullOrWhiteSpace(key))
            {
                key = id!;
            }

            var existingIndex = grouping == OversightGrouping.ByLocation
                ? entities.FindIndex(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))
                : -1;

            if (existingIndex >= 0)
            {
                var existing = entities[existingIndex];
                var mergedMeasured = existing.MeasuredCount + snap.Active;
                var mergedAwaiting = existing.AwaitingCount + awaiting;
                var mergedCaughtUp = Math.Max(0, mergedMeasured - mergedAwaiting);

                entities[existingIndex] = new OversightEntityHealth
                {
                    Key = existing.Key,
                    DisplayName = existing.DisplayName,
                    Kind = existing.Kind,
                    AccountCount = existing.AccountCount + 1,
                    OpenCount = existing.OpenCount + awaiting,
                    MeasuredCount = mergedMeasured,
                    HistoricalOpenCount = existing.HistoricalOpenCount,
                    AwaitingCount = mergedAwaiting,
                    HasChatData = true,
                    OnTimePercent = mergedMeasured > 0
                        ? MetricMath.HonestPercent(mergedCaughtUp, mergedMeasured)
                        : 100,
                    // The joining account keeps the location honest about timing: a location that mixes a
                    // channel with reply times and one without cannot claim the latter's are measured.
                    SupportsResponseTiming = existing.SupportsResponseTiming && capabilities(id!).SupportsFrt,
                    UrgentCount = existing.UrgentCount,
                    DroppedCount = existing.DroppedCount,
                    SlaBreachedCount = existing.SlaBreachedCount,
                    IsStale = existing.IsStale && (isStale?.Invoke(id!) ?? false),
                    IsSignedOut = existing.IsSignedOut && (isSignedOut?.Invoke(id!) ?? false),
                    ReadFailed = existing.ReadFailed || (readFailed?.Invoke(id!) ?? false),
                    LastActivityUtc = existing.LastActivityUtc,
                    MemberInstanceIds = [.. existing.MemberInstanceIds, id!],
                    TrendCounts = existing.TrendCounts
                };

                continue;
            }

            var displayName = grouping == OversightGrouping.ByLocation
                ? key
                : nameByInstance.TryGetValue(id!, out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : instance!.DisplayName;

            entities.Add(new OversightEntityHealth
            {
                Key = key,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName,
                Kind = grouping == OversightGrouping.ByLocation
                    ? OversightEntityKind.Location
                    : OversightEntityKind.Instance,
                AccountCount = 1,
                OpenCount = awaiting,
                MeasuredCount = snap.Active,
                // Zero, not a thread count: there is no message history behind this account, and a
                // historical figure it cannot substantiate would be the kind of number this whole stream
                // exists to stop printing.
                HistoricalOpenCount = 0,
                AwaitingCount = awaiting,
                HasChatData = true,
                OnTimePercent = snap.Active > 0
                    ? MetricMath.HonestPercent(snap.CaughtUp, snap.Active)
                    : 100,
                SupportsResponseTiming = capabilities(id!).SupportsFrt,
                UrgentCount = 0,
                DroppedCount = 0,
                SlaBreachedCount = 0,
                IsStale = isStale?.Invoke(id!) ?? false,
                IsSignedOut = isSignedOut?.Invoke(id!) ?? false,
                ReadFailed = readFailed?.Invoke(id!) ?? false,
                LastActivityUtc = null,
                MemberInstanceIds = [id!],
                // Empty rather than zeroes. A sparkline of seven zeroes reads as "seven quiet days"; an
                // empty one renders nothing, which is what "no history was read" should look like.
                TrendCounts = []
            });
        }
    }

    private static IReadOnlyList<int> BuildTrend(IReadOnlyList<ThreadData> list, DateTime today, TimeZoneInfo? zone)
    {
        var buckets = new int[TrendDays];
        foreach (var thread in list)
        {
            // Both sides of this subtraction must use the same clock as `today` above, or the fix is
            // only half applied and the boundary moves rather than closing.
            //
            // Both are calendar dates at midnight, so the subtraction counts *days on the calendar*, not
            // elapsed hours. That is what keeps the buckets right across a DST transition: a 23-hour day
            // is still one day ago. Deriving `daysAgo` from a duration instead would round 23 hours to
            // zero days and double-count a bar. Covered by DstMetricBucketingTests.
            var daysAgo = (today - LocalDayBoundary.LocalDate(thread.LastMessageTime, zone)).Days;
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
