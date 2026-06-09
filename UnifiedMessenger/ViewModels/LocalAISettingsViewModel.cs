using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UnifiedMessenger.ViewModels;

public partial class LocalAISettingsViewModel : ViewModelBase
{
    public ObservableCollection<LocalAiModelRowViewModel> ModelRows { get; } = [];

    [ObservableProperty]
    private bool _enableLocalAi;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModelManagerVisible))]
    private bool _showModelManager;

    [ObservableProperty]
    private bool _ollamaAutoBootstrap;

    [ObservableProperty]
    private bool _enableAutoDraft;

    [ObservableProperty]
    private bool _autoDraftOnlyWhenVisible;

    [ObservableProperty]
    private string _connectionStatusText = "Checking…";

    [ObservableProperty]
    private string _engineStatusText = "—";

    [ObservableProperty]
    private string _connectionIndicatorColorHex = "#787878";

    [ObservableProperty]
    private string _defaultModelId = string.Empty;

    [ObservableProperty]
    private bool _canRefreshEngine;

    [ObservableProperty]
    private string _breadcrumbText = "Settings › Local AI";

    public bool IsModelManagerVisible => EnableLocalAi && ShowModelManager;

    partial void OnEnableLocalAiChanged(bool value)
    {
        if (!value)
        {
            ShowModelManager = false;
        }

        OnPropertyChanged(nameof(IsModelManagerVisible));
    }

    partial void OnShowModelManagerChanged(bool value) =>
        OnPropertyChanged(nameof(IsModelManagerVisible));
}
