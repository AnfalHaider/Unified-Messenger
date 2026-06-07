using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace UnifiedMessenger.Pages;

public sealed partial class SettingsPage : Page
{
    private InstanceRegistryService? _registry;
    private bool _suppressToggleEvents;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnsureComboBoxesInitialized();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is RegistryNavigationArgs args)
        {
            _registry = args.Registry;
        }

        RefreshAll();
    }

    public void RefreshAll()
    {
        EnsureComboBoxesInitialized();

        var settings = AppSettingsService.Instance.Settings;

        _suppressToggleEvents = true;
        BackgroundToastsToggle.IsOn = settings.EnableBackgroundToasts;
        TaskbarBadgeToggle.IsOn = settings.ShowTaskbarBadge;
        ThemePreferenceBox.SelectedItem = ((ThemePreferenceOption[])ThemePreferenceBox.ItemsSource!)
            .FirstOrDefault(o => o.Preference == settings.ThemePreference)
            ?? ((ThemePreferenceOption[])ThemePreferenceBox.ItemsSource!)[0];
        PanelAutoOpenBox.SelectedItem = ((PanelAutoOpenOption[])PanelAutoOpenBox.ItemsSource!)
            .FirstOrDefault(o => o.Mode == settings.PanelAutoOpen)
            ?? ((PanelAutoOpenOption[])PanelAutoOpenBox.ItemsSource!)[0];
        ToastSoundBox.SelectedItem = ((ToastSoundOption[])ToastSoundBox.ItemsSource!)
            .FirstOrDefault(o => o.Preference == settings.ToastSound)
            ?? ((ToastSoundOption[])ToastSoundBox.ItemsSource!)[0];
        PanelDockBox.SelectedItem = ((PanelDockOption[])PanelDockBox.ItemsSource!)
            .FirstOrDefault(o => o.Dock == settings.PanelDock)
            ?? ((PanelDockOption[])PanelDockBox.ItemsSource!)[0];
        StartupWarmModeBox.SelectedItem = ((StartupWarmModeOption[])StartupWarmModeBox.ItemsSource!)
            .FirstOrDefault(o => o.Mode == settings.StartupWarmMode)
            ?? ((StartupWarmModeOption[])StartupWarmModeBox.ItemsSource!)[0];
        MaxConcurrentWebViewsBox.Value = settings.MaxConcurrentWebViews;
        IncludeMutedBadgesToggle.IsOn = settings.IncludeMutedChatBadges;
        ToastGroupToggle.IsOn = settings.ToastGroupByInstance;
        ToastBrandingToggle.IsOn = settings.ToastUsePlatformBranding;
        EnableLazyWebViewLoadingToggle.IsOn = settings.EnableLazyWebViewLoading;
        EnablePerInstanceSleepUnloadToggle.IsOn = settings.EnablePerInstanceSleepUnload;
        EnableEditInstanceMetadataToggle.IsOn = settings.EnableEditInstanceMetadata;
        EnableImportExportInstancesToggle.IsOn = settings.EnableImportExportInstances;
        EnableInstanceNotesTagsToggle.IsOn = settings.EnableInstanceNotesTags;
        RunInBackgroundOnCloseToggle.IsOn = settings.RunInBackgroundOnClose;
        EnableStartupBackfillToggle.IsOn = settings.EnableStartupBackfill;
        DashboardUrgencyThresholdBox.Value = settings.DashboardUrgencyThreshold;
        ShowHeuristicExecutiveInsightsToggle.IsOn = settings.ShowHeuristicExecutiveInsights;
        LaunchAtStartupToggle.IsOn = StartupTaskService.EnsureRegistrationMatchesPreference(
            settings.LaunchAtStartup);
        PromptPinToTaskbarToggle.IsOn = settings.PromptPinToTaskbar;
        EnableAutoUpdateToggle.IsOn = settings.EnableAutoUpdate;
        PromptBeforeAutoUpdateToggle.IsOn = settings.PromptBeforeAutoUpdate;
        SlaThresholdBox.Value = settings.SlaThresholdMinutes;
        _suppressToggleEvents = false;

        UpdateImportExportPanelVisibility(settings.EnableImportExportInstances);
        RefreshArchivedAccounts();
        RefreshStoragePaths();
        VersionText.Text = SettingsPageHelper.BuildVersionLabel(typeof(App).Assembly.GetName().Version);
    }

    private void EnsureComboBoxesInitialized()
    {
        if (ThemePreferenceBox.ItemsSource is null)
        {
            ThemePreferenceBox.ItemsSource = new[]
            {
                new ThemePreferenceOption(AppThemePreference.System, "Use system setting"),
                new ThemePreferenceOption(AppThemePreference.Light, "Light"),
                new ThemePreferenceOption(AppThemePreference.Dark, "Dark")
            };
            ThemePreferenceBox.DisplayMemberPath = nameof(ThemePreferenceOption.Label);
        }

        if (PanelAutoOpenBox.ItemsSource is null)
        {
            PanelAutoOpenBox.ItemsSource = new[]
            {
                new PanelAutoOpenOption(
                    NotificationPanelAutoOpenMode.UnfocusedOnly,
                    "When app is unfocused or in background"),
                new PanelAutoOpenOption(NotificationPanelAutoOpenMode.Always, "Always on new alerts"),
                new PanelAutoOpenOption(NotificationPanelAutoOpenMode.Never, "Never auto-open")
            };
            PanelAutoOpenBox.DisplayMemberPath = nameof(PanelAutoOpenOption.Label);
        }

        if (ToastSoundBox.ItemsSource is null)
        {
            ToastSoundBox.ItemsSource = new[]
            {
                new ToastSoundOption(ToastSoundPreference.Default, "Default"),
                new ToastSoundOption(ToastSoundPreference.Silent, "Silent")
            };
            ToastSoundBox.DisplayMemberPath = nameof(ToastSoundOption.Label);
        }

        if (PanelDockBox.ItemsSource is null)
        {
            PanelDockBox.ItemsSource = new[]
            {
                new PanelDockOption(NotificationPanelDock.Right, "Right"),
                new PanelDockOption(NotificationPanelDock.Bottom, "Bottom")
            };
            PanelDockBox.DisplayMemberPath = nameof(PanelDockOption.Label);
        }

        if (StartupWarmModeBox.ItemsSource is null)
        {
            StartupWarmModeBox.ItemsSource = new[]
            {
                new StartupWarmModeOption(StartupWarmMode.WarmAll, "Warm all"),
                new StartupWarmModeOption(StartupWarmMode.VisibleOnly, "Visible only"),
                new StartupWarmModeOption(StartupWarmMode.Lazy, "Lazy")
            };
            StartupWarmModeBox.DisplayMemberPath = nameof(StartupWarmModeOption.Label);
        }
    }

    private void RefreshStoragePaths()
    {
        InstancesPathText.Text = SettingsPageHelper.ResolveInstancesStorePath(_registry?.StorePath);
        ProfilesPathText.Text = WebViewProfileManager.Instance.UserDataFolder;
    }

    private void RefreshArchivedAccounts()
    {
        if (_registry is null)
        {
            ArchivedAccountsList.ItemsSource = null;
            NoArchivedAccountsText.Visibility = Visibility.Visible;
            return;
        }

        var items = SettingsPageHelper.BuildArchivedAccountItems(_registry.ArchivedInstances);

        ArchivedAccountsList.ItemsSource = items;
        NoArchivedAccountsText.Visibility = items.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateImportExportPanelVisibility(bool isVisible)
    {
        ImportExportPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void BackgroundToastsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.EnableBackgroundToasts = BackgroundToastsToggle.IsOn);
    }

    private async void TaskbarBadgeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.ShowTaskbarBadge = TaskbarBadgeToggle.IsOn);

        _ = TaskbarBadgeService.Instance.SyncBadgeAsync(NotificationHub.Instance.TotalUnreadCount);
    }

    private async void ThemePreferenceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || ThemePreferenceBox.SelectedItem is not ThemePreferenceOption option)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.ThemePreference = option.Preference);
    }

    private async void PanelAutoOpenBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || PanelAutoOpenBox.SelectedItem is not PanelAutoOpenOption option)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.PanelAutoOpen = option.Mode);
    }

    private async void ToastSoundBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || ToastSoundBox.SelectedItem is not ToastSoundOption option)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.ToastSound = option.Preference);
    }

    private async void PanelDockBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || PanelDockBox.SelectedItem is not PanelDockOption option)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.PanelDock = option.Dock);

        ShellNavigationService.Instance.RequestLayoutRefresh();
    }

    private async void StartupWarmModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || StartupWarmModeBox.SelectedItem is not StartupWarmModeOption option)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.StartupWarmMode = option.Mode);
    }

    private async void MaxConcurrentWebViewsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressToggleEvents || double.IsNaN(args.NewValue))
        {
            return;
        }

        var value = SettingsPageHelper.NormalizeMaxConcurrentWebViews(args.NewValue);
        if (SettingsPageHelper.RequiresNumberBoxCorrection(value, args.NewValue))
        {
            sender.Value = value;
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.MaxConcurrentWebViews = value);
    }

    private async void RefreshAllWebViewsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshAllWebViewsButton.IsEnabled = false;
        var originalContent = RefreshAllWebViewsButton.Content;
        RefreshAllWebViewsButton.Content = "Refreshing…";
        try
        {
            await InstanceSessionManager.Instance.ReloadAllSessionsAsync();
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync("WebView refresh failed", ex.Message);
        }
        finally
        {
            RefreshAllWebViewsButton.IsEnabled = true;
            RefreshAllWebViewsButton.Content = originalContent;
        }
    }

    private async void EnableLazyWebViewLoadingToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.EnableLazyWebViewLoading = EnableLazyWebViewLoadingToggle.IsOn);
    }

    private async void EnablePerInstanceSleepUnloadToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.EnablePerInstanceSleepUnload = EnablePerInstanceSleepUnloadToggle.IsOn);
    }

    private async void EnableEditInstanceMetadataToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.EnableEditInstanceMetadata = EnableEditInstanceMetadataToggle.IsOn);
    }

    private async void EnableImportExportInstancesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.EnableImportExportInstances = EnableImportExportInstancesToggle.IsOn);

        UpdateImportExportPanelVisibility(EnableImportExportInstancesToggle.IsOn);
    }

    private async void EnableInstanceNotesTagsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.EnableInstanceNotesTags = EnableInstanceNotesTagsToggle.IsOn);
    }

    private async void RunInBackgroundOnCloseToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.RunInBackgroundOnClose = RunInBackgroundOnCloseToggle.IsOn);
    }

    private async void EnableStartupBackfillToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.EnableStartupBackfill = EnableStartupBackfillToggle.IsOn);
    }

    private async void DashboardUrgencyThresholdBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressToggleEvents || double.IsNaN(args.NewValue))
        {
            return;
        }

        var value = (int)Math.Round(args.NewValue, MidpointRounding.AwayFromZero);
        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.DashboardUrgencyThreshold = value);
    }

    private async void ShowHeuristicExecutiveInsightsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.ShowHeuristicExecutiveInsights = ShowHeuristicExecutiveInsightsToggle.IsOn);
    }

    private async void LaunchAtStartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        try
        {
            StartupTaskService.SetRegistered(LaunchAtStartupToggle.IsOn);
            await AppSettingsService.Instance.UpdateAsync(settings =>
                settings.LaunchAtStartup = LaunchAtStartupToggle.IsOn);
        }
        catch (Exception ex)
        {
            _suppressToggleEvents = true;
            LaunchAtStartupToggle.IsOn = StartupTaskService.IsRegisteredForCurrentExecutable();
            _suppressToggleEvents = false;
            await ShowMessageDialogAsync("Startup registration failed", ex.Message);
        }
    }

    private async void PromptPinToTaskbarToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.PromptPinToTaskbar = PromptPinToTaskbarToggle.IsOn);
    }

    private async void EnableAutoUpdateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.EnableAutoUpdate = EnableAutoUpdateToggle.IsOn);
    }

    private async void PromptBeforeAutoUpdateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.PromptBeforeAutoUpdate = PromptBeforeAutoUpdateToggle.IsOn);
    }

    private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await GitHubUpdateService.Instance.CheckForUpdatesManualAsync();

        var dialog = new ContentDialog
        {
            Title = "Update check",
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };

        dialog.Content = new TextBlock
        {
            Text = SettingsPageHelper.BuildUpdateCheckMessage(result),
            TextWrapping = TextWrapping.WrapWholeWords
        };

        await dialog.ShowAsync();
    }

    private async void ClearAnalyticsButton_Click(object sender, RoutedEventArgs e)
    {
        var confirm = new ContentDialog
        {
            Title = "Clear operational data?",
            Content = "This permanently removes message analytics and saved thread/triage state used by the Operations Command Center.",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await OperationalDataService.ClearAllAsync();
        ShellNavigationService.Instance.RequestDashboardRefresh();
    }

    private async void ExportInstancesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_registry is null)
        {
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "instances",
            FileTypeChoices = { { "Instances JSON", [".json"] } }
        };

        InitializePicker(picker);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            await _registry.ExportInstancesAsync(file.Path);
            await ShowMessageDialogAsync("Export complete", $"Saved to {file.Path}");
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync("Export failed", ex.Message);
        }
    }

    private async void ImportInstancesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_registry is null)
        {
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Import instances?",
            Content = "This replaces your current instance list with the imported file.",
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            FileTypeFilter = { ".json" }
        };

        InitializePicker(picker);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            var result = await _registry.ImportInstancesAsync(file.Path);
            RefreshArchivedAccounts();
            RefreshStoragePaths();
            ShellNavigationService.Instance.RequestInstanceRegistryRefresh();
            await ShowMessageDialogAsync(
                "Import complete",
                SettingsPageHelper.BuildImportSuccessMessage(result.ActiveCount, result.ArchivedCount));
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync("Import failed", ex.Message);
        }
    }

    private async void IncludeMutedBadgesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.IncludeMutedChatBadges = IncludeMutedBadgesToggle.IsOn);

        await InstanceSessionManager.Instance.BroadcastAdapterSettingsAsync();
    }

    private async void ToastGroupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.ToastGroupByInstance = ToastGroupToggle.IsOn);
    }

    private async void ToastBrandingToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.ToastUsePlatformBranding = ToastBrandingToggle.IsOn);
    }

    private async void SlaThresholdBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressToggleEvents || double.IsNaN(args.NewValue))
        {
            return;
        }

        var minutes = SettingsPageHelper.NormalizeSlaThresholdMinutes(args.NewValue);
        if (SettingsPageHelper.RequiresNumberBoxCorrection(minutes, args.NewValue))
        {
            sender.Value = minutes;
            return;
        }

        await AppSettingsService.Instance.UpdateAsync(settings =>
            settings.SlaThresholdMinutes = minutes);

        ShellNavigationService.Instance.RequestDashboardRefresh();
    }

    private async void ClearNotificationsButton_Click(object sender, RoutedEventArgs e)
    {
        var confirm = new ContentDialog
        {
            Title = "Clear notification history?",
            Content = "This removes all stored notification alerts. Unread badges on sidebar icons will reset.",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        NotificationHub.Instance.ClearAlerts();
    }

    private void RestoreAccountButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string instanceId } &&
            ShellNavigationService.IsValidInstanceId(instanceId))
        {
            ShellNavigationService.Instance.RequestArchivedInstanceRestore(instanceId);
        }
    }

    private void LocalAiSettingsLink_Click(object sender, RoutedEventArgs e) =>
        Frame?.Navigate(typeof(LocalAISettingsPage));

    private void AboutLink_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(AboutPage));
    }

    private static void InitializePicker(object picker)
    {
        if (App.CurrentWindow is null)
        {
            return;
        }

        var hwnd = WindowNative.GetWindowHandle(App.CurrentWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
    }

    private async Task ShowMessageDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }

    private sealed record ThemePreferenceOption(AppThemePreference Preference, string Label);

    private sealed record PanelAutoOpenOption(NotificationPanelAutoOpenMode Mode, string Label);

    private sealed record ToastSoundOption(ToastSoundPreference Preference, string Label);

    private sealed record PanelDockOption(NotificationPanelDock Dock, string Label);

    private sealed record StartupWarmModeOption(StartupWarmMode Mode, string Label);
}
