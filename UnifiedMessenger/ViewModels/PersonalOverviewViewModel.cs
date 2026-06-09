using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.ViewModels;

public partial class PersonalOverviewViewModel : ViewModelBase
{
    public ObservableCollection<PersonalOverviewActivityRowViewModel> ActivityItems { get; } = [];

    public ObservableCollection<PersonalOverviewTileRowViewModel> TileItems { get; } = [];

    public ObservableCollection<PersonalOverviewSearchSuggestionViewModel> SearchSuggestions { get; } = [];

    [ObservableProperty]
    private int _personalAccountCount;

    [ObservableProperty]
    private int _totalUnreadCount;

    [ObservableProperty]
    private long _appWorkingSetMegabytes;

    [ObservableProperty]
    private string _visibleInstanceName = "None";

    [ObservableProperty]
    private string _lastUpdatedText = string.Empty;

    [ObservableProperty]
    private bool _showQuickAction;

    [ObservableProperty]
    private string _quickActionLabel = string.Empty;

    [ObservableProperty]
    private string? _quickActionInstanceId;

    [ObservableProperty]
    private string _activityEmptyTitle = string.Empty;

    [ObservableProperty]
    private string _activityEmptyHint = string.Empty;

    [ObservableProperty]
    private string _activityEmptyIconGlyph = "\uE7F3";

    [ObservableProperty]
    private bool _showActivityList;

    [ObservableProperty]
    private bool _showActivityEmptyState;

    [ObservableProperty]
    private bool _showInstanceTilesEmptyState;

    [ObservableProperty]
    private string _instanceTilesEmptyHint = string.Empty;

    [ObservableProperty]
    private bool _showNoAccountsEmptyState;

    [ObservableProperty]
    private bool _showToolbar;

    [ObservableProperty]
    private bool _showContent;

    public void ApplyViewState(PersonalOverviewViewState viewState)
    {
        ArgumentNullException.ThrowIfNull(viewState);

        PersonalAccountCount = viewState.PersonalAccountCount;
        TotalUnreadCount = viewState.TotalUnreadCount;
        AppWorkingSetMegabytes = viewState.AppWorkingSetMegabytes;
        VisibleInstanceName = viewState.VisibleInstanceName;
        LastUpdatedText = viewState.LastUpdatedText;
        ShowQuickAction = viewState.QuickAction.IsVisible;
        QuickActionLabel = viewState.QuickAction.Label;
        QuickActionInstanceId = viewState.QuickAction.InstanceId;
        ActivityEmptyTitle = viewState.ActivityEmptyState.Title;
        ActivityEmptyHint = viewState.ActivityEmptyState.Hint;
        ActivityEmptyIconGlyph = viewState.ActivityEmptyState.IconGlyph;
        ShowActivityList = viewState.ShowActivityList;
        ShowActivityEmptyState = !viewState.ShowActivityList;
        ShowInstanceTilesEmptyState = viewState.ShowInstanceTilesEmptyState;
        InstanceTilesEmptyHint = viewState.InstanceTilesEmptyHint;
        ShowNoAccountsEmptyState = viewState.ShowNoAccountsEmptyState;
        ShowToolbar = !viewState.ShowNoAccountsEmptyState;
        ShowContent = !viewState.ShowNoAccountsEmptyState;

        ReplaceCollection(
            ActivityItems,
            viewState.FilteredActivity.Select(item => new PersonalOverviewActivityRowViewModel
            {
                Alert = item.Alert,
                Title = item.Title,
                Body = item.Body,
                InstanceDisplayName = item.InstanceDisplayName,
                RelativeTimeText = item.RelativeTimeText,
                IconGlyph = item.IconGlyph,
                AccentColorHex = item.AccentColorHex,
                IsUnread = item.IsUnread
            }));

        ReplaceCollection(
            TileItems,
            viewState.InstanceTiles.Select(tile => new PersonalOverviewTileRowViewModel
            {
                InstanceId = tile.InstanceId,
                DisplayName = tile.DisplayName,
                PlatformLabel = tile.PlatformLabel,
                DetailLine = tile.DetailLine,
                ConnectionStatusLabel = tile.ConnectionStatusLabel,
                ConnectionColorHex = tile.ConnectionColorHex,
                IconGlyph = tile.IconGlyph,
                AccentColorHex = tile.AccentColorHex,
                IsMuted = tile.IsMuted,
                UnreadCount = tile.UnreadCount
            }));
    }

    public void ApplySearchSuggestions(IEnumerable<PersonalOverviewSearchSuggestionViewModel> suggestions) =>
        ReplaceCollection(SearchSuggestions, suggestions);

    private static void ReplaceCollection<T>(
        ObservableCollection<T> target,
        IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
