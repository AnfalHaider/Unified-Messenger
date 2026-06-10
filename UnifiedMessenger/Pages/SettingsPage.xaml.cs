using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;
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
        _sectionAnchors[SettingsNavigationHelper.KeyboardShortcutsSectionKey] = KeyboardShortcutsSection;
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
        EnsureAppearanceComboBoxesInitialized();
        EnsureNotificationsComboBoxesInitialized();
        EnsureSessionComboBoxesInitialized();
    }

    private void RefreshStoragePaths()
    {
        _viewModel.InstancesStorePath = SettingsPageHelper.ResolveInstancesStorePath(_registry?.StorePath);
        _viewModel.ProfilesPath = WebViewProfileManager.Instance.UserDataFolder;
        InstancesPathText.Text = _viewModel.InstancesStorePath;
        ProfilesPathText.Text = _viewModel.ProfilesPath;
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
}
