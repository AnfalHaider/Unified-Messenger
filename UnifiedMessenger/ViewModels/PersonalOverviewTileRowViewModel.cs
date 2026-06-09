using CommunityToolkit.Mvvm.ComponentModel;

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

    partial void OnUnreadCountChanged(int value) =>
        OnPropertyChanged(nameof(ShowUnreadBadge));
}
