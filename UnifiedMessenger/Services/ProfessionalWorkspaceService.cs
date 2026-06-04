using System.Collections.Concurrent;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class MetaResponseEfficiencySnapshot
{
    public string AverageResponseDisplay { get; init; } = "—";

    public string EfficiencyRating { get; init; } = "Awaiting data";

    public int SampleCount { get; init; }

    public string LastInboundDisplay { get; init; } = "—";

    public string LastReplyDisplay { get; init; } = "—";

    public int ActiveUnreadCount { get; init; }
}

public sealed class CustomerTrustSnapshot
{
    public int TotalUnrepliedReviews { get; init; }

    public string AggregateRatingDisplay { get; init; } = "—";

    public IReadOnlyList<GoogleReviewAlert> PendingReviews { get; init; } = [];
}

public sealed class ProfessionalWorkspaceService
{
    internal const int MaxStoredReviews = 40;

    private static readonly Lazy<ProfessionalWorkspaceService> LazyInstance =
        new(() => new ProfessionalWorkspaceService());

    private readonly ConcurrentDictionary<string, GoogleReviewAlert> _reviewAlerts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _unrepliedByInstance = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, MetaInboundState> _metaInbound = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, MetaTelemetryHint> _metaTelemetryHints =
        new(StringComparer.OrdinalIgnoreCase);

    public static ProfessionalWorkspaceService Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    internal static ProfessionalWorkspaceService CreateForTests() => new();

    public void HandleGoogleReviewSnapshot(string instanceId, string instanceDisplayName, int unrepliedCount)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        unrepliedCount = Math.Max(0, unrepliedCount);
        if (_unrepliedByInstance.TryGetValue(instanceId, out var existing) && existing == unrepliedCount)
        {
            return;
        }

        _unrepliedByInstance[instanceId] = unrepliedCount;
        NotifyChanged();
    }

    public void HandleGoogleReviewAlert(
        string instanceId,
        string instanceDisplayName,
        string reviewId,
        string reviewerName,
        string snippet,
        string locationLabel,
        int rating,
        DateTimeOffset detectedAt)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(reviewId))
        {
            return;
        }

        var alert = new GoogleReviewAlert
        {
            Id = $"{instanceId}:{reviewId}",
            InstanceId = instanceId,
            InstanceDisplayName = instanceDisplayName,
            ReviewId = reviewId,
            ReviewerName = reviewerName,
            Snippet = snippet,
            LocationLabel = locationLabel,
            Rating = rating,
            DetectedAt = detectedAt
        };

        alert.Normalize();

        if (_reviewAlerts.TryGetValue(alert.Id, out var existing) &&
            !existing.IsReplied &&
            existing.Rating == alert.Rating &&
            existing.Snippet.Equals(alert.Snippet, StringComparison.Ordinal) &&
            existing.ReviewerName.Equals(alert.ReviewerName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _reviewAlerts[alert.Id] = alert;
        TrimReviewAlerts();
        NotifyChanged();
    }

    public void MarkReviewReplied(string alertId)
    {
        if (string.IsNullOrWhiteSpace(alertId) ||
            !_reviewAlerts.TryGetValue(alertId, out var alert) ||
            alert.IsReplied)
        {
            return;
        }

        alert.IsReplied = true;
        NotifyChanged();
    }

    public void HandleMetaTelemetrySnapshot(
        string instanceId,
        double? averageResponseMinutes,
        int slaBreachHints,
        int unreadCount)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var hint = _metaTelemetryHints.GetOrAdd(instanceId, _ => new MetaTelemetryHint());
        var changed = false;

        if (averageResponseMinutes is > 0 &&
            Math.Abs((hint.AverageResponseMinutes ?? 0) - averageResponseMinutes.Value) > 0.01)
        {
            hint.AverageResponseMinutes = averageResponseMinutes;
            changed = true;
        }

        if (slaBreachHints >= 0 && hint.SlaBreachHints != slaBreachHints)
        {
            hint.SlaBreachHints = slaBreachHints;
            changed = true;
        }

        if (unreadCount >= 0 && hint.UnreadCount != unreadCount)
        {
            hint.UnreadCount = unreadCount;
            changed = true;
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    public void HandleMetaInboundMessage(string instanceId, DateTimeOffset timestampUtc, int unreadCount)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        unreadCount = Math.Max(0, unreadCount);
        var state = _metaInbound.GetOrAdd(instanceId, _ => new MetaInboundState());
        var changed = state.ActiveUnreadCount != unreadCount ||
                      state.LastInboundUtc != timestampUtc;

        state.LastInboundUtc = timestampUtc;
        state.ActiveUnreadCount = unreadCount;

        if (changed)
        {
            NotifyChanged();
        }
    }

    public void HandleMetaReplySent(string instanceId, DateTimeOffset replyUtc)
    {
        if (string.IsNullOrWhiteSpace(instanceId) ||
            !_metaInbound.TryGetValue(instanceId, out var state) ||
            state.LastInboundUtc is not { } inboundAt)
        {
            return;
        }

        var deltaMinutes = (replyUtc - inboundAt).TotalMinutes;
        if (deltaMinutes < 0)
        {
            return;
        }

        state.LastReplyUtc = replyUtc;
        state.TotalResponseMinutes += deltaMinutes;
        state.ResponseSampleCount++;
        state.ActiveUnreadCount = Math.Max(0, state.ActiveUnreadCount - 1);
        NotifyChanged();
    }

    public void RemoveInstance(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var removedReviews = false;
        foreach (var key in _reviewAlerts.Keys.ToList())
        {
            if (_reviewAlerts.TryGetValue(key, out var alert) &&
                alert.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase) &&
                _reviewAlerts.TryRemove(key, out _))
            {
                removedReviews = true;
            }
        }

        var removedUnreplied = _unrepliedByInstance.TryRemove(instanceId, out _);
        var removedMeta = _metaInbound.TryRemove(instanceId, out _);
        var removedHints = _metaTelemetryHints.TryRemove(instanceId, out _);

        if (removedReviews || removedUnreplied || removedMeta || removedHints)
        {
            NotifyChanged();
        }
    }

    public CustomerTrustSnapshot CaptureCustomerTrust(IEnumerable<MessengerInstance> googleInstances)
    {
        var instanceIds = googleInstances
            .Select(i => i.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var pending = _reviewAlerts.Values
            .Where(a => instanceIds.Contains(a.InstanceId) && !a.IsReplied)
            .OrderByDescending(a => a.DetectedAt)
            .Take(12)
            .ToList();

        var totalUnreplied = instanceIds.Sum(id =>
            _unrepliedByInstance.TryGetValue(id, out var count) ? count : 0);

        if (totalUnreplied == 0)
        {
            totalUnreplied = pending.Count;
        }

        var rated = pending.Where(p => p.Rating > 0).ToList();
        var aggregateRating = rated.Count == 0
            ? "—"
            : $"{rated.Average(p => p.Rating):0.#}★";

        return new CustomerTrustSnapshot
        {
            TotalUnrepliedReviews = totalUnreplied,
            AggregateRatingDisplay = aggregateRating,
            PendingReviews = pending
        };
    }

    public MetaResponseEfficiencySnapshot CaptureMetaResponseEfficiency(
        IEnumerable<MessengerInstance> metaInstances)
    {
        var instances = metaInstances.ToList();
        if (instances.Count == 0)
        {
            return new MetaResponseEfficiencySnapshot();
        }

        var totalMinutes = 0.0;
        var sampleCount = 0;
        DateTimeOffset? latestInbound = null;
        DateTimeOffset? latestReply = null;
        var activeUnread = 0;

        foreach (var instance in instances)
        {
            var analyticsStats = MessageAnalyticsService.Instance.GetReplyStats(instance.Id);
            totalMinutes += analyticsStats.TotalReplyMinutes;
            sampleCount += analyticsStats.ReplyCount;

            if (analyticsStats.LastReceivedUtc is { } analyticsInbound &&
                (latestInbound is null || analyticsInbound > latestInbound))
            {
                latestInbound = analyticsInbound;
            }

            if (analyticsStats.LastSentUtc is { } analyticsReply &&
                (latestReply is null || analyticsReply > latestReply))
            {
                latestReply = analyticsReply;
            }

            if (!_metaInbound.TryGetValue(instance.Id, out var state))
            {
                continue;
            }

            activeUnread += state.ActiveUnreadCount;

            if (state.LastInboundUtc is { } inbound &&
                (latestInbound is null || inbound > latestInbound))
            {
                latestInbound = inbound;
            }

            if (state.LastReplyUtc is { } reply &&
                (latestReply is null || reply > latestReply))
            {
                latestReply = reply;
            }
        }

        var averageDisplay = sampleCount > 0
            ? FormatMinutes(totalMinutes / sampleCount)
            : ResolveDomAverageDisplay(instances);

        var efficiencyRating = sampleCount > 0
            ? ClassifyEfficiency(totalMinutes, sampleCount)
            : ClassifyEfficiencyFromDomHint(instances);

        return new MetaResponseEfficiencySnapshot
        {
            AverageResponseDisplay = averageDisplay,
            EfficiencyRating = efficiencyRating,
            SampleCount = sampleCount,
            LastInboundDisplay = FormatRelative(latestInbound),
            LastReplyDisplay = FormatRelative(latestReply),
            ActiveUnreadCount = activeUnread
        };
    }

    internal static string ClassifyEfficiency(double totalMinutes, int sampleCount, int slaThresholdMinutes)
    {
        if (sampleCount == 0)
        {
            return "Awaiting data";
        }

        var average = totalMinutes / sampleCount;

        if (average <= slaThresholdMinutes * 0.5)
        {
            return "Excellent";
        }

        if (average <= slaThresholdMinutes)
        {
            return "Good";
        }

        if (average <= slaThresholdMinutes * 1.5)
        {
            return "Fair";
        }

        return "Needs attention";
    }

    private static string ClassifyEfficiency(double totalMinutes, int sampleCount) =>
        ClassifyEfficiency(totalMinutes, sampleCount, AppSettingsService.Instance.Settings.SlaThresholdMinutes);

    private static string FormatMinutes(double minutes)
    {
        if (minutes < 1)
        {
            return "< 1 min";
        }

        if (minutes < 60)
        {
            return $"{Math.Round(minutes, 0)} min";
        }

        return $"{Math.Round(minutes / 60.0, 1)} hr";
    }

    private static string FormatRelative(DateTimeOffset? timestamp) =>
        timestamp is null ? "—" : RelativeTimeFormatter.Format(timestamp.Value);

    private string ResolveDomAverageDisplay(IReadOnlyList<MessengerInstance> instances)
    {
        double? best = null;
        foreach (var instance in instances)
        {
            if (!_metaTelemetryHints.TryGetValue(instance.Id, out var hint) ||
                hint.AverageResponseMinutes is not { } minutes ||
                minutes <= 0)
            {
                continue;
            }

            best = best is null ? minutes : Math.Min(best.Value, minutes);
        }

        return best is null ? "—" : FormatMinutes(best.Value);
    }

    private string ClassifyEfficiencyFromDomHint(IReadOnlyList<MessengerInstance> instances)
    {
        var domAverage = ResolveDomAverageDisplay(instances);
        if (domAverage == "—")
        {
            return "Awaiting data";
        }

        var threshold = AppSettingsService.Instance.Settings.SlaThresholdMinutes;
        if (_metaTelemetryHints.Values.Any(h => h.SlaBreachHints > 0))
        {
            return "Needs attention";
        }

        return "Good";
    }

    private void TrimReviewAlerts()
    {
        if (_reviewAlerts.Count <= MaxStoredReviews)
        {
            return;
        }

        var removeIds = _reviewAlerts.Values
            .OrderBy(a => a.IsReplied)
            .ThenBy(a => a.DetectedAt)
            .Take(_reviewAlerts.Count - MaxStoredReviews)
            .Select(a => a.Id)
            .ToList();

        foreach (var id in removeIds)
        {
            _reviewAlerts.TryRemove(id, out _);
        }
    }

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private sealed class MetaInboundState
    {
        public DateTimeOffset? LastInboundUtc { get; set; }

        public DateTimeOffset? LastReplyUtc { get; set; }

        public double TotalResponseMinutes { get; set; }

        public int ResponseSampleCount { get; set; }

        public int ActiveUnreadCount { get; set; }
    }

    private sealed class MetaTelemetryHint
    {
        public double? AverageResponseMinutes { get; set; }

        public int SlaBreachHints { get; set; }

        public int UnreadCount { get; set; }
    }
}
