using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Presenters;

public static class PersonalSnapshotPresenter
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

        var instanceTiles = snapshot.InstanceTiles
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
            .Where(tile => !hasQuery ||
                           DashboardPageHelper.PersonalTileMatches(
                               tile.DisplayName,
                               tile.PlatformLabel,
                               tile.DetailLine,
                               searchQuery))
            .ToList();

        return new PersonalOverviewViewState
        {
            PersonalAccountCount = snapshot.PersonalAccountCount,
            TotalUnreadCount = snapshot.TotalUnreadCount,
            AppWorkingSetMegabytes = snapshot.AppWorkingSetMegabytes,
            VisibleInstanceName = snapshot.VisibleInstanceName,
            LastUpdatedText = DashboardPageHelper.FormatPersonalLastUpdated(snapshot.CapturedAtUtc),
            QuickAction = BuildQuickAction(snapshot),
            FilteredActivity = filteredActivity,
            InstanceTiles = instanceTiles,
            ActivityEmptyState = ResolveActivityEmptyState(snapshot.EmptyReason, hasQuery),
            ShowActivityList = filteredActivity.Count > 0,
            ShowInstanceTilesEmptyState = snapshot.PersonalAccountCount == 0 ||
                                          (hasQuery && instanceTiles.Count == 0),
            InstanceTilesEmptyHint = hasQuery && snapshot.PersonalAccountCount > 0
                ? "No personal accounts match your search."
                : ResolveInstanceTilesEmptyHint(snapshot.EmptyReason),
            ShowNoAccountsEmptyState = snapshot.PersonalAccountCount == 0
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
                "Connect a personal messenger with Add Instance in the sidebar.",
            PersonalDashboardEmptyReason.AllAccountsMuted =>
                "Accounts remain listed here once added, even when notifications are muted.",
            _ => "Account connection status and unread counts appear here."
        };
}
