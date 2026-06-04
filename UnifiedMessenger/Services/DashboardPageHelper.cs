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
        string? selectedBranchInstanceId)
    {
        ArgumentNullException.ThrowIfNull(professionalInstances);

        var normalizedId = NormalizeBranchInstanceId(selectedBranchInstanceId);
        if (normalizedId is null)
        {
            return professionalInstances;
        }

        return professionalInstances.Where(instance =>
            instance.Id.Equals(normalizedId, StringComparison.OrdinalIgnoreCase));
    }

    public static string? NormalizeBranchInstanceId(string? selectedBranchInstanceId)
    {
        if (string.IsNullOrWhiteSpace(selectedBranchInstanceId))
        {
            return null;
        }

        return selectedBranchInstanceId.Trim();
    }

    public static string? ResolveBranchInstanceId(DashboardBranchFilterEntry? entry)
    {
        if (entry is null || entry.IsAllBranches)
        {
            return null;
        }

        return NormalizeBranchInstanceId(entry.InstanceId);
    }

    public static ObservableCollection<DashboardBranchFilterEntry> BuildBranchFilterCollection(
        IEnumerable<MessengerInstance> professionalInstances)
    {
        var collection = new ObservableCollection<DashboardBranchFilterEntry>
        {
            DashboardBranchFilterEntry.CreateAllBranches()
        };

        foreach (var instance in professionalInstances
                     .Where(instance => instance.IsProfessional && !string.IsNullOrWhiteSpace(instance.Id))
                     .OrderBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            collection.Add(DashboardBranchFilterEntry.FromInstance(instance));
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
        MessageTriageService? triageService = null)
    {
        ArgumentNullException.ThrowIfNull(professionalInstances);

        var allowedIds = FilterProfessionalInstances(professionalInstances, branchInstanceId)
            .Select(instance => instance.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var service = triageService ?? MessageTriageService.Instance;

        return service.GetAllItems()
            .Where(item => allowedIds.Contains(item.InstanceId))
            .Where(HasExecutiveInsightContent)
            .OrderByDescending(item => item.UrgencyScore)
            .ThenByDescending(item => item.TimestampUtc)
            .Take(12)
            .Select(BuildExecutiveInsightCard)
            .ToList();
    }

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

    private static ExecutiveInsightCardDisplay BuildExecutiveInsightCard(MessageTriageItem item)
    {
        var entities = item.ExtractedEntities ?? new RichTriageExtractedEntities();
        var fields = new List<ExecutiveInsightFieldDisplay>();

        AddField(fields, "Customer", "\uE77B", entities.CustomerName);
        AddField(fields, "Contact", "\uE717", entities.ContactNumber);
        AddField(fields, "Service", "\uE14C", entities.ServiceType);
        AddField(fields, "Date", "\uE787", entities.RequestedDate);
        AddField(fields, "Time", "\uE121", entities.RequestedTime);
        AddField(fields, "Action required", "\uE72C", entities.ActionRequired, emphasize: true);

        return new ExecutiveInsightCardDisplay
        {
            CustomerName = string.IsNullOrWhiteSpace(item.CustomerName) ? "Customer" : item.CustomerName.Trim(),
            BranchName = item.InstanceDisplayName,
            CoreSummary = string.IsNullOrWhiteSpace(item.CoreSummary)
                ? item.MessagePreview
                : item.CoreSummary.Trim(),
            IntentLabel = FormatCustomerIntent(item.CustomerIntent),
            UrgencyLabel = item.UrgencyLabel,
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

    public static ProfessionalDashboardDisplay BuildProfessionalDisplay(ProfessionalAnalyticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new ProfessionalDashboardDisplay
        {
            AverageReplyTime = snapshot.HasReplyMetrics
                ? snapshot.AverageReplyTimeDisplay
                : Placeholder,
            SlaBreaches = snapshot.HasMessageVolume
                ? snapshot.SlaBreaches.ToString()
                : Placeholder,
            ResponseRate = snapshot.HasReplyMetrics
                ? snapshot.ResponseRateDisplay
                : Placeholder,
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

        var hasData = snapshot.SampleCount > 0 ||
                        snapshot.ActiveUnreadCount > 0 ||
                        snapshot.LastInboundDisplay != Placeholder ||
                        snapshot.LastReplyDisplay != Placeholder;

        return new MetaResponseDisplay
        {
            AverageResponse = hasData && snapshot.AverageResponseDisplay != Placeholder
                ? snapshot.AverageResponseDisplay
                : Placeholder,
            EfficiencyRating = hasData
                ? snapshot.EfficiencyRating
                : "Awaiting data",
            SampleCount = hasData
                ? snapshot.SampleCount.ToString()
                : Placeholder,
            LastInbound = hasData ? snapshot.LastInboundDisplay : Placeholder,
            LastReply = hasData ? snapshot.LastReplyDisplay : Placeholder,
            HasData = hasData
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

    public required string SlaBreaches { get; init; }

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
}
