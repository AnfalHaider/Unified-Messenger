using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace UnifiedMessenger.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly SettingsViewModel _viewModel = new();

    private ApplicationServices _services = new();
    private IInstanceRegistryService? _registry;
    private bool _suppressToggleEvents;

    private readonly Dictionary<string, FrameworkElement> _sectionAnchors = new(StringComparer.OrdinalIgnoreCase);

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        InitializeSectionNav();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnsureComboBoxesInitialized();
        ImportBackupCheckBox.IsChecked = _viewModel.CreateImportBackup;
        ApplySettingsAccessibilityNames();
        AccessibilityTabOrderHelper.ApplyTabIndex(SectionNavList, AccessibilityTabOrderHelper.SettingsSectionNav);
        AccessibilityTabOrderHelper.ApplyTabIndex(SettingsScrollViewer, AccessibilityTabOrderHelper.SettingsContent);
    }

    private void ApplySettingsAccessibilityNames()
    {
        AutomationProperties.SetName(SectionNavList, "Settings sections");
        AutomationProperties.SetName(BackgroundToastsToggle, "Background toast notifications");
        AutomationProperties.SetName(TaskbarBadgeToggle, "Taskbar badge");
        AutomationProperties.SetName(PanelAutoOpenBox, "Auto-open notification panel");
        AutomationProperties.SetName(ToastSoundBox, "Toast sound");
        AutomationProperties.SetName(IncludeMutedBadgesToggle, "Include muted chats in badge totals");
        AutomationProperties.SetName(ToastGroupToggle, "Group toasts by instance");
        AutomationProperties.SetName(ToastBrandingToggle, "Use platform branding on toasts");
        AutomationProperties.SetName(ClearNotificationsButton, "Clear notification history");
        AutomationProperties.SetName(ThemePreferenceBox, "Theme preference");
        AutomationProperties.SetName(PanelDockBox, "Notification panel dock position");
        AutomationProperties.SetName(StartupWarmModeBox, "Startup warm mode");
        AutomationProperties.SetName(MaxConcurrentWebViewsBox, "Maximum concurrent WebViews");
        AutomationProperties.SetName(RefreshAllWebViewsButton, "Refresh all WebViews");
        AutomationProperties.SetName(ExperimentalExpander, "Experimental session options");
        AutomationProperties.SetName(EnableLazyWebViewLoadingToggle, "Enable lazy WebView loading");
        AutomationProperties.SetName(EnablePerInstanceSleepUnloadToggle, "Enable per-instance sleep unload");
        AutomationProperties.SetName(EnableEditInstanceMetadataToggle, "Enable edit instance metadata");
        AutomationProperties.SetName(EnableImportExportInstancesToggle, "Enable import export instances");
        AutomationProperties.SetName(EnableInstanceNotesTagsToggle, "Enable instance notes and tags");
        AutomationProperties.SetName(SlaThresholdBox, "SLA threshold minutes");
        AutomationProperties.SetName(DashboardUrgencyThresholdBox, "Dashboard urgency threshold");
        AutomationProperties.SetName(EnableStartupBackfillToggle, "Enable startup backfill");
        AutomationProperties.SetName(ShowHeuristicExecutiveInsightsToggle, "Show heuristic executive insights");
        AutomationProperties.SetName(PlatformModulesList, "Platform module toggles");
        AutomationProperties.SetName(BranchOperationalCatalogList, "Branch service catalog");
        AutomationProperties.SetName(ClearAnalyticsButton, "Clear operational data");
        AutomationProperties.SetName(ExportInstancesButton, "Export instances");
        AutomationProperties.SetName(ImportBackupCheckBox, "Create backup before import");
        AutomationProperties.SetName(ImportInstancesButton, "Import instances");
        AutomationProperties.SetName(RunInBackgroundOnCloseToggle, "Run in background on close");
        AutomationProperties.SetName(LaunchAtStartupToggle, "Launch at startup");
        AutomationProperties.SetName(PromptPinToTaskbarToggle, "Suggest pin to taskbar");
        AutomationProperties.SetName(EnableAutoUpdateToggle, "Enable auto update");
        AutomationProperties.SetName(PromptBeforeAutoUpdateToggle, "Prompt before auto-update installs");
        AutomationProperties.SetName(CheckForUpdatesButton, "Check for updates");
        AutomationProperties.SetName(ArchivedAccountsList, "Removed accounts");
    }

    private void InitializeSectionNav()
    {
        _sectionAnchors[SettingsNavigationHelper.NotificationsSectionKey] = NotificationsSection;
        _sectionAnchors[SettingsNavigationHelper.AppearanceSectionKey] = AppearanceSection;
        _sectionAnchors[SettingsNavigationHelper.SessionPerformanceSectionKey] = SessionPerformanceSection;
        _sectionAnchors[SettingsNavigationHelper.PlatformModulesSectionKey] = PlatformModulesSection;
        _sectionAnchors[SettingsNavigationHelper.ProfessionalMetricsSectionKey] = ProfessionalMetricsSection;
        _sectionAnchors[SettingsNavigationHelper.DataPrivacySectionKey] = DataPrivacySection;
        _sectionAnchors[SettingsNavigationHelper.SystemSectionKey] = SystemSection;
        _sectionAnchors[SettingsNavigationHelper.RemovedAccountsSectionKey] = RemovedAccountsSection;
        _sectionAnchors[SettingsNavigationHelper.StorageSectionKey] = StorageSection;
        _sectionAnchors[SettingsNavigationHelper.LocalAiSectionKey] = LocalAiSection;
        _sectionAnchors[SettingsNavigationHelper.AboutSectionKey] = AboutSection;

        _viewModel.SectionNavItems.Clear();
        foreach (var item in SettingsNavigationHelper.BuildSectionNavItems())
        {
            _viewModel.SectionNavItems.Add(item);
        }

        SectionNavList.ItemsSource = _viewModel.SectionNavItems;
        SectionNavList.SelectedIndex = 0;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is RegistryNavigationArgs args)
        {
            _registry = args.Registry;
            if (args.Services is not null)
            {
                _services = args.Services;
            }
        }

        RefreshAll();

        if (e.Parameter is RegistryNavigationArgs { SettingsSectionKey: { Length: > 0 } sectionKey })
        {
            DispatcherQueue.TryEnqueue(() => NavigateToSectionKey(sectionKey));
        }
    }

    public void RefreshAll()
    {
        EnsureComboBoxesInitialized();

        var settings = _services.AppSettings.Settings;

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
        RefreshBranchOperationalCatalog(settings);
        RefreshPlatformModules(settings);
        _suppressToggleEvents = false;

        UpdateImportExportPanelVisibility(settings.EnableImportExportInstances);
        RefreshArchivedAccounts();
        RefreshStoragePaths();
        _viewModel.VersionLabel = SettingsPageHelper.BuildVersionLabel(typeof(App).Assembly.GetName().Version);
        VersionText.Text = _viewModel.VersionLabel;
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
        _viewModel.InstancesStorePath = SettingsPageHelper.ResolveInstancesStorePath(_registry?.StorePath);
        _viewModel.ProfilesPath = WebViewProfileManager.Instance.UserDataFolder;
        InstancesPathText.Text = _viewModel.InstancesStorePath;
        ProfilesPathText.Text = _viewModel.ProfilesPath;
    }

    private void RefreshBranchOperationalCatalog(AppSettings settings)
    {
        _viewModel.BranchOperationalCatalogRows.Clear();
        foreach (var row in SettingsPageHelper.BuildBranchOperationalCatalogRows(settings.BranchOperationalCatalog))
        {
            _viewModel.BranchOperationalCatalogRows.Add(row);
        }

        BranchOperationalCatalogList.ItemsSource = _viewModel.BranchOperationalCatalogRows;
    }

    private void RefreshPlatformModules(AppSettings settings)
    {
        _viewModel.PlatformModuleRows.Clear();
        foreach (var row in PlatformModuleSettingsHelper.BuildToggleRows(settings))
        {
            _viewModel.PlatformModuleRows.Add(row);
        }

        PlatformModulesList.ItemsSource = _viewModel.PlatformModuleRows;
    }

    private async void PlatformModuleToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents ||
            sender is not ToggleSwitch { Tag: string platformId, IsOn: var isEnabled })
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            PlatformModuleSettingsHelper.SetPlatformEnabled(settings, platformId, isEnabled));
    }

    private async void BranchCatalogField_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents ||
            sender is not TextBox { Tag: string branchKey })
        {
            return;
        }

        var row = _viewModel.BranchOperationalCatalogRows
            .FirstOrDefault(entry => entry.BranchKey.Equals(branchKey, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
        {
            var index = settings.BranchOperationalCatalog.FindIndex(profile =>
                profile.BranchKey.Equals(branchKey, StringComparison.OrdinalIgnoreCase));
            var updated = SettingsPageHelper.ToBranchOperationalProfile(row);

            if (index >= 0)
            {
                settings.BranchOperationalCatalog[index] = updated;
            }
            else
            {
                settings.BranchOperationalCatalog.Add(updated);
            }
        });
    }

    private void RefreshArchivedAccounts()
    {
        if (_registry is null)
        {
            _viewModel.ArchivedAccounts.Clear();
            ArchivedAccountsList.ItemsSource = null;
            _viewModel.ShowNoArchivedAccounts = true;
            NoArchivedAccountsText.Visibility = Visibility.Visible;
            return;
        }

        var rows = SettingsArchivedAccountsPresenter.BuildRows(_registry.ArchivedInstances);
        _viewModel.ArchivedAccounts.Clear();
        foreach (var row in rows)
        {
            _viewModel.ArchivedAccounts.Add(row);
        }

        ArchivedAccountsList.ItemsSource = _viewModel.ArchivedAccounts;
        _viewModel.ShowNoArchivedAccounts = rows.Count == 0;
        NoArchivedAccountsText.Visibility = _viewModel.ShowNoArchivedAccounts
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateImportExportPanelVisibility(bool isVisible)
    {
        _viewModel.ShowImportExportPanel = isVisible;
        ImportExportPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SectionNavList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not SettingsSectionNavItemViewModel item)
        {
            return;
        }

        NavigateToSectionKey(item.Key);
    }

    private void NavigateToSectionKey(string sectionKey)
    {
        _viewModel.SelectedSectionKey = sectionKey;

        if (sectionKey.Equals(SettingsNavigationHelper.LocalAiSectionKey, StringComparison.OrdinalIgnoreCase))
        {
            Frame?.Navigate(typeof(LocalAISettingsPage), _services);
            return;
        }

        if (sectionKey.Equals(SettingsNavigationHelper.AboutSectionKey, StringComparison.OrdinalIgnoreCase))
        {
            Frame?.Navigate(typeof(AboutPage));
            return;
        }

        if (!_sectionAnchors.TryGetValue(sectionKey, out var anchor))
        {
            return;
        }

        ScrollSectionIntoView(anchor);
    }

    private void ScrollSectionIntoView(FrameworkElement section)
    {
        var transform = section.TransformToVisual(SettingsScrollViewer);
        var point = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
        SettingsScrollViewer.ChangeView(null, point.Y, null, disableAnimation: false);
    }

    private async void BackgroundToastsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableBackgroundToasts = BackgroundToastsToggle.IsOn);
    }

    private async void TaskbarBadgeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.ShowTaskbarBadge = TaskbarBadgeToggle.IsOn);

        _ = TaskbarBadgeService.Instance.SyncBadgeAsync(_services.NotificationHub.TotalUnreadCount);
    }

    private async void ThemePreferenceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || ThemePreferenceBox.SelectedItem is not ThemePreferenceOption option)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.ThemePreference = option.Preference);
    }

    private async void PanelAutoOpenBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || PanelAutoOpenBox.SelectedItem is not PanelAutoOpenOption option)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.PanelAutoOpen = option.Mode);
    }

    private async void ToastSoundBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || ToastSoundBox.SelectedItem is not ToastSoundOption option)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.ToastSound = option.Preference);
    }

    private async void PanelDockBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || PanelDockBox.SelectedItem is not PanelDockOption option)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.PanelDock = option.Dock);

        _services.Navigation.RequestLayoutRefresh();
    }

    private async void StartupWarmModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressToggleEvents || StartupWarmModeBox.SelectedItem is not StartupWarmModeOption option)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
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

        await _services.AppSettings.UpdateAsync(settings =>
            settings.MaxConcurrentWebViews = value);
    }

    private async void RefreshAllWebViewsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshAllWebViewsButton.IsEnabled = false;
        var originalContent = RefreshAllWebViewsButton.Content;
        RefreshAllWebViewsButton.Content = "Refreshing…";
        try
        {
            await _services.SessionManager.ReloadAllSessionsAsync();
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

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableLazyWebViewLoading = EnableLazyWebViewLoadingToggle.IsOn);
    }

    private async void EnablePerInstanceSleepUnloadToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnablePerInstanceSleepUnload = EnablePerInstanceSleepUnloadToggle.IsOn);
    }

    private async void EnableEditInstanceMetadataToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableEditInstanceMetadata = EnableEditInstanceMetadataToggle.IsOn);
    }

    private async void EnableImportExportInstancesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableImportExportInstances = EnableImportExportInstancesToggle.IsOn);

        UpdateImportExportPanelVisibility(EnableImportExportInstancesToggle.IsOn);
    }

    private async void EnableInstanceNotesTagsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableInstanceNotesTags = EnableInstanceNotesTagsToggle.IsOn);
    }

    private async void RunInBackgroundOnCloseToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.RunInBackgroundOnClose = RunInBackgroundOnCloseToggle.IsOn);
    }

    private async void EnableStartupBackfillToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableStartupBackfill = EnableStartupBackfillToggle.IsOn);
    }

    private async void DashboardUrgencyThresholdBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressToggleEvents || double.IsNaN(args.NewValue))
        {
            return;
        }

        var value = (int)Math.Round(args.NewValue, MidpointRounding.AwayFromZero);
        await _services.AppSettings.UpdateAsync(settings =>
            settings.DashboardUrgencyThreshold = value);
    }

    private async void ShowHeuristicExecutiveInsightsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
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
            await _services.AppSettings.UpdateAsync(settings =>
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

        await _services.AppSettings.UpdateAsync(settings =>
            settings.PromptPinToTaskbar = PromptPinToTaskbarToggle.IsOn);
    }

    private async void EnableAutoUpdateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.EnableAutoUpdate = EnableAutoUpdateToggle.IsOn);
    }

    private async void PromptBeforeAutoUpdateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.PromptBeforeAutoUpdate = PromptBeforeAutoUpdateToggle.IsOn);
    }

    private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await _services.GitHubUpdate.CheckForUpdatesManualAsync();

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
        _services.Navigation.RequestDashboardRefresh();
    }

    private async void ExportInstancesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_registry is null)
        {
            return;
        }

        var summary = SettingsImportExportPresenter.BuildExportSummary(
            _registry.Instances,
            _registry.ArchivedInstances,
            _registry.StorePath);

        var preExportDialog = new ContentDialog
        {
            Title = "Export instances?",
            Content = SettingsImportExportPresenter.BuildPreExportDialogContent(summary),
            PrimaryButtonText = "Choose file",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await preExportDialog.ShowAsync() != ContentDialogResult.Primary)
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

        SettingsImportSummary importSummary;
        try
        {
            await using var stream = File.OpenRead(file.Path);
            var imported = await System.Text.Json.JsonSerializer
                .DeserializeAsync<InstanceStore>(stream)
                .ConfigureAwait(true)
                ?? throw new InvalidDataException("Import file is empty or invalid.");

            importSummary = SettingsImportExportPresenter.BuildImportSummary(file.Path, imported);
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException)
        {
            await ShowMessageDialogAsync("Import failed", "Import file is not valid JSON.");
            return;
        }

        _viewModel.CreateImportBackup = ImportBackupCheckBox.IsChecked == true;
        var confirm = new ContentDialog
        {
            Title = "Import instances?",
            Content = SettingsImportExportPresenter.BuildImportDialogContent(
                importSummary,
                _viewModel.CreateImportBackup),
            PrimaryButtonText = "Import",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            if (_viewModel.CreateImportBackup && File.Exists(_registry.StorePath))
            {
                var backupPath = SettingsPageHelper.BuildImportBackupPath(_registry.StorePath);
                File.Copy(_registry.StorePath, backupPath, overwrite: true);
            }

            var result = await _registry.ImportInstancesAsync(file.Path);
            RefreshArchivedAccounts();
            RefreshStoragePaths();
            _services.Navigation.RequestInstanceRegistryRefresh();
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

        await _services.AppSettings.UpdateAsync(settings =>
            settings.IncludeMutedChatBadges = IncludeMutedBadgesToggle.IsOn);

        await _services.SessionManager.BroadcastAdapterSettingsAsync();
    }

    private async void ToastGroupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
            settings.ToastGroupByInstance = ToastGroupToggle.IsOn);
    }

    private async void ToastBrandingToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents)
        {
            return;
        }

        await _services.AppSettings.UpdateAsync(settings =>
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

        await _services.AppSettings.UpdateAsync(settings =>
            settings.SlaThresholdMinutes = minutes);

        _services.Navigation.RequestDashboardRefresh();
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

        _services.NotificationHub.ClearAlerts();
    }

    private void RestoreAccountButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string instanceId } &&
            ShellNavigationService.IsValidInstanceId(instanceId))
        {
            _services.Navigation.RequestArchivedInstanceRestore(instanceId);
        }
    }

    private async void PermanentDeleteAccountButton_Click(object sender, RoutedEventArgs e)
    {
        if (_registry is null ||
            sender is not Button { Tag: string instanceId } ||
            !ShellNavigationService.IsValidInstanceId(instanceId))
        {
            return;
        }

        var row = _viewModel.ArchivedAccounts
            .FirstOrDefault(account =>
                account.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase));

        var confirm = new ContentDialog
        {
            Title = "Delete account permanently?",
            Content = SettingsPageHelper.BuildPermanentDeleteConfirmation(row?.DisplayName),
            PrimaryButtonText = "Delete permanently",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            var instance = InstanceDeletionService.ResolveInstance(_registry, instanceId);
            if (instance is null)
            {
                await ShowMessageDialogAsync("Delete failed", "Account not found.");
                return;
            }

            await InstanceDeletionService.DeleteAsync(
                _services,
                instance,
                DeleteInstanceChoice.PermanentDelete);

            RefreshArchivedAccounts();
            RefreshStoragePaths();
            _services.Navigation.RequestInstanceRegistryRefresh();
        }
        catch (Exception ex)
        {
            await ShowMessageDialogAsync("Delete failed", ex.Message);
        }
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
