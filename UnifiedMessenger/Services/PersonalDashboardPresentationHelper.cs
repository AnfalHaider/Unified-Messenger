using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class PersonalOverviewActivityItem
{
    public required NotificationAlert Alert { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public required string InstanceDisplayName { get; init; }

    public required string RelativeTimeText { get; init; }

    public required string IconGlyph { get; init; }

    public required string AccentColorHex { get; init; }

    public bool IsUnread { get; init; }

    public bool Matches(string query) =>
        DashboardPageHelper.ActivityMatches(Title, Body, InstanceDisplayName, query);
}

public sealed class PersonalOverviewTileItem
{
    public required string InstanceId { get; init; }

    public required string DisplayName { get; init; }

    public required string PlatformLabel { get; init; }

    public required string DetailLine { get; init; }

    public required string ConnectionStatusLabel { get; init; }

    public required string ConnectionColorHex { get; init; }

    public required string IconGlyph { get; init; }

    public required string AccentColorHex { get; init; }

    public int UnreadCount { get; init; }

    public bool IsMuted { get; init; }
}

public sealed class PersonalOverviewQuickAction
{
    public bool IsVisible { get; init; }

    public string? InstanceId { get; init; }

    public string Label { get; init; } = string.Empty;
}

public sealed class PersonalOverviewEmptyState
{
    public required string Title { get; init; }

    public required string Hint { get; init; }

    public required string IconGlyph { get; init; }
}

public sealed class PersonalOverviewViewState
{
    public int PersonalAccountCount { get; init; }

    public int TotalUnreadCount { get; init; }

    public long AppWorkingSetMegabytes { get; init; }

    public string VisibleInstanceName { get; init; } = "None";

    public string LastUpdatedText { get; init; } = string.Empty;

    public PersonalOverviewQuickAction QuickAction { get; init; } = new();

    public IReadOnlyList<PersonalOverviewActivityItem> FilteredActivity { get; init; } = [];

    public IReadOnlyList<PersonalOverviewTileItem> InstanceTiles { get; init; } = [];

    public PersonalOverviewEmptyState ActivityEmptyState { get; init; } = new()
    {
        Title = "No recent activity",
        Hint = string.Empty,
        IconGlyph = "\uE7F3"
    };

    public bool ShowActivityList { get; init; }

    public bool ShowInstanceTilesEmptyState { get; init; }

    public string InstanceTilesEmptyHint { get; init; } = string.Empty;
}

public static class PersonalDashboardPresentationHelper
{
    public static PersonalOverviewViewState BuildViewState(
        PersonalDashboardSnapshot snapshot,
        string? searchQuery = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var activityItems = snapshot.RecentActivity
            .Select(item => new PersonalOverviewActivityItem
            {
                Alert = item.Alert,
                Title = item.Title,
                Body = item.Body,
                InstanceDisplayName = item.InstanceDisplayName,
                RelativeTimeText = item.RelativeTimeText,
                IconGlyph = item.IconGlyph,
                AccentColorHex = item.AccentColorHex,
                IsUnread = item.IsUnread
            })
            .ToList();

        var hasQuery = !string.IsNullOrWhiteSpace(searchQuery);
        var filteredActivity = hasQuery
            ? activityItems.Where(item => item.Matches(searchQuery!)).ToList()
            : activityItems;

        return new PersonalOverviewViewState
        {
            PersonalAccountCount = snapshot.PersonalAccountCount,
            TotalUnreadCount = snapshot.TotalUnreadCount,
            AppWorkingSetMegabytes = snapshot.AppWorkingSetMegabytes,
            VisibleInstanceName = snapshot.VisibleInstanceName,
            LastUpdatedText = DashboardPageHelper.FormatPersonalLastUpdated(snapshot.CapturedAtUtc),
            QuickAction = BuildQuickAction(snapshot),
            FilteredActivity = filteredActivity,
            InstanceTiles = snapshot.InstanceTiles
                .Select(tile => new PersonalOverviewTileItem
                {
                    InstanceId = tile.InstanceId,
                    DisplayName = tile.DisplayName,
                    PlatformLabel = tile.PlatformLabel,
                    DetailLine = tile.DetailLine,
                    ConnectionStatusLabel = tile.ConnectionStatusLabel,
                    ConnectionColorHex = tile.ConnectionColorHex,
                    IconGlyph = tile.IconGlyph,
                    AccentColorHex = tile.AccentColorHex,
                    UnreadCount = tile.UnreadCount,
                    IsMuted = tile.IsMuted
                })
                .ToList(),
            ActivityEmptyState = ResolveActivityEmptyState(snapshot.EmptyReason, hasQuery),
            ShowActivityList = filteredActivity.Count > 0,
            ShowInstanceTilesEmptyState = snapshot.PersonalAccountCount == 0,
            InstanceTilesEmptyHint = ResolveInstanceTilesEmptyHint(snapshot.EmptyReason)
        };
    }

    internal static PersonalOverviewQuickAction BuildQuickAction(PersonalDashboardSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.MostUnreadInstanceId) || snapshot.MostUnreadCount <= 0)
        {
            return new PersonalOverviewQuickAction();
        }

        var busiestName = snapshot.InstanceTiles
            .FirstOrDefault(tile =>
                tile.InstanceId.Equals(snapshot.MostUnreadInstanceId, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName;

        if (string.IsNullOrWhiteSpace(busiestName))
        {
            return new PersonalOverviewQuickAction();
        }

        return new PersonalOverviewQuickAction
        {
            IsVisible = true,
            InstanceId = snapshot.MostUnreadInstanceId,
            Label = DashboardPageHelper.FormatPersonalQuickActionLabel(busiestName, snapshot.MostUnreadCount)
        };
    }

    internal static PersonalOverviewEmptyState ResolveActivityEmptyState(
        PersonalDashboardEmptyReason emptyReason,
        bool hasSearchQuery)
    {
        if (hasSearchQuery)
        {
            return new PersonalOverviewEmptyState
            {
                Title = "No matches",
                Hint = DashboardPageHelper.ResolvePersonalActivityEmptyMessage(emptyReason, true),
                IconGlyph = "\uE721"
            };
        }

        return new PersonalOverviewEmptyState
        {
            Title = DashboardPageHelper.ResolvePersonalEmptyTitle(emptyReason),
            Hint = DashboardPageHelper.ResolvePersonalEmptyHint(emptyReason),
            IconGlyph = ResolveEmptyIconGlyph(emptyReason)
        };
    }

    internal static string ResolveEmptyIconGlyph(PersonalDashboardEmptyReason emptyReason) =>
        emptyReason switch
        {
            PersonalDashboardEmptyReason.NoPersonalAccounts => "\uE8FA",
            PersonalDashboardEmptyReason.AllAccountsMuted => "\uE74F",
            _ => "\uE7F3"
        };

    internal static string ResolveInstanceTilesEmptyHint(PersonalDashboardEmptyReason emptyReason) =>
        emptyReason switch
        {
            PersonalDashboardEmptyReason.NoPersonalAccounts =>
                "Personal account status will appear here after you add an account.",
            PersonalDashboardEmptyReason.AllAccountsMuted =>
                "Accounts remain listed here once added, even when notifications are muted.",
            _ => "Account connection status and unread counts appear here."
        };
}
