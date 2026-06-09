using CommunityToolkit.Mvvm.ComponentModel;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.ViewModels;

public partial class PersonalOverviewActivityRowViewModel : ViewModelBase
{
    public required NotificationAlert Alert { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public required string InstanceDisplayName { get; init; }

    public required string RelativeTimeText { get; init; }

    public required string IconGlyph { get; init; }

    public required string AccentColorHex { get; init; }

    [ObservableProperty]
    private bool _isUnread;
}
