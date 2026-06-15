using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public ObservableCollection<SettingsSectionNavItemViewModel> SectionNavItems { get; } = [];

    public ObservableCollection<ArchivedAccountRowViewModel> ArchivedAccounts { get; } = [];

    [ObservableProperty]
    private string _selectedSectionKey = SettingsNavigationHelper.NotificationsSectionKey;

    [ObservableProperty]
    private bool _showImportExportPanel;

    [ObservableProperty]
    private bool _showNoArchivedAccounts = true;

    [ObservableProperty]
    private string _versionLabel = string.Empty;

    [ObservableProperty]
    private string _instancesStorePath = string.Empty;

    [ObservableProperty]
    private string _profilesPath = string.Empty;
}
