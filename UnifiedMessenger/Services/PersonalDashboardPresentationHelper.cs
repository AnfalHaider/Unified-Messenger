using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;

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

    /// <summary>App process + all WebView2 child processes — the honest total RAM footprint.</summary>
    public long TotalWorkingSetMegabytes { get; init; }

    /// <summary>Number of live WebView2 processes backing the open sessions.</summary>
    public int WebView2ProcessCount { get; init; }

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

    public bool ShowNoAccountsEmptyState { get; init; }
}

public static class PersonalDashboardPresentationHelper
{
    public static PersonalOverviewViewState BuildViewState(
        PersonalDashboardSnapshot snapshot,
        string? searchQuery = null) =>
        PersonalSnapshotPresenter.BuildViewState(snapshot, searchQuery);

    internal static PersonalOverviewQuickAction BuildQuickAction(PersonalDashboardSnapshot snapshot) =>
        PersonalSnapshotPresenter.BuildQuickAction(snapshot);

    internal static PersonalOverviewEmptyState ResolveActivityEmptyState(
        PersonalDashboardEmptyReason emptyReason,
        bool hasSearchQuery) =>
        PersonalSnapshotPresenter.ResolveActivityEmptyState(emptyReason, hasSearchQuery);

    internal static string ResolveEmptyIconGlyph(PersonalDashboardEmptyReason emptyReason) =>
        PersonalSnapshotPresenter.ResolveEmptyIconGlyph(emptyReason);

    internal static string ResolveInstanceTilesEmptyHint(PersonalDashboardEmptyReason emptyReason) =>
        PersonalSnapshotPresenter.ResolveInstanceTilesEmptyHint(emptyReason);
}
