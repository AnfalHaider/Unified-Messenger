using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using UnifiedMessenger.Controls;

namespace UnifiedMessenger.ViewModels;

public partial class NotificationFeedViewModel : ViewModelBase
{
    public ObservableCollection<NotificationFeedAlertRow> AlertRows { get; } = [];

    [ObservableProperty]
    private bool _showAlertList;

    [ObservableProperty]
    private bool _clearAllEnabled;

    [ObservableProperty]
    private bool _markAllReadEnabled;

    [ObservableProperty]
    private int _headerBadgeValue;

    [ObservableProperty]
    private Visibility _headerBadgeVisibility = Visibility.Collapsed;
}
