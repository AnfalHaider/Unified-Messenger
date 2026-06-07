using System.Collections.ObjectModel;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class DashboardPageHelper
{
    public const int ResourceRefreshIntervalSeconds = 30;

    public const int MaxSearchSuggestions = 6;

    public const string AllBranchesOptionId = "";

    public static IReadOnlyList<DashboardBranchOption> BuildBranchOptions(
        IEnumerable<MessengerInstance> professionalInstances)
    {
        var options = new List<DashboardBranchOption>
        {
            new(AllBranchesOptionId, "All Branches")
        };

        foreach (var instance in professionalInstances
                     .Where(instance => !string.IsNullOrWhiteSpace(instance.Id))
                     .OrderBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            options.Add(new DashboardBranchOption(instance.Id.Trim(), instance.DisplayName.Trim()));
        }

        return options;
    }

    public static IEnumerable<MessengerInstance> FilterProfessionalInstances(
        IEnumerable<MessengerInstance> professionalInstances,
        string? selectedBranchKey) =>
        BranchWorkspaceHelper.FilterByBranchKey(professionalInstances, selectedBranchKey);

    public static string? NormalizeBranchInstanceId(string? selectedBranchKey) =>
        BranchWorkspaceHelper.NormalizeBranchKey(selectedBranchKey);

    public static string? ResolveBranchInstanceId(DashboardBranchFilterEntry? entry) =>
        BranchWorkspaceHelper.ResolveBranchKeyFromEntry(entry);

    public static ObservableCollection<DashboardBranchFilterEntry> BuildBranchFilterCollection(
        IEnumerable<MessengerInstance> professionalInstances)
    {
        var collection = new ObservableCollection<DashboardBranchFilterEntry>();
        foreach (var entry in BranchWorkspaceHelper.BuildBranchFilterEntries(professionalInstances))
        {
            collection.Add(entry);
        }

        return collection;
    }

    public static ProfessionalDashboardTelemetry CaptureProfessionalDashboardTelemetry(
        IEnumerable<MessengerInstance> professionalInstances,
        NotificationHub notificationHub,
        string? branchInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(professionalInstances);
        ArgumentNullException.ThrowIfNull(notificationHub);

        var snapshot = MessageAnalyticsService.Instance.CaptureProfessionalSnapshot(
            professionalInstances,
            notificationHub,
            branchInstanceId);

        return new ProfessionalDashboardTelemetry
        {
            Snapshot = snapshot,
            Display = BuildProfessionalDisplay(snapshot),
            FilteredInstances = FilterProfessionalInstances(professionalInstances, branchInstanceId).ToList()
        };
    }

    public static MessageTriageDashboardSnapshot BuildFilteredTriageSnapshot(
        IEnumerable<MessengerInstance> professionalInstances,
        string? branchInstanceId = null,
        MessageTriageService? triageService = null)
    {
        var service = triageService ?? MessageTriageService.Instance;
        return service.BuildSnapshot(
            FilterProfessionalInstances(professionalInstances, branchInstanceId));
    }

    public static IReadOnlyList<ExecutiveInsightCardDisplay> BuildExecutiveInsights(
        IEnumerable<MessengerInstance> professionalInstances,
        string? branchInstanceId = null,
        MessageTriageService? triageService = null,
        bool? includeHeuristic = null)
    {
        ArgumentNullException.ThrowIfNull(professionalInstances);

        var allowedIds = FilterProfessionalInstances(professionalInstances, branchInstanceId)
            .Select(instance => instance.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var service = triageService ?? MessageTriageService.Instance;
        var showHeuristic = includeHeuristic ??
                            AppSettingsService.Instance.Settings.ShowHeuristicExecutiveInsights;

        var items = service.GetAllItems()
            .Where(item => allowedIds.Contains(item.InstanceId))
            .OrderByDescending(item => item.UrgencyScore)
            .ThenByDescending(item => item.TimestampUtc)
            .ToList();

        var cards = new List<ExecutiveInsightCardDisplay>();
        foreach (var item in items.Where(HasExecutiveInsightContent))
        {
            var sourceLabel = item.InferenceSource == TriageInferenceSource.LocalAi ? "Local AI" : "Rich";
            cards.Add(BuildExecutiveInsightCard(item, sourceLabel));
            if (cards.Count >= 12)
            {
                return cards;
            }
        }

        if (!showHeuristic)
        {
            return cards;
        }

        foreach (var item in items.Where(item => !HasExecutiveInsightContent(item)))
        {
            cards.Add(BuildHeuristicInsightCard(item));
            if (cards.Count >= 12)
            {
                break;
            }
        }

        return cards;
    }

    internal static ExecutiveInsightCardDisplay BuildHeuristicInsightCard(MessageTriageItem item) =>
        BuildExecutiveInsightCard(item, "Heuristic");

    internal static bool HasExecutiveInsightContent(MessageTriageItem item) =>
        item.InferenceSource == TriageInferenceSource.LocalAi ||
        !string.IsNullOrWhiteSpace(item.CoreSummary) ||
        HasExtractedEntityValue(item.ExtractedEntities);

    private static bool HasExtractedEntityValue(RichTriageExtractedEntities entities) =>
        !string.IsNullOrWhiteSpace(entities.CustomerName) ||
        !string.IsNullOrWhiteSpace(entities.ContactNumber) ||
        !string.IsNullOrWhiteSpace(entities.RequestedDate) ||
        !string.IsNullOrWhiteSpace(entities.RequestedTime) ||
        !string.IsNullOrWhiteSpace(entities.ServiceType) ||
        !string.IsNullOrWhiteSpace(entities.ActionRequired);

    internal static ExecutiveInsightCardDisplay BuildExecutiveInsightCard(
        MessageTriageItem item,
        string sourceLabel)
    {
        var entities = item.ExtractedEntities ?? new RichTriageExtractedEntities();
        var fields = new List<ExecutiveInsightFieldDisplay>();

        AddField(fields, "Customer", "\uE77B", entities.CustomerName);
        AddField(fields, "Contact", "\uE717", entities.ContactNumber);
        AddField(fields, "Service", "\uE14C", entities.ServiceType);
        AddField(fields, "Date", "\uE787", entities.RequestedDate);
        AddField(fields, "Time", "\uE121", entities.RequestedTime);
        AddField(fields, "Action required", "\uE72C", entities.ActionRequired, emphasize: true);

        if (fields.Count == 0 && sourceLabel.Equals("Heuristic", StringComparison.OrdinalIgnoreCase))
        {
            AddField(fields, "Sentiment", "\uE8BD", item.Sentiment.ToString());
            AddField(fields, "Urgency", "\uE7BA", item.UrgencyScore.ToString());
        }

        return new ExecutiveInsightCardDisplay
        {
            CustomerName = string.IsNullOrWhiteSpace(item.CustomerName) ? "Customer" : item.CustomerName.Trim(),
            BranchName = string.IsNullOrWhiteSpace(item.BranchName)
                ? BranchNameResolver.Resolve(item.InstanceDisplayName)
                : item.BranchName.Trim(),
            CoreSummary = ResolveInsightSummary(item),
            IntentLabel = FormatIntentLabel(item.AiIntentCategory),
            UrgencyLabel = item.UrgencyLabel,
            SourceLabel = sourceLabel,
            Fields = fields
        };
    }

    private static void AddField(
        ICollection<ExecutiveInsightFieldDisplay> fields,
        string label,
        string iconGlyph,
        string? value,
        bool emphasize = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        fields.Add(new ExecutiveInsightFieldDisplay
        {
            Label = label,
            Value = value.Trim(),
            IconGlyph = iconGlyph,
            Emphasize = emphasize
        });
    }

    private static string ResolveInsightSummary(MessageTriageItem item)
    {
        if (item.IsSpamOrPromo)
        {
            return "Promotional message — no action required";
        }

        if (!string.IsNullOrWhiteSpace(item.NextActionSummary))
        {
            return item.NextActionSummary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(item.CoreSummary))
        {
            return item.CoreSummary.Trim();
        }

        return "Awaiting AI classification";
    }

    private static string FormatIntentLabel(string? intentCategory) =>
        UnifiedMessengerDashboardPresentationHelper.FormatIntentLabel(intentCategory);

    private static string FormatCustomerIntent(CustomerIntent intent) =>
        intent switch
        {
            CustomerIntent.Booking => "Booking",
            CustomerIntent.Complaint => "Complaint",
            CustomerIntent.Spam => "Spam",
            _ => "Inquiry"
        };

    public static string BuildWelcomeSubtitle(int professionalCount, int personalCount) =>
        (professionalCount, personalCount) switch
        {
            (0, 0) => "Add an account to start receiving unified notifications.",
            ( > 0, > 0) => $"{professionalCount} professional and {personalCount} personal accounts connected.",
            ( > 0, 0) => $"{professionalCount} professional account{(professionalCount == 1 ? "" : "s")} connected.",
            _ => $"{personalCount} personal account{(personalCount == 1 ? "" : "s")} connected."
        };

    public static string FormatUnrepliedReviewCount(int count) =>
        count == 1 ? "1 unreplied review" : $"{count} unreplied reviews";

    public static string FormatInboundOnlyResponseRate(int receivedCount, int replyPairCount)
    {
        if (receivedCount <= 0)
        {
            return Placeholder;
        }

        var percent = replyPairCount <= 0
            ? 0
            : (int)Math.Round(replyPairCount * 100.0 / receivedCount, MidpointRounding.AwayFromZero);

        return $"Inbound: {receivedCount} · Replied: {replyPairCount} ({percent}%)";
    }

    public static ProfessionalDashboardDisplay BuildProfessionalDisplay(ProfessionalAnalyticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var slaThreshold = AppSettingsService.Instance.Settings.SlaThresholdMinutes;
        var averageReply = snapshot.HasReplyMetrics
            ? snapshot.AverageReplyTimeDisplay
            : snapshot.ReceivedCount > 0
                ? "No replies logged yet"
                : Placeholder;

        var responseRate = snapshot.HasReplyMetrics
            ? snapshot.ResponseRateDisplay
            : FormatInboundOnlyResponseRate(snapshot.ReceivedCount, snapshot.ReplyPairCount);

        return new ProfessionalDashboardDisplay
        {
            AverageReplyTime = averageReply,
            AverageReplyTimeSubtext = snapshot.HasReplyMetrics
                ? string.Empty
                : snapshot.ReceivedCount > 0
                    ? "Reply in a professional inbox to measure response time"
                    : string.Empty,
            SlaBreaches = snapshot.HasMessageVolume
                ? snapshot.SlaBreaches.ToString()
                : Placeholder,
            SlaThresholdSubtext = snapshot.HasMessageVolume
                ? $"Threshold: {slaThreshold} min"
                : string.Empty,
            ResponseRate = responseRate,
            PeakHour = snapshot.HasMessageVolume
                ? snapshot.PeakHourDisplay
                : Placeholder,
            DailyTrend = snapshot.HasMessageVolume
                ? snapshot.DailyTrendDisplay
                : Placeholder,
            SentCount = snapshot.HasMessageVolume
                ? snapshot.SentCount.ToString()
                : Placeholder,
            ReceivedCount = snapshot.HasMessageVolume
                ? snapshot.ReceivedCount.ToString()
                : Placeholder,
            WeeklyActivity = snapshot.WeeklyActivity,
            Highlights = snapshot.Highlights,
            Triage = snapshot.Triage,
            HasMessageVolume = snapshot.HasMessageVolume,
            HasReplyMetrics = snapshot.HasReplyMetrics
        };
    }

    public static CustomerTrustDisplay BuildCustomerTrustDisplay(CustomerTrustSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var hasData = snapshot.TotalUnrepliedReviews > 0 || snapshot.PendingReviews.Count > 0;
        return new CustomerTrustDisplay
        {
            AggregateRating = hasData && snapshot.AggregateRatingDisplay != Placeholder
                ? snapshot.AggregateRatingDisplay
                : hasData
                    ? snapshot.AggregateRatingDisplay
                    : Placeholder,
            UnrepliedReviews = hasData
                ? FormatUnrepliedReviewCount(snapshot.TotalUnrepliedReviews)
                : Placeholder,
            PendingReviews = snapshot.PendingReviews
        };
    }

    public static MetaResponseDisplay BuildMetaResponseDisplay(MetaResponseEfficiencySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var hasInbound = snapshot.LastInboundDisplay != Placeholder;
        var hasReplySamples = snapshot.SampleCount > 0;
        var hasData = hasReplySamples ||
                        snapshot.ActiveUnreadCount > 0 ||
                        hasInbound ||
                        snapshot.LastReplyDisplay != Placeholder;

        var inboundOnly = hasData && !hasReplySamples && hasInbound;
        var average = snapshot.AverageResponseDisplay != Placeholder
            ? snapshot.AverageResponseDisplay
            : Placeholder;

        return new MetaResponseDisplay
        {
            AverageResponse = average,
            EfficiencyRating = hasData
                ? snapshot.EfficiencyRating
                : "Awaiting data",
            SampleCount = hasData
                ? snapshot.SampleCount.ToString()
                : Placeholder,
            LastInbound = hasData ? snapshot.LastInboundDisplay : Placeholder,
            LastReply = hasData ? snapshot.LastReplyDisplay : Placeholder,
            HasData = hasData,
            InboundOnly = inboundOnly,
            PendingResponseLabel = inboundOnly && snapshot.ActiveUnreadCount > 0
                ? $"{snapshot.ActiveUnreadCount} pending response"
                : string.Empty
        };
    }

    private const string Placeholder = "—";

    public static string BuildInstanceStatusLine(InstanceResourceTile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        var parts = new List<string>();
        if (tile.IsVisible)
        {
            parts.Add("Visible");
        }

        parts.Add(tile.MemoryTier);
        if (tile.UnreadCount > 0)
        {
            parts.Add($"{tile.UnreadCount} unread");
        }

        parts.Add(tile.HealthState.ToString());
        return string.Join(" · ", parts);
    }

    public static bool ActivityMatches(
        string title,
        string body,
        string instanceDisplayName,
        string? query)
    {
        query = CommandPaletteHelper.NormalizeQuery(query);
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || body.Contains(query, StringComparison.OrdinalIgnoreCase)
            || instanceDisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveEmptyActivityMessage(bool hasSearchQuery) =>
        hasSearchQuery
            ? "No personal activity matches your search."
            : "No recent notifications from personal accounts.";

    public static IReadOnlyList<DashboardSearchMatch> FilterPersonalSearchMatches(
        IEnumerable<MessengerInstance> personalInstances,
        string? query,
        int maxResults = MaxSearchSuggestions)
    {
        ArgumentNullException.ThrowIfNull(personalInstances);

        query = CommandPaletteHelper.NormalizeQuery(query);
        if (string.IsNullOrWhiteSpace(query) || maxResults <= 0)
        {
            return [];
        }

        var matches = new List<DashboardSearchMatch>();
        foreach (var instance in personalInstances)
        {
            if (string.IsNullOrWhiteSpace(instance.Id))
            {
                continue;
            }

            var platform = PlatformDefinition.FindById(instance.Platform);
            var platformLabel = platform?.DisplayName ?? instance.Platform;
            if (!instance.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !platformLabel.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches.Add(new DashboardSearchMatch(
                instance.Id.Trim(),
                instance.DisplayName,
                platformLabel,
                instance.AccentColor));

            if (matches.Count >= maxResults)
            {
                break;
            }
        }

        return matches;
    }
}

public readonly record struct DashboardSearchMatch(
    string InstanceId,
    string Label,
    string SubLabel,
    string AccentColorHex);

public readonly record struct DashboardBranchOption(string InstanceId, string DisplayName);

public sealed class ProfessionalDashboardDisplay
{
    public required string AverageReplyTime { get; init; }

    public string AverageReplyTimeSubtext { get; init; } = string.Empty;

    public required string SlaBreaches { get; init; }

    public string SlaThresholdSubtext { get; init; } = string.Empty;

    public required string ResponseRate { get; init; }

    public required string PeakHour { get; init; }

    public required string DailyTrend { get; init; }

    public required string SentCount { get; init; }

    public required string ReceivedCount { get; init; }

    public bool HasMessageVolume { get; init; }

    public bool HasReplyMetrics { get; init; }

    public IReadOnlyList<DailyActivityPoint> WeeklyActivity { get; init; } = [];

    public IReadOnlyList<OperationalHighlightItem> Highlights { get; init; } = [];

    public MessageTriageDashboardSnapshot Triage { get; init; } = MessageTriageDashboardSnapshot.Empty;
}

public sealed class CustomerTrustDisplay
{
    public required string AggregateRating { get; init; }

    public required string UnrepliedReviews { get; init; }

    public IReadOnlyList<GoogleReviewAlert> PendingReviews { get; init; } = [];
}

public sealed class ProfessionalDashboardTelemetry
{
    public required ProfessionalAnalyticsSnapshot Snapshot { get; init; }

    public required ProfessionalDashboardDisplay Display { get; init; }

    public IReadOnlyList<MessengerInstance> FilteredInstances { get; init; } = [];
}

public sealed class ExecutiveInsightFieldDisplay
{
    public required string Label { get; init; }

    public required string Value { get; init; }

    public required string IconGlyph { get; init; }

    public bool Emphasize { get; init; }
}

public sealed class ExecutiveInsightCardDisplay
{
    public required string CustomerName { get; init; }

    public required string BranchName { get; init; }

    public required string CoreSummary { get; init; }

    public required string IntentLabel { get; init; }

    public required string UrgencyLabel { get; init; }

    public string SourceLabel { get; init; } = "Local AI";

    public IReadOnlyList<ExecutiveInsightFieldDisplay> Fields { get; init; } = [];
}

public sealed class MetaResponseDisplay
{
    public required string AverageResponse { get; init; }

    public required string EfficiencyRating { get; init; }

    public required string SampleCount { get; init; }

    public required string LastInbound { get; init; }

    public required string LastReply { get; init; }

    public bool HasData { get; init; }

    public bool InboundOnly { get; init; }

    public string PendingResponseLabel { get; init; } = string.Empty;
}
