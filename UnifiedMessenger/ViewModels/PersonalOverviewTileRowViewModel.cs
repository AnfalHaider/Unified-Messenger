using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.ViewModels;

public partial class PersonalOverviewTileRowViewModel : ViewModelBase
{
    public required string InstanceId { get; init; }

    public required string DisplayName { get; init; }

    public required string PlatformLabel { get; init; }

    public required string DetailLine { get; init; }

    public required string ConnectionStatusLabel { get; init; }

    public required string ConnectionColorHex { get; init; }

    public required string IconGlyph { get; init; }

    public required string AccentColorHex { get; init; }

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private int _unreadCount;

    public bool ShowUnreadBadge => UnreadCount > 0;

    public string UnreadBadgeText => UnreadCount == 1 ? "1 unread" : $"{UnreadCount} unread";

    public SolidColorBrush ConnectionBrush => PlatformBrandingHelper.GetAccentBrush(ConnectionColorHex);

    public SolidColorBrush AccentBrush => PlatformBrandingHelper.GetAccentBrush(AccentColorHex);

    public Visibility MutedIndicatorVisibility =>
        IsMuted ? Visibility.Visible : Visibility.Collapsed;

    public Visibility UnreadBadgeVisibility =>
        ShowUnreadBadge ? Visibility.Visible : Visibility.Collapsed;

    partial void OnUnreadCountChanged(int value)
    {
        OnPropertyChanged(nameof(ShowUnreadBadge));
        OnPropertyChanged(nameof(UnreadBadgeVisibility));
    }

    partial void OnIsMutedChanged(bool value) =>
        OnPropertyChanged(nameof(MutedIndicatorVisibility));
}
