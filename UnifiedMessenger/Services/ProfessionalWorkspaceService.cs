using System.Collections.Concurrent;
using System.Text.Json;
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
    private const int MaxStoredReviews = 40;

    private static readonly Lazy<ProfessionalWorkspaceService> LazyInstance =
        new(() => new ProfessionalWorkspaceService());

    private readonly ConcurrentDictionary<string, GoogleReviewAlert> _reviewAlerts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _unrepliedByInstance = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, MetaInboundState> _metaInbound = new(StringComparer.OrdinalIgnoreCase);

    public static ProfessionalWorkspaceService Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    public void HandleGoogleReviewSnapshot(string instanceId, string instanceDisplayName, int unrepliedCount)
    {
        _unrepliedByInstance[instanceId] = Math.Max(0, unrepliedCount);
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

        _reviewAlerts[alert.Id] = alert;
        TrimReviewAlerts();
        NotifyChanged();
    }

    public void MarkReviewReplied(string alertId)
    {
        if (_reviewAlerts.TryGetValue(alertId, out var alert))
        {
            alert.IsReplied = true;
            NotifyChanged();
        }
    }

    public void HandleMetaInboundMessage(string instanceId, DateTimeOffset timestampUtc, int unreadCount)
    {
        var state = _metaInbound.GetOrAdd(instanceId, _ => new MetaInboundState());
        state.LastInboundUtc = timestampUtc;
        state.ActiveUnreadCount = unreadCount;
        NotifyChanged();
    }

    public void HandleMetaReplySent(string instanceId, DateTimeOffset replyUtc)
    {
        if (!_metaInbound.TryGetValue(instanceId, out var state) ||
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

    public CustomerTrustSnapshot CaptureCustomerTrust(IEnumerable<MessengerInstance> googleInstances)
    {
        var instanceIds = googleInstances
            .Select(i => i.Id)
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

        var averageDisplay = sampleCount == 0
            ? "—"
            : FormatMinutes(totalMinutes / sampleCount);

        return new MetaResponseEfficiencySnapshot
        {
            AverageResponseDisplay = averageDisplay,
            EfficiencyRating = ClassifyEfficiency(totalMinutes, sampleCount),
            SampleCount = sampleCount,
            LastInboundDisplay = FormatRelative(latestInbound),
            LastReplyDisplay = FormatRelative(latestReply),
            ActiveUnreadCount = activeUnread
        };
    }

    private static string ClassifyEfficiency(double totalMinutes, int sampleCount)
    {
        if (sampleCount == 0)
        {
            return "Awaiting data";
        }

        var average = totalMinutes / sampleCount;
        var sla = AppSettingsService.Instance.Settings.SlaThresholdMinutes;

        if (average <= sla * 0.5)
        {
            return "Excellent";
        }

        if (average <= sla)
        {
            return "Good";
        }

        if (average <= sla * 1.5)
        {
            return "Fair";
        }

        return "Needs attention";
    }

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

    private static string FormatRelative(DateTimeOffset? timestamp)
    {
        if (timestamp is null)
        {
            return "—";
        }

        var delta = DateTimeOffset.UtcNow - timestamp.Value;
        if (delta.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (delta.TotalHours < 1)
        {
            return $"{(int)delta.TotalMinutes}m ago";
        }

        if (delta.TotalDays < 1)
        {
            return $"{(int)delta.TotalHours}h ago";
        }

        return $"{(int)delta.TotalDays}d ago";
    }

    private void TrimReviewAlerts()
    {
        if (_reviewAlerts.Count <= MaxStoredReviews)
        {
            return;
        }

        var removeIds = _reviewAlerts.Values
            .OrderBy(a => a.DetectedAt)
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
}
