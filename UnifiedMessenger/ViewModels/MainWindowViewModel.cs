using CommunityToolkit.Mvvm.ComponentModel;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isDashboardSelected = true;

    [ObservableProperty]
    private bool _isSettingsSelected;

    [ObservableProperty]
    private bool _isWorkQueueSelected;

    [ObservableProperty]
    private string? _selectedInstanceId;

    [ObservableProperty]
    private bool _notificationPanelVisible;

    [ObservableProperty]
    private bool _isInstanceLoading;

    [ObservableProperty]
    private string _instanceLoadingMessage = string.Empty;

    [ObservableProperty]
    private bool _showStartupWarmProgress;

    [ObservableProperty]
    private int _startupWarmCompleted;

    [ObservableProperty]
    private int _startupWarmTotal;

    [ObservableProperty]
    private string _startupWarmAccountName = string.Empty;

    public double StartupWarmProgress =>
        StartupWarmTotal <= 0 ? 0 : (double)StartupWarmCompleted / StartupWarmTotal;

    public string StartupWarmStatusText =>
        StartupWarmTotal <= 0
            ? InstanceLoadingMessage
            : string.IsNullOrWhiteSpace(StartupWarmAccountName)
                ? $"Starting accounts ({StartupWarmCompleted} of {StartupWarmTotal})…"
                : $"Starting {StartupWarmAccountName} ({StartupWarmCompleted} of {StartupWarmTotal})…";

    public void ApplySelection(bool dashboardSelected, string? instanceId, bool settingsSelected)
    {
        IsDashboardSelected = dashboardSelected;
        IsSettingsSelected = settingsSelected;
        SelectedInstanceId = instanceId;
    }

    public void SetInstanceLoading(bool isLoading, string? message)
    {
        IsInstanceLoading = isLoading;
        InstanceLoadingMessage = string.IsNullOrWhiteSpace(message)
            ? "Loading instance..."
            : message!;
        if (!isLoading)
        {
            ResetStartupWarmProgress();
        }
    }

    public void BeginStartupWarm(int totalAccounts)
    {
        ShowStartupWarmProgress = totalAccounts > 0;
        StartupWarmTotal = totalAccounts;
        StartupWarmCompleted = 0;
        StartupWarmAccountName = string.Empty;
        IsInstanceLoading = totalAccounts > 0;
        OnPropertyChanged(nameof(StartupWarmProgress));
        OnPropertyChanged(nameof(StartupWarmStatusText));
    }

    public void ReportStartupWarmProgress(int completed, int total, string? accountDisplayName)
    {
        StartupWarmCompleted = completed;
        StartupWarmTotal = total;
        StartupWarmAccountName = accountDisplayName ?? string.Empty;
        OnPropertyChanged(nameof(StartupWarmProgress));
        OnPropertyChanged(nameof(StartupWarmStatusText));
    }

    public void ResetStartupWarmProgress()
    {
        ShowStartupWarmProgress = false;
        StartupWarmCompleted = 0;
        StartupWarmTotal = 0;
        StartupWarmAccountName = string.Empty;
        OnPropertyChanged(nameof(StartupWarmProgress));
        OnPropertyChanged(nameof(StartupWarmStatusText));
    }
}
