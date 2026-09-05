using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class DashboardPageHelper
{
    public const int ResourceRefreshIntervalSeconds = 30;

    public const int MaxSearchSuggestions = 6;

    public static IEnumerable<MessengerInstance> FilterProfessionalInstances(
        IEnumerable<MessengerInstance> professionalInstances,
        string? selectedBranchKey) =>
        BranchWorkspaceHelper.FilterByBranchKey(professionalInstances, selectedBranchKey);

    public static ProfessionalDashboardTelemetry CaptureProfessionalDashboardTelemetry(
        IEnumerable<MessengerInstance> professionalInstances,
        NotificationHub notificationHub,
        string? branchInstanceId = null) =>
        CaptureProfessionalDashboardTelemetry(
            professionalInstances,
            notificationHub,
            branchInstanceId,
            fromUtc: null,
            toUtc: null);

    public static ProfessionalDashboardTelemetry CaptureProfessionalDashboardTelemetry(
        IEnumerable<MessengerInstance> professionalInstances,
        NotificationHub notificationHub,
        string? branchInstanceId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        ArgumentNullException.ThrowIfNull(professionalInstances);
        ArgumentNullException.ThrowIfNull(notificationHub);

        var snapshot = MessageAnalyticsService.Instance.CaptureProfessionalSnapshot(
            professionalInstances,
            notificationHub,
            branchInstanceId,
            fromUtc,
            toUtc);

        return new ProfessionalDashboardTelemetry
        {
            Snapshot = snapshot,
            Display = BuildProfessionalDisplay(snapshot),
            FilteredInstances = FilterProfessionalInstances(professionalInstances, branchInstanceId).ToList()
        };
    }

    /// <summary>The id of the placeholder account the registry seeds on first run.</summary>
    public const string SeededDefaultInstanceId = "whatsapp-default";

    /// <summary>
    /// True when the only account present is the one the app seeded itself — i.e. the owner has connected
    /// nothing yet.
    /// </summary>
    /// <remarks>
    /// On a clean install the dashboard header read "1 personal account connected." directly above an
    /// empty state reading "No accounts connected yet." Both came from the seeded <c>whatsapp-default</c>
    /// placeholder: it is a real registry entry, so it counted, but the owner had not connected anything
    /// and had not signed into it. Two figures on one screen contradicting, on the very first screen a
    /// stranger sees.
    /// </remarks>
    public static bool HasOnlySeededDefaultAccount(IReadOnlyCollection<MessengerInstance>? instances) =>
        instances is { Count: 1 }
        && string.Equals(
            instances.First().Id,
            SeededDefaultInstanceId,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The line under the greeting. Counts accounts, and — since A12 — stops calling them all connected
    /// when some are not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This read "8 professional accounts connected." on a machine where one of the eight was sitting on a
    /// WhatsApp QR screen. It is the first sentence on the first screen, and it was asserting the one thing
    /// the app had just established was false. The count was never wrong — the verb was.
    /// </para>
    /// <para>
    /// Found by installing the build and looking at it, after the increment that added the sign-in gate had
    /// claimed this line was fixed. It was not: that increment qualified the hero's all-clear and left the
    /// greeting alone. The suite proves functions; the app proves features.
    /// </para>
    /// </remarks>
    public static string BuildWelcomeSubtitle(int professionalCount, int personalCount, int signedOutCount = 0)
    {
        var baseLine = (professionalCount, personalCount) switch
        {
            (0, 0) => "Add an account to start receiving unified notifications.",
            ( > 0, > 0) => $"{professionalCount} professional and {personalCount} personal accounts connected.",
            ( > 0, 0) => $"{professionalCount} professional account{(professionalCount == 1 ? "" : "s")} connected.",
            _ => $"{personalCount} personal account{(personalCount == 1 ? "" : "s")} connected."
        };

        if (signedOutCount <= 0 || professionalCount + personalCount == 0)
        {
            return baseLine;
        }

        // "connected" is replaced rather than appended to. Adding "…, 1 signed out." after a clause that
        // already said all of them were connected leaves the sentence contradicting itself, which is how
        // the seeded-default defect read before it was fixed: two figures on one screen disagreeing.
        var connected = professionalCount + personalCount - signedOutCount;
        var accountWord = connected == 1 ? "account" : "accounts";

        return connected <= 0
            ? $"{signedOutCount} account{(signedOutCount == 1 ? " is" : "s are")} signed out — nothing is being read yet."
            : $"{connected} {accountWord} reading · {signedOutCount} signed out.";
    }

    public static string FormatInboundOnlyResponseRate(int receivedCount, int replyPairCount)
    {
        if (receivedCount <= 0)
        {
            return Placeholder;
        }

        var percent = replyPairCount <= 0
            ? 0
            : (int)Math.Round(replyPairCount * 100.0 / receivedCount, MidpointRounding.AwayFromZero);

        return $"Inbound events: {receivedCount} · Replied: {replyPairCount} ({percent}%)";
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

    private const string Placeholder = "—";

    public static string FormatConnectionPillLabel(InstanceConnectionStatus connectionStatus) =>
        connectionStatus switch
        {
            InstanceConnectionStatus.Connected => "Connected",
            InstanceConnectionStatus.LoggedOut => "Logged out",
            InstanceConnectionStatus.Error => "Error",
            InstanceConnectionStatus.Initializing => "Connecting",
            _ => "Connecting"
        };

    public static string FormatConnectionColorHex(
        InstanceConnectionStatus connectionStatus,
        AdapterHealthState adapterState)
    {
        // The HEX overload, not the brush one. This runs on a thread-pool thread via
        // PersonalDashboardService.BuildSnapshot, and the brush overload reads
        // Application.Current.RequestedTheme — a UI-thread-only WinRT call that terminates the process
        // rather than throwing when made from anywhere else.
        return WorkspaceSidebarHelper.ResolveConnectionIndicatorHex(connectionStatus, adapterState);
    }

    public static string BuildPersonalTileDetailLine(
        InstanceResourceTile tile,
        InstanceConnectionStatus connectionStatus,
        bool notificationsMuted,
        string? connectionDetail = null)
    {
        ArgumentNullException.ThrowIfNull(tile);

        if (notificationsMuted)
        {
            return "Notifications muted";
        }

        var healthLine = AdapterHealthStatus.GetDescription(tile.HealthState);
        var parts = new List<string>();

        if (tile.IsVisible)
        {
            parts.Add("Visible");
        }

        if (tile.UnreadCount > 0)
        {
            parts.Add($"{tile.UnreadCount} unread");
        }

        parts.Add(healthLine);

        if (!string.IsNullOrWhiteSpace(connectionDetail) &&
            connectionStatus is InstanceConnectionStatus.Connected
                or InstanceConnectionStatus.LoggedOut
                or InstanceConnectionStatus.Error)
        {
            // The detail is whatever WebView2 last reported, and for a network failure that is a raw enum
            // name — an owner whose wifi dropped read "HostNameNotResolved" beside their WhatsApp
            // account. Translate the ones that are error codes; pass through the ones that were already
            // written for a human (the describer returns null for those).
            parts.Add(NetworkFailureDescriber.DescribeWebViewStatus(connectionDetail) ?? connectionDetail.Trim());
        }

        return string.Join(" · ", parts);
    }

    public static string FormatPersonalQuickActionLabel(string displayName, int unreadCount) =>
        unreadCount == 1
            ? $"Open {displayName} (1 unread)"
            : $"Open {displayName} ({unreadCount} unread)";

    public static string FormatPersonalLastUpdated(DateTimeOffset capturedAtUtc)
    {
        var elapsed = DateTimeOffset.UtcNow - capturedAtUtc;
        if (elapsed.TotalSeconds < 45)
        {
            return "Updated just now";
        }

        if (elapsed.TotalMinutes < 2)
        {
            return "Updated 1 min ago";
        }

        if (elapsed.TotalHours < 1)
        {
            return $"Updated {(int)elapsed.TotalMinutes} min ago";
        }

        return $"Updated at {capturedAtUtc.ToLocalTime():t}";
    }

    public static string ResolvePersonalEmptyTitle(PersonalDashboardEmptyReason emptyReason) =>
        emptyReason switch
        {
            PersonalDashboardEmptyReason.NoPersonalAccounts => "No personal accounts yet",
            PersonalDashboardEmptyReason.AllAccountsMuted => "Notifications are muted",
            PersonalDashboardEmptyReason.NoRecentActivity => "No recent activity",
            _ => "No activity to show"
        };

    public static string ResolvePersonalEmptyHint(PersonalDashboardEmptyReason emptyReason) =>
        emptyReason switch
        {
            PersonalDashboardEmptyReason.NoPersonalAccounts =>
                "Use Add Instance in the sidebar to connect a WhatsApp or WhatsApp Business account.",
            PersonalDashboardEmptyReason.AllAccountsMuted =>
                "Unmute an account in Settings or the sidebar to see notifications again.",
            PersonalDashboardEmptyReason.NoRecentActivity =>
                "New messages from personal accounts will appear here as they arrive.",
            _ => string.Empty
        };

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

    public static string ResolvePersonalActivityEmptyMessage(
        PersonalDashboardEmptyReason emptyReason,
        bool hasSearchQuery) =>
        hasSearchQuery
            ? ResolveEmptyActivityMessage(true)
            : emptyReason switch
            {
                PersonalDashboardEmptyReason.NoPersonalAccounts =>
                    "Add a personal account to see activity here.",
                PersonalDashboardEmptyReason.AllAccountsMuted =>
                    "Personal notifications are muted for all accounts.",
                PersonalDashboardEmptyReason.NoRecentActivity =>
                    ResolveEmptyActivityMessage(false),
                _ => ResolveEmptyActivityMessage(false)
            };

    public static bool PersonalTileMatches(
        string displayName,
        string platformLabel,
        string detailLine,
        string? query)
    {
        query = CommandPaletteHelper.NormalizeQuery(query);
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return displayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || platformLabel.Contains(query, StringComparison.OrdinalIgnoreCase)
            || detailLine.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<DashboardSearchMatch> FilterPersonalSearchMatches(
        IEnumerable<MessengerInstance> personalInstances,
        string? query,
        IEnumerable<NotificationAlert>? personalAlerts = null,
        int maxResults = MaxSearchSuggestions)
    {
        ArgumentNullException.ThrowIfNull(personalInstances);

        query = CommandPaletteHelper.NormalizeQuery(query);
        if (string.IsNullOrWhiteSpace(query) || maxResults <= 0)
        {
            return [];
        }

        var instanceList = personalInstances
            .Where(instance => !string.IsNullOrWhiteSpace(instance.Id))
            .ToList();

        var instanceLookup = instanceList.ToDictionary(
            instance => instance.Id.Trim(),
            instance => instance,
            StringComparer.OrdinalIgnoreCase);

        var matches = new List<DashboardSearchMatch>();
        var matchedInstanceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var instance in instanceList)
        {
            var platform = PlatformDefinition.FindById(instance.Platform);
            var platformLabel = platform?.DisplayName ?? instance.Platform;
            if (!instance.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !platformLabel.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var instanceId = instance.Id.Trim();
            matches.Add(new DashboardSearchMatch(
                instanceId,
                instance.DisplayName,
                platformLabel,
                instance.AccentColor));
            matchedInstanceIds.Add(instanceId);

            if (matches.Count >= maxResults)
            {
                return matches;
            }
        }

        if (personalAlerts is null)
        {
            return matches;
        }

        foreach (var alert in personalAlerts.OrderByDescending(alert => alert.ReceivedAt))
        {
            if (!instanceLookup.ContainsKey(alert.InstanceId))
            {
                continue;
            }

            if (!ActivityMatches(alert.Title, alert.Body, alert.InstanceDisplayName, query))
            {
                continue;
            }

            if (matchedInstanceIds.Contains(alert.InstanceId)
                && matches.Any(match =>
                    match.InstanceId.Equals(alert.InstanceId, StringComparison.OrdinalIgnoreCase)
                    && match.Label.Equals(alert.InstanceDisplayName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var accentColor = instanceLookup[alert.InstanceId].AccentColor;
            matches.Add(new DashboardSearchMatch(
                alert.InstanceId,
                alert.Title,
                alert.InstanceDisplayName,
                accentColor));
            matchedInstanceIds.Add(alert.InstanceId);

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

public sealed class ProfessionalDashboardTelemetry
{
    public required ProfessionalAnalyticsSnapshot Snapshot { get; init; }

    public required ProfessionalDashboardDisplay Display { get; init; }

    public IReadOnlyList<MessengerInstance> FilteredInstances { get; init; } = [];
}


