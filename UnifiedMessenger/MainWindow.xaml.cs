using System.Text.Json;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Controls;
using UnifiedMessenger.Dialogs;
using UnifiedMessenger.Models;
using UnifiedMessenger.Pages;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;
using Windows.System;
using Windows.UI.Shell;

namespace UnifiedMessenger;

public sealed partial class MainWindow : Window
{
    private readonly ApplicationServices _services = new();
    private readonly MainWindowViewModel _shellViewModel = new();
    private readonly AdapterHealthMonitor _adapterHealth = AdapterHealthMonitor.Instance;
    private readonly KeyboardShortcutService _keyboardShortcuts;
    private bool _notificationPanelVisible;
    private bool _panePinned;
    private bool _sidebarHoverExpanded;
    private bool _isDashboardSelected = true;
    private bool _isSettingsSelected;
    private string? _selectedInstanceId;
    private bool _isWindowActivated = true;
    private bool _isWindowVisible = true;
    private bool _pendingPanelReveal;
    private int _slaThresholdMinutes;
    private bool _forceShutdown;
    private bool _trackingStartupWarm;
    private int _startupWarmCompleted;

    private bool IsAppInForeground =>
        MainWindowShellLayout.IsAppInForeground(_isWindowVisible, _isWindowActivated);

    public MainWindow()
    {
        InitializeComponent();
        _services.ConfigureUi(() => Content.XamlRoot);
        _slaThresholdMinutes = _services.AppSettings.Settings.SlaThresholdMinutes;

        _keyboardShortcuts = new KeyboardShortcutService((UIElement)Content);
        RegisterKeyboardShortcuts();

        _services.SessionManager.AttachHost(InstanceWebViewHost);

        WorkspaceSidebar.DashboardRequested += WorkspaceSidebar_DashboardRequested;
        WorkspaceSidebar.InstanceRequested += WorkspaceSidebar_InstanceRequested;
        WorkspaceSidebar.AddInstanceRequested += WorkspaceSidebar_AddInstanceRequested;
        WorkspaceSidebar.NotificationsRequested += WorkspaceSidebar_NotificationsRequested;
        WorkspaceSidebar.SettingsRequested += WorkspaceSidebar_SettingsRequested;
        WorkspaceSidebar.InstanceContextRequested += OnInstanceContextRequested;
        WorkspaceSidebar.InstanceReorderRequested += OnInstanceReorderRequested;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        AppWindow.Changed += OnAppWindowChanged;
        AppWindow.Closing += OnAppWindowClosing;
        Activated += OnWindowActivated;
        Closed += OnMainWindowClosed;

        NotificationPanel.CollapseRequested += NotificationPanel_CollapseRequested;
        NotificationPanel.AlertClicked += NotificationPanel_AlertClicked;
        _services.NotificationHub.Changed += OnNotificationHubChanged;
        AppNotificationService.Instance.ActivationRequested += OnToastActivationRequested;
        _adapterHealth.Changed += OnAdapterHealthChanged;
        _adapterHealth.AdapterStaleDetected += OnAdapterStaleDetected;
        InstanceConnectionStatusService.Instance.Changed += OnConnectionStatusChanged;
        _services.SessionManager.SessionInitializing += OnSessionInitializing;
        _services.SessionManager.SessionFailed += OnSessionFailed;
        AutoDraftOrchestrator.Instance.DraftCompleted += OnAutoDraftCompleted;
        AttachShellNavigationHandlers();
        _services.MessageAnalytics.Changed += OnAnalyticsChanged;
        _services.AppSettings.Changed += OnAppSettingsChanged;

        WorkspaceSidebar.Loaded += WorkspaceSidebar_Loaded;
    }

    public Task RunInitializationAsync() => InitializeAsync();

    private void WorkspaceSidebar_Loaded(object sender, RoutedEventArgs e) =>
        RebuildInstanceNavigation();

    private void WorkspaceSidebar_DashboardRequested(object? sender, EventArgs e) =>
        _ = ShowDashboardAsync();

    private void WorkspaceSidebar_InstanceRequested(object? sender, string instanceId) =>
        _ = SelectInstanceAsync(instanceId);

    private void WorkspaceSidebar_AddInstanceRequested(object? sender, EventArgs e) =>
        _ = ShowAddInstanceDialogAsync();

    private void WorkspaceSidebar_NotificationsRequested(object? sender, EventArgs e) =>
        SetNotificationPanelVisible(!_notificationPanelVisible);

    private void WorkspaceSidebar_SettingsRequested(object? sender, EventArgs e) =>
        _ = ShowSettingsAsync();

    private void AttachShellNavigationHandlers()
    {
        var shell = _services.Navigation;
        shell.InstanceNavigationRequested += OnShellInstanceNavigationRequested;
        shell.DashboardRefreshRequested += OnShellDashboardRefreshRequested;
        shell.ArchivedInstanceRestoreRequested += OnArchivedInstanceRestoreRequested;
        shell.LayoutRefreshRequested += OnShellLayoutRefreshRequested;
        shell.InstanceRegistryRefreshRequested += OnShellInstanceRegistryRefreshRequested;
        shell.AddInstanceRequested += OnShellAddInstanceRequested;
    }

    private void DetachShellNavigationHandlers()
    {
        var shell = _services.Navigation;
        shell.InstanceNavigationRequested -= OnShellInstanceNavigationRequested;
        shell.DashboardRefreshRequested -= OnShellDashboardRefreshRequested;
        shell.ArchivedInstanceRestoreRequested -= OnArchivedInstanceRestoreRequested;
        shell.LayoutRefreshRequested -= OnShellLayoutRefreshRequested;
        shell.InstanceRegistryRefreshRequested -= OnShellInstanceRegistryRefreshRequested;
        shell.AddInstanceRequested -= OnShellAddInstanceRequested;
    }

    private void OnShellAddInstanceRequested(object? sender, EventArgs e) =>
        _ = ShowAddInstanceDialogAsync();

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        GlobalHotkeyService.Instance.CtrlSpacePressed -= OnGlobalCopilotHotkey;
        DetachWindowLifetimeHandlers();
        DetachShellNavigationHandlers();
        _keyboardShortcuts.Dispose();

        if (_forceShutdown)
        {
            SystemTrayService.Instance.Dispose();
        }
    }

    public void ShowFromTray()
    {
        AppWindow.Show();
        Activate();
        _isWindowVisible = true;
    }

    /// <summary>
    /// Invoked only from <see cref="SystemTrayService.TrayMenu_Quit"/> for a full process exit.
    /// </summary>
    public async void RequestTrayQuit()
    {
        if (_forceShutdown)
        {
            return;
        }

        _forceShutdown = true;
        GlobalHotkeyService.Instance.Dispose();

        await ApplicationLifecycleService.ShutdownAsync().ConfigureAwait(true);

        SystemTrayService.Instance.Dispose();
        Close();
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!ApplicationLifecycleService.ShouldHideOnClose(
                _forceShutdown,
                AppSettingsService.Instance.Settings.RunInBackgroundOnClose))
        {
            return;
        }

        args.Cancel = true;
        ApplicationLifecycleService.FlushPersistentStateFireAndForget();
        sender.Hide();
        _isWindowVisible = false;
    }

    private void DetachWindowLifetimeHandlers()
    {
        AppWindow.Changed -= OnAppWindowChanged;
        AppWindow.Closing -= OnAppWindowClosing;
        Activated -= OnWindowActivated;

        WorkspaceSidebar.Loaded -= WorkspaceSidebar_Loaded;
        WorkspaceSidebar.DashboardRequested -= WorkspaceSidebar_DashboardRequested;
        WorkspaceSidebar.InstanceRequested -= WorkspaceSidebar_InstanceRequested;
        WorkspaceSidebar.AddInstanceRequested -= WorkspaceSidebar_AddInstanceRequested;
        WorkspaceSidebar.NotificationsRequested -= WorkspaceSidebar_NotificationsRequested;
        WorkspaceSidebar.SettingsRequested -= WorkspaceSidebar_SettingsRequested;
        WorkspaceSidebar.InstanceContextRequested -= OnInstanceContextRequested;
        WorkspaceSidebar.InstanceReorderRequested -= OnInstanceReorderRequested;

        NotificationPanel.CollapseRequested -= NotificationPanel_CollapseRequested;
        NotificationPanel.AlertClicked -= NotificationPanel_AlertClicked;
        _services.NotificationHub.Changed -= OnNotificationHubChanged;
        AppNotificationService.Instance.ActivationRequested -= OnToastActivationRequested;
        _adapterHealth.Changed -= OnAdapterHealthChanged;
        _adapterHealth.AdapterStaleDetected -= OnAdapterStaleDetected;
        InstanceConnectionStatusService.Instance.Changed -= OnConnectionStatusChanged;
        _services.SessionManager.SessionInitializing -= OnSessionInitializing;
        _services.SessionManager.SessionFailed -= OnSessionFailed;
        AutoDraftOrchestrator.Instance.DraftCompleted -= OnAutoDraftCompleted;
        MessageAnalyticsService.Instance.Changed -= OnAnalyticsChanged;
        AppSettingsService.Instance.Changed -= OnAppSettingsChanged;
    }

    private void OnAnalyticsChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshDashboardIfVisible);
    }

    private void OnShellDashboardRefreshRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshDashboardIfVisible);
    }

    private void OnShellLayoutRefreshRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(ApplyNotificationPanelDockLayout);
    }

    private void OnShellInstanceRegistryRefreshRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            RebuildInstanceNavigation();
            RefreshDashboardIfVisible();
        });
    }

    private void OnAppSettingsChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ThemeService.Apply(AppSettingsService.Instance.Settings.ThemePreference);

            var slaThreshold = AppSettingsService.Instance.Settings.SlaThresholdMinutes;
            if (slaThreshold != _slaThresholdMinutes)
            {
                _slaThresholdMinutes = slaThreshold;
                MessageAnalyticsService.Instance.RecalculateSlaBreaches();
            }

            ApplyNotificationPanelDockLayout();
            _ = TaskbarBadgeService.Instance.SyncBadgeAsync(_services.NotificationHub.TotalUnreadCount);
            _ = _services.SessionManager.BroadcastAdapterSettingsAsync();
        });
    }

    private void RegisterKeyboardShortcuts()
    {
        bool CanUseGlobalShortcuts() => !CommandPaletteOverlay.IsOpen;

        _keyboardShortcuts.Register(
            VirtualKey.D,
            VirtualKeyModifiers.Control,
            () => _ = ShowDashboardAsync(),
            CanUseGlobalShortcuts);
        _keyboardShortcuts.Register(
            VirtualKey.K,
            VirtualKeyModifiers.Control,
            OpenCommandPalette,
            CanUseGlobalShortcuts);
        _keyboardShortcuts.Register(
            KeyboardShortcutService.SettingsShortcutKey,
            VirtualKeyModifiers.Control,
            () => _ = ShowSettingsAsync(),
            CanUseGlobalShortcuts);
        _keyboardShortcuts.Register(
            VirtualKey.N,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            () => SetNotificationPanelVisible(!_notificationPanelVisible),
            CanUseGlobalShortcuts);

        _keyboardShortcuts.RegisterIndexedShortcuts(
            VirtualKey.Number1,
            9,
            VirtualKeyModifiers.Control,
            ActivateInstanceByIndex,
            CanUseGlobalShortcuts);
    }

    private void ActivateInstanceByIndex(int index)
    {
        var instances = GetSidebarOrderedInstances();
        if (index < 0 || index >= instances.Count)
        {
            return;
        }

        _ = SelectInstanceAsync(instances[index].Id);
    }

    private IReadOnlyList<MessengerInstance> GetSidebarOrderedInstances()
    {
        return _services.Registry.GetOrderedInstances().ToList();
    }

    private void OpenCommandPalette()
    {
        CommandPaletteOverlay.SetEntries(BuildCommandPaletteEntries());
        CommandPaletteOverlay.Open();
    }

    private IReadOnlyList<CommandPaletteEntry> BuildCommandPaletteEntries()
    {
        var entries = new List<CommandPaletteEntry>
        {
            new()
            {
                Title = "Dashboard",
                Subtitle = "Open overview",
                Category = "Navigation",
                Selection = new CommandPaletteSelection { Action = CommandPaletteAction.OpenDashboard }
            },
            new()
            {
                Title = "Settings",
                Subtitle = "App preferences",
                Category = "Navigation",
                Selection = new CommandPaletteSelection { Action = CommandPaletteAction.OpenSettings }
            },
            new()
            {
                Title = "Toggle notification panel",
                Subtitle = "Show or hide the hub panel",
                Category = "Actions",
                Selection = new CommandPaletteSelection { Action = CommandPaletteAction.ToggleNotifications }
            },
            new()
            {
                Title = "Mark all notifications read",
                Subtitle = "Clear unread state in the hub",
                Category = "Actions",
                Selection = new CommandPaletteSelection { Action = CommandPaletteAction.MarkAllRead }
            },
            new()
            {
                Title = "Clear notification history",
                Subtitle = "Remove all hub alerts",
                Category = "Actions",
                Selection = new CommandPaletteSelection { Action = CommandPaletteAction.ClearNotifications }
            }
        };

        foreach (var instance in _services.Registry.GetOrderedInstances())
        {
            var platform = PlatformDefinition.FindById(instance.Platform);
            entries.Add(new CommandPaletteEntry
            {
                Title = instance.DisplayName,
                Subtitle = $"{platform?.DisplayName ?? instance.Platform} · {instance.Category}",
                Category = "Instances",
                Selection = new CommandPaletteSelection
                {
                    Action = CommandPaletteAction.OpenInstance,
                    InstanceId = instance.Id
                }
            });
        }

        foreach (var alert in _services.NotificationHub.GetAlertsSortedByInstance().Take(20))
        {
            entries.Add(new CommandPaletteEntry
            {
                Title = alert.Title,
                Subtitle = $"{alert.InstanceDisplayName} · {alert.Body}",
                Category = "Notifications",
                Selection = new CommandPaletteSelection
                {
                    Action = CommandPaletteAction.OpenAlert,
                    InstanceId = alert.InstanceId,
                    AlertId = alert.Id
                }
            });
        }

        return entries;
    }

    private async Task ConfirmClearNotificationsAsync()
    {
        if (_services.NotificationHub.Alerts.Count == 0)
        {
            return;
        }

        var confirmed = await _services.Dialog.ConfirmAsync(
            "Clear all notifications?",
            "This removes every alert from the notification panel and resets unread sidebar badges.",
            "Clear all");

        if (confirmed)
        {
            _services.NotificationHub.ClearAlerts();
        }
    }

    private void CommandPaletteOverlay_ItemSelected(object? sender, CommandPaletteSelection selection)
    {
        switch (selection.Action)
        {
            case CommandPaletteAction.OpenDashboard:
                _ = ShowDashboardAsync();
                break;
            case CommandPaletteAction.OpenSettings:
                _ = ShowSettingsAsync();
                break;
            case CommandPaletteAction.ToggleNotifications:
                SetNotificationPanelVisible(!_notificationPanelVisible);
                break;
            case CommandPaletteAction.MarkAllRead:
                _services.NotificationHub.MarkAllAlertsRead();
                break;
            case CommandPaletteAction.ClearNotifications:
                _ = ConfirmClearNotificationsAsync();
                break;
            case CommandPaletteAction.OpenInstance:
                if (!string.IsNullOrWhiteSpace(selection.InstanceId))
                {
                    _ = SelectInstanceAsync(selection.InstanceId);
                }

                break;
            case CommandPaletteAction.OpenAlert:
                if (!string.IsNullOrWhiteSpace(selection.AlertId))
                {
                    _services.NotificationHub.MarkAlertRead(selection.AlertId);
                }

                if (!string.IsNullOrWhiteSpace(selection.InstanceId))
                {
                    _ = SelectInstanceAsync(selection.InstanceId);
                    if (!_notificationPanelVisible)
                    {
                        ShowNotificationPanel();
                    }
                }

                break;
        }
    }

    private void OnArchivedInstanceRestoreRequested(object? sender, string instanceId)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var restored = await _services.Registry.RestoreArchivedInstanceAsync(instanceId);
                RebuildInstanceNavigation();
                RefreshDashboardIfVisible();

                if (ContentFrame.Content is SettingsPage settingsPage)
                {
                    settingsPage.RefreshAll();
                }

                await SelectInstanceAsync(restored.Id);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Could not restore account", ex.Message);
            }
        });
    }

    private void RefreshDashboardIfVisible()
    {
        if (ContentFrame.Content is DashboardPage dashboard)
        {
            dashboard.RefreshAll();
        }
    }

    private async Task InitializeAsync()
    {
        await AppSettingsService.Instance.LoadAsync().ConfigureAwait(true);
        await _services.Registry.LoadAsync().ConfigureAwait(true);
        await MessageAnalyticsService.Instance.LoadAsync().ConfigureAwait(true);
        var loadResult = await RichTriageStoreService.Instance.LoadAsync().ConfigureAwait(true);
        if (loadResult.Status == RichTriageStoreLoadStatus.CorruptRecovered &&
            !string.IsNullOrWhiteSpace(loadResult.UserMessage))
        {
            await ShowErrorDialogAsync("Triage data recovered", loadResult.UserMessage);
        }

        _panePinned = AppSettingsService.Instance.Settings.SidebarPinnedExpanded;
        UpdatePanePinUi();
        ApplySidebarLayout(forceVisible: true);
        ApplyNotificationPanelDockLayout();

        RebuildInstanceNavigation();
        RefreshNotificationUi();

        await WarmUpWebViewEnvironmentAsync().ConfigureAwait(true);

        var instances = _services.Registry.Instances.ToList();
        if (instances.Count > 0)
        {
            _trackingStartupWarm = true;
            _startupWarmCompleted = 0;
            _shellViewModel.BeginStartupWarm(instances.Count);
            ApplyInstanceLoadingUi();
            try
            {
                await _services.SessionManager.WarmAllSessionsAsync(instances, visibleInstanceId: null)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Could not start instances", FormatExceptionMessage(ex));
            }
            finally
            {
                _trackingStartupWarm = false;
                _shellViewModel.ResetStartupWarmProgress();
                ApplyInstanceLoadingUi();
            }
        }

        await ShowDashboardAsync().ConfigureAwait(true);
        _ = MaybePromptPinToTaskbarAsync();

        SystemTrayService.Instance.Attach(this);
        GlobalHotkeyService.Instance.EnsureRegistered();
        GlobalHotkeyService.Instance.CtrlSpacePressed += OnGlobalCopilotHotkey;

        GitHubUpdateService.Instance.PromptForUpdateApplicationAsync = PromptForAutoUpdateAsync;
    }

    private async Task<bool> PromptForAutoUpdateAsync(UpdateCheckResult result, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var current = result.CurrentVersion?.ToString() ?? "unknown";
        var latest = result.LatestVersion?.ToString() ?? "unknown";

        var dialog = new ContentDialog
        {
            Title = "Install update?",
            Content =
                $"A newer version ({latest}) is available. You are running {current}.\n\n" +
                "Unified Messenger will download the installer, verify its signature, and restart to apply the update.",
            PrimaryButtonText = "Install now",
            CloseButtonText = "Not now",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        var dialogResult = await dialog.ShowAsync();
        return dialogResult == ContentDialogResult.Primary;
    }

    private void OnGlobalCopilotHotkey(object? sender, EventArgs e)
    {
        _ = HotkeyCopilotOrchestrator.Instance.TryRunCopilotAsync();
    }

    private async Task MaybePromptPinToTaskbarAsync()
    {
        var settings = AppSettingsService.Instance.Settings;
        if (!settings.PromptPinToTaskbar || settings.HasPromptedPinToTaskbar)
        {
            return;
        }

        var taskbarManager = TaskbarManager.GetDefault();
        if (!taskbarManager.IsPinningAllowed)
        {
            return;
        }

        if (await taskbarManager.IsCurrentAppPinnedAsync())
        {
            await AppSettingsService.Instance.UpdateAsync(s => s.HasPromptedPinToTaskbar = true);
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Pin Unified Messenger?",
            Content = "Pin this app to your taskbar for quick access to all your messaging accounts.",
            PrimaryButtonText = "Pin to taskbar",
            CloseButtonText = "Not now",
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        await AppSettingsService.Instance.UpdateAsync(s => s.HasPromptedPinToTaskbar = true);

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await taskbarManager.RequestPinCurrentAppAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Taskbar pin request failed: {ex.Message}");
            await ShowErrorDialogAsync(
                "Could not pin to taskbar",
                "Right-click the taskbar icon and choose Pin to taskbar.");
        }
    }

    private void OnAdapterStaleDetected(object? sender, string instanceId)
    {
        if (!_adapterHealth.TryBeginRecovery(instanceId))
        {
            return;
        }

        DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await _services.SessionManager.RecoverStaleAdapterAsync(instanceId);
                RefreshAdapterHealthIndicators();
            }
            finally
            {
                _adapterHealth.EndRecovery(instanceId);
            }
        });
    }

    private async void OnInstanceReorderRequested(object? sender, (string SourceInstanceId, string TargetInstanceId) args)
    {
        try
        {
            await _services.Registry.ReorderInstanceBeforeAsync(args.SourceInstanceId, args.TargetInstanceId);
            RebuildInstanceNavigation();
            RestoreSidebarSelection();
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Could not reorder instance", ex.Message);
        }
    }

    private void OnShellInstanceNavigationRequested(object? sender, InstanceNavigationRequest request)
    {
        DispatcherQueue.TryEnqueue(() => _ = NavigateToInstanceAsync(request));
    }

    private async Task NavigateToInstanceAsync(InstanceNavigationRequest request)
    {
        await SelectInstanceAsync(request.InstanceId).ConfigureAwait(true);

        if (!request.HasConversationTarget)
        {
            return;
        }

        await Task.Delay(900).ConfigureAwait(true);

        var instance = _services.Registry.FindById(request.InstanceId);
        if (instance is null)
        {
            return;
        }

        var focused = await TryFocusConversationAsync(
                instance,
                request.ConversationKey,
                request.CustomerName)
            .ConfigureAwait(true);

        if (!focused)
        {
            DispatcherQueue.TryEnqueue(() =>
                _ = ShowErrorDialogAsync(
                    "Could not open conversation",
                    "The account opened, but Unified Messenger could not focus the requested chat. Open it manually in the inbox."));
        }
    }

    private async Task<bool> TryFocusConversationAsync(
        MessengerInstance instance,
        string? conversationKey,
        string? customerName)
    {
        try
        {
            var script = WebViewScriptBuilder.BuildFunctionCall(
                "__umFocusConversation",
                [
                    PlatformDefinition.NormalizePlatformId(instance.Platform),
                    conversationKey ?? string.Empty,
                    customerName ?? string.Empty
                ]);

            var rawResult = await _services.SessionManager
                .TryExecuteScriptOnInstanceAsync(instance.Id, script)
                .ConfigureAwait(true);

            return ParseScriptBoolean(rawResult);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Conversation focus failed: {ex.Message}");
            return false;
        }
    }

    private static bool ParseScriptBoolean(string? scriptResult)
    {
        if (string.IsNullOrWhiteSpace(scriptResult))
        {
            return false;
        }

        var normalized = scriptResult.Trim().Trim('"');
        return normalized.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ShowDashboardAsync()
    {
        _isDashboardSelected = true;
        _isSettingsSelected = false;
        _selectedInstanceId = null;
        WorkspaceSidebar.SetSelection(true, null);

        await _services.SessionManager.HideVisibleSessionAsync();
        InstanceWebViewHost.Visibility = Visibility.Collapsed;
        SetInstanceLoading(false, null);
        ContentFrame.Visibility = Visibility.Visible;

        var navArgs = PageServices.CreateRegistryArgs(_services);
        ContentFrame.Navigate(typeof(DashboardPage), navArgs);
        ActiveWorkspaceContext.SetDashboardVisible();
        AppTitleBar.Subtitle = "Dashboard";
    }

    private async Task WarmUpWebViewEnvironmentAsync()
    {
        try
        {
            await WebViewProfileManager.Instance.EnsureEnvironmentAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView environment warmup failed: {ex}");
        }
    }

    private double GetSidebarTargetWidth() =>
        MainWindowShellLayout.ResolveSidebarWidth(_panePinned, _sidebarHoverExpanded);

    private void ApplySidebarLayout(bool forceVisible = false)
    {
        if (MainWindowShellLayout.ShouldUseCompactSidebarDisplay(SidebarColumn.Width.Value, forceVisible))
        {
            WorkspaceSidebar.SetCompactDisplay(true);
            return;
        }

        var width = GetSidebarTargetWidth();
        SidebarColumn.Width = new GridLength(width);
        WorkspaceSidebar.SetCompactDisplay(MainWindowShellLayout.IsCompactSidebarWidth(width));
    }

    private void UpdatePanePinUi()
    {
        if (_panePinned)
        {
            PanePinIcon.Glyph = "\uE840";
            ToolTipService.SetToolTip(PanePinButton, "Unpin sidebar (compact rail with hover expand)");
        }
        else
        {
            PanePinIcon.Glyph = "\uE718";
            ToolTipService.SetToolTip(PanePinButton, "Pin sidebar expanded");
        }
    }

    private bool ShouldAutoOpenNotificationPanel() =>
        MainWindowShellLayout.ShouldAutoOpenNotificationPanel(
            AppSettingsService.Instance.Settings.PanelAutoOpen,
            IsAppInForeground);

    private void RebuildInstanceNavigation()
    {
        _services.NotificationHub.SyncMutedInstances(_services.Registry.Instances);

        WorkspaceSidebar.Refresh(
            _services.Registry.Instances,
            _selectedInstanceId,
            _isDashboardSelected);

        foreach (var instance in _services.Registry.Instances)
        {
            UpdateInstanceBadge(instance.Id);
            WorkspaceSidebar.UpdateInstanceHealth(instance.Id, instance);
        }

        ApplySidebarLayout(forceVisible: SidebarColumn.Width.Value > 0);
    }

    private void OnInstanceContextRequested(
        object? sender,
        (string InstanceId, MessengerInstance Instance, FrameworkElement Anchor) args)
    {
        var flyout = new MenuFlyout();
        var moveItem = new MenuFlyoutItem
        {
            Text = args.Instance.IsProfessional
                ? "Move to Personal workspace"
                : "Move to Professional workspace"
        };
        moveItem.Click += (_, _) => _ = ToggleInstanceCategoryAsync(args.InstanceId);
        flyout.Items.Add(moveItem);

        var renameItem = new MenuFlyoutItem { Text = "Rename instance..." };
        renameItem.Click += (_, _) => _ = RenameInstanceAsync(args.InstanceId);
        flyout.Items.Add(renameItem);

        var muteItem = new MenuFlyoutItem
        {
            Text = args.Instance.NotificationsMuted
                ? "Unmute notifications"
                : "Mute notifications"
        };
        muteItem.Click += (_, _) => _ = ToggleInstanceMuteAsync(args.InstanceId);
        flyout.Items.Add(muteItem);

        var refreshItem = new MenuFlyoutItem { Text = "Refresh WebView" };
        refreshItem.Click += (_, _) => _ = _services.SessionManager.ReloadSessionAsync(args.InstanceId);
        flyout.Items.Add(refreshItem);

        var memoryFlyout = new MenuFlyoutSubItem { Text = "Memory tier" };
        foreach (var tier in Enum.GetValues<MemoryTierPreference>())
        {
            var tierItem = new MenuFlyoutItem
            {
                Text = tier.ToString(),
                Tag = tier
            };
            tierItem.Click += async (_, _) =>
            {
                if (tierItem.Tag is MemoryTierPreference selectedTier)
                {
                    await _services.Registry.UpdateInstanceMemoryTierAsync(args.InstanceId, selectedTier);
                    RebuildInstanceNavigation();
                }
            };
            memoryFlyout.Items.Add(tierItem);
        }

        flyout.Items.Add(memoryFlyout);

        if (AppSettingsService.Instance.Settings.EnableEditInstanceMetadata)
        {
            var editItem = new MenuFlyoutItem { Text = "Edit instance metadata..." };
            editItem.Click += (_, _) => _ = EditInstanceMetadataAsync(args.InstanceId);
            flyout.Items.Add(editItem);
        }

        var moveUpItem = new MenuFlyoutItem { Text = "Move up" };
        moveUpItem.Click += (_, _) => _ = MoveInstanceAsync(args.InstanceId, -1);
        flyout.Items.Add(moveUpItem);

        var moveDownItem = new MenuFlyoutItem { Text = "Move down" };
        moveDownItem.Click += (_, _) => _ = MoveInstanceAsync(args.InstanceId, 1);
        flyout.Items.Add(moveDownItem);

        var removeItem = new MenuFlyoutItem { Text = "Remove instance..." };
        removeItem.Click += (_, _) => _ = DeleteInstanceAsync(args.InstanceId);
        flyout.Items.Add(removeItem);

        flyout.ShowAt(args.Anchor);
    }

    private void OnAdapterHealthChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshAdapterHealthIndicators);
    }

    private void OnConnectionStatusChanged(object? sender, InstanceConnectionStatusChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var instance = _services.Registry.FindById(e.InstanceId);
            if (instance is not null)
            {
                instance.Status = e.Status;
                WorkspaceSidebar.UpdateInstanceHealth(e.InstanceId, instance);
                RefreshDashboardIfVisible();
                return;
            }

            RefreshAdapterHealthIndicators();
        });
    }

    private void OnSessionInitializing(object? sender, InstanceSessionEventArgs e)
    {
        InstanceConnectionStatusService.Instance.SetInitializing(e.Instance.Id, "Starting session");
        e.Instance.Status = InstanceConnectionStatus.Initializing;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_trackingStartupWarm)
            {
                _startupWarmCompleted++;
                _shellViewModel.ReportStartupWarmProgress(
                    _startupWarmCompleted,
                    _shellViewModel.StartupWarmTotal,
                    e.Instance.DisplayName);
                ApplyInstanceLoadingUi();
            }

            WorkspaceSidebar.UpdateInstanceHealth(e.Instance.Id, e.Instance);
            RefreshDashboardIfVisible();
        });
    }

    private void OnSessionFailed(object? sender, InstanceSessionErrorEventArgs e)
    {
        InstanceConnectionStatusService.Instance.SetError(e.Instance.Id, e.Error.Message);
        e.Instance.Status = InstanceConnectionStatus.Error;
        DispatcherQueue.TryEnqueue(() =>
        {
            WorkspaceSidebar.UpdateInstanceHealth(e.Instance.Id, e.Instance);
            RefreshDashboardIfVisible();
        });
    }

    private void RefreshAdapterHealthIndicators()
    {
        foreach (var instance in _services.Registry.Instances)
        {
            InstanceConnectionStatusService.Instance.MirrorStatusToInstance(instance);
            WorkspaceSidebar.UpdateInstanceHealth(instance.Id, instance);
        }

        RefreshDashboardIfVisible();
    }

    private async Task ToggleInstanceCategoryAsync(string instanceId)
    {
        var instance = _services.Registry.FindById(instanceId);
        if (instance is null)
        {
            return;
        }

        var newCategory = instance.IsProfessional
            ? WorkspaceCategory.Personal
            : WorkspaceCategory.Professional;

        try
        {
            await _services.Registry.UpdateInstanceCategoryAsync(instanceId, newCategory);
            RebuildInstanceNavigation();
            RefreshDashboardIfVisible();
            RestoreSidebarSelection();
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Could not update workspace", ex.Message);
        }
    }

    private async Task RenameInstanceAsync(string instanceId)
    {
        var instance = _services.Registry.FindById(instanceId);
        if (instance is null)
        {
            return;
        }

        var dialog = new RenameInstanceDialog(instance.DisplayName)
        {
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary || dialog.ResultDisplayName is null)
        {
            return;
        }

        try
        {
            await _services.Registry.UpdateInstanceDisplayNameAsync(instanceId, dialog.ResultDisplayName);
            RebuildInstanceNavigation();
            RefreshDashboardIfVisible();
            RestoreSidebarSelection();
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Could not rename instance", ex.Message);
        }
    }

    private async Task EditInstanceMetadataAsync(string instanceId)
    {
        var instance = _services.Registry.FindById(instanceId);
        if (instance is null)
        {
            return;
        }

        var dialog = new EditInstanceMetadataDialog(instance)
        {
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary ||
            dialog.ResultDisplayName is null ||
            dialog.ResultPlatformId is null ||
            dialog.ResultStartUrl is null)
        {
            return;
        }

        if (!dialog.ResultPlatformId.Equals(instance.Platform, StringComparison.OrdinalIgnoreCase))
        {
            var confirm = new ContentDialog
            {
                Title = "Change platform?",
                Content =
                    $"Switching from {PlatformDefinition.FindById(instance.Platform)?.DisplayName ?? instance.Platform} " +
                    $"to {PlatformDefinition.FindById(dialog.ResultPlatformId)?.DisplayName ?? dialog.ResultPlatformId} " +
                    "may require signing in again in the embedded web app.",
                PrimaryButtonText = "Change platform",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        try
        {
            await _services.Registry.UpdateInstanceMetadataAsync(
                instanceId,
                dialog.ResultDisplayName,
                dialog.ResultStartUrl,
                dialog.ResultPlatformId,
                dialog.ResultNotes,
                dialog.ResultBranchKey);

            await _services.SessionManager.ReloadSessionAsync(instanceId);
            RebuildInstanceNavigation();
            RefreshDashboardIfVisible();
            RestoreSidebarSelection();
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Could not update instance metadata", ex.Message);
        }
    }

    private async Task ToggleInstanceMuteAsync(string instanceId)
    {
        var instance = _services.Registry.FindById(instanceId);
        if (instance is null)
        {
            return;
        }

        try
        {
            var muted = !instance.NotificationsMuted;
            await _services.Registry.UpdateInstanceNotificationsMutedAsync(instanceId, muted);
            if (muted)
            {
                _services.NotificationHub.UpdateBadgeCount(instanceId, 0);
            }

            RebuildInstanceNavigation();
            RefreshNotificationUi();
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Could not update notification mute", ex.Message);
        }
    }

    private async Task MoveInstanceAsync(string instanceId, int direction)
    {
        try
        {
            await _services.Registry.MoveInstanceAsync(instanceId, direction);
            RebuildInstanceNavigation();
            RestoreSidebarSelection();
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Could not reorder instance", ex.Message);
        }
    }

    private void UpdateInstanceBadge(string instanceId)
    {
        var instance = _services.Registry.FindById(instanceId);
        var count = instance?.NotificationsMuted == true
            ? 0
            : _services.NotificationHub.GetBadgeCount(instanceId);
        WorkspaceSidebar.UpdateInstanceBadge(instanceId, count, instance);
    }

    private void RefreshNotificationUi()
    {
        foreach (var instance in _services.Registry.Instances)
        {
            UpdateInstanceBadge(instance.Id);
        }

        WorkspaceSidebar.UpdateNotificationHubBadge(_services.NotificationHub.TotalUnreadCount);
        NotificationPanel.Refresh(_services.NotificationHub, _services.Registry.Instances);
        _ = TaskbarBadgeService.Instance.SyncBadgeAsync(_services.NotificationHub.TotalUnreadCount);

        if (ContentFrame.Content is DashboardPage dashboard)
        {
            dashboard.RefreshAll();
        }
    }

    private void OnNotificationHubChanged(object? sender, NotificationHubChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            RefreshNotificationUi();

            if (e.Kind != NotificationChangeKind.AlertAdded || e.Alert is null)
            {
                return;
            }

            if (IsAppInForeground)
            {
                if (!_notificationPanelVisible && ShouldAutoOpenNotificationPanel())
                {
                    ShowNotificationPanel();
                }
            }
            else
            {
                _pendingPanelReveal = true;
                if (AppSettingsService.Instance.Settings.EnableBackgroundToasts)
                {
                    var instance = _services.Registry.FindById(e.Alert.InstanceId);
                    AppNotificationService.Instance.ShowAlertToast(e.Alert, instance);
                }
            }
        });
    }

    private void OnToastActivationRequested(object? sender, ToastActivationEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            Activate();
            AppWindow.Show();

            NotificationAlert? alert = null;
            if (!string.IsNullOrWhiteSpace(e.AlertId))
            {
                _services.NotificationHub.MarkAlertRead(e.AlertId);
                alert = _services.NotificationHub.Alerts
                    .FirstOrDefault(item => item.Id.Equals(e.AlertId, StringComparison.OrdinalIgnoreCase));
            }

            if (_services.Registry.FindById(e.InstanceId) is not null)
            {
                NotificationNavigationHelper.OpenToastActivation(_services.Navigation, e, alert);
            }

            if (!_notificationPanelVisible)
            {
                ShowNotificationPanel();
            }

            _pendingPanelReveal = false;
        });
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        if (SidebarColumn.Width.Value > 0)
        {
            SidebarColumn.Width = new GridLength(0);
        }
        else
        {
            _sidebarHoverExpanded = _panePinned;
            ApplySidebarLayout(forceVisible: true);
        }
    }

    private async void PanePinButton_Click(object sender, RoutedEventArgs e)
    {
        _panePinned = !_panePinned;
        _sidebarHoverExpanded = _panePinned;

        UpdatePanePinUi();
        ApplySidebarLayout(forceVisible: true);

        try
        {
            await AppSettingsService.Instance.UpdateAsync(settings =>
                settings.SidebarPinnedExpanded = _panePinned);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not persist sidebar pin: {ex.Message}");
        }
    }

    private void SidebarHost_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_panePinned || SidebarColumn.Width.Value <= 0)
        {
            return;
        }

        _sidebarHoverExpanded = true;
        ApplySidebarLayout(forceVisible: true);
    }

    private void SidebarHost_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_panePinned || SidebarColumn.Width.Value <= 0)
        {
            return;
        }

        _sidebarHoverExpanded = false;
        ApplySidebarLayout(forceVisible: true);
    }

    private async Task ShowSettingsAsync()
    {
        _isDashboardSelected = false;
        _isSettingsSelected = true;
        _selectedInstanceId = null;
        WorkspaceSidebar.SetSelection(false, null, settingsSelected: true);

        await _services.SessionManager.HideVisibleSessionAsync();

        InstanceWebViewHost.Visibility = Visibility.Collapsed;
        SetInstanceLoading(false, null);
        ContentFrame.Visibility = Visibility.Visible;

        var navArgs = PageServices.CreateRegistryArgs(_services);
        ContentFrame.Navigate(typeof(SettingsPage), navArgs);
        ActiveWorkspaceContext.SetSettingsVisible();
        AppTitleBar.Subtitle = "Settings";
    }

    private void OnAutoDraftCompleted(object? sender, AutoDraftCompletedEventArgs e)
    {
        if (!ActiveWorkspaceContext.IsInstanceActive(e.InstanceId))
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            AppTitleBar.Subtitle = "AI draft ready — review before sending";
            _ = ClearAutoDraftSubtitleAsync();
        });
    }

    private async Task ClearAutoDraftSubtitleAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(8)).ConfigureAwait(true);
        DispatcherQueue.TryEnqueue(() =>
        {
            if (AppTitleBar.Subtitle == "AI draft ready — review before sending" &&
                !string.IsNullOrWhiteSpace(_selectedInstanceId) &&
                _services.Registry.FindById(_selectedInstanceId) is { } instance)
            {
                AppTitleBar.Subtitle = instance.DisplayName;
            }
        });
    }

    private void NotificationToggleButton_Click(object sender, RoutedEventArgs e)
    {
        SetNotificationPanelVisible(!_notificationPanelVisible);
    }

    private void NotificationPanel_CollapseRequested(object? sender, EventArgs e)
    {
        SetNotificationPanelVisible(false);
    }

    private void NotificationPanel_AlertClicked(object? sender, NotificationAlert alert) =>
        NotificationNavigationHelper.OpenAlert(_services.Navigation, alert);

    public void ShowNotificationPanel()
    {
        SetNotificationPanelVisible(true);
    }

    private void SetNotificationPanelVisible(bool isVisible)
    {
        _notificationPanelVisible = isVisible;
        ApplyNotificationPanelVisibilityMetrics(isVisible);
        NotificationPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyNotificationPanelDockLayout()
    {
        var dock = AppSettingsService.Instance.Settings.PanelDock;
        if (dock == NotificationPanelDock.Bottom)
        {
            Grid.SetColumn(NotificationPanel, 1);
            Grid.SetRow(NotificationPanel, 1);
            Grid.SetColumnSpan(NotificationPanel, 1);
            Grid.SetRowSpan(NotificationPanel, 1);
        }
        else
        {
            Grid.SetColumn(NotificationPanel, 2);
            Grid.SetRow(NotificationPanel, 0);
            Grid.SetColumnSpan(NotificationPanel, 1);
            Grid.SetRowSpan(NotificationPanel, 2);
        }

        ApplyNotificationPanelVisibilityMetrics(_notificationPanelVisible);
    }

    private void ApplyNotificationPanelVisibilityMetrics(bool isVisible)
    {
        var metrics = MainWindowShellLayout.ResolveNotificationPanelMetrics(
            AppSettingsService.Instance.Settings.PanelDock,
            isVisible);
        NotificationColumn.Width = metrics.ColumnWidth;
        NotificationRow.Height = metrics.RowHeight;
    }

    private async Task ShowAddInstanceDialogAsync()
    {
        var dialog = new AddInstanceDialog(_services.Registry.ArchivedInstances)
        {
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(dialog.ResultRestoreInstanceId))
            {
                var restored = await _services.Registry.RestoreArchivedInstanceAsync(dialog.ResultRestoreInstanceId);
                RebuildInstanceNavigation();
                RefreshDashboardIfVisible();
                await SelectInstanceAsync(restored.Id);
                return;
            }

            if (dialog.ResultDisplayName is null || dialog.ResultPlatformId is null)
            {
                return;
            }

            var instance = await _services.Registry.AddInstanceAsync(
                dialog.ResultDisplayName,
                dialog.ResultPlatformId,
                dialog.ResultCustomUrl,
                dialog.ResultCategory);

            RebuildInstanceNavigation();
            RefreshDashboardIfVisible();
            await SelectInstanceAsync(instance.Id);
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Could not add instance", ex.Message);
        }
    }

    private async Task DeleteInstanceAsync(string instanceId)
    {
        var instance = _services.Registry.FindById(instanceId);
        if (instance is null)
        {
            return;
        }

        var dialog = new DeleteInstanceDialog(instance.DisplayName)
        {
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();
        if (dialog.Choice == DeleteInstanceChoice.Cancelled)
        {
            return;
        }

        try
        {
            if (_selectedInstanceId == instanceId)
            {
                await _services.SessionManager.HideVisibleSessionAsync();
                _selectedInstanceId = null;
            }
            else
            {
                await _services.SessionManager.CloseSessionAsync(instanceId);
            }

            if (dialog.Choice == DeleteInstanceChoice.RemoveFromSidebar)
            {
                await _services.Registry.RemoveFromSidebarAsync(instanceId);
            }
            else
            {
                var webView = _services.SessionManager.TryGetWebView(instanceId)
                    ?? InstanceWebViewRegistry.Instance.TryGet(instanceId);
                await WebViewProfileManager.Instance.PermanentlyDeleteProfileAsync(
                    instance.ProfileName,
                    webView);
                await _services.Registry.RemovePermanentlyAsync(instanceId);
            }

            _services.NotificationHub.RemoveAlertsForInstance(instanceId);
            _adapterHealth.RemoveInstance(instanceId);
            ProfessionalWorkspaceService.Instance.RemoveInstance(instanceId);
            RebuildInstanceNavigation();
            RefreshNotificationUi();

            var nextInstance = _services.Registry.Instances.FirstOrDefault();
            if (nextInstance is not null)
            {
                await SelectInstanceAsync(nextInstance.Id);
            }
            else
            {
                await ShowDashboardAsync();
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Could not remove instance", ex.Message);
        }
    }

    private static string FormatExceptionMessage(Exception ex)
    {
        if (ex is AggregateException aggregate)
        {
            var parts = aggregate.Flatten().InnerExceptions
                .Select(static inner => inner.Message?.Trim())
                .Where(static part => !string.IsNullOrWhiteSpace(part))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (parts.Count > 0)
            {
                return string.Join(Environment.NewLine, parts);
            }
        }

        var message = ex.Message?.Trim();
        if (ex.InnerException is { } inner)
        {
            var innerMessage = inner.Message?.Trim();
            if (!string.IsNullOrWhiteSpace(innerMessage) &&
                !string.Equals(message, innerMessage, StringComparison.Ordinal))
            {
                return $"{message}{Environment.NewLine}{innerMessage}";
            }
        }

        return string.IsNullOrWhiteSpace(message) ? ex.GetType().Name : message;
    }

    private Task ShowErrorDialogAsync(string title, string message) =>
        _services.Dialog.ShowErrorAsync(title, message);

    private void RestoreSidebarSelection()
    {
        WorkspaceSidebar.SetSelection(_isDashboardSelected, _selectedInstanceId, _isSettingsSelected);
    }

    private async Task SelectInstanceAsync(string instanceId)
    {
        if (!ShellNavigationService.IsValidInstanceId(instanceId))
        {
            return;
        }

        var normalizedId = instanceId.Trim();
        WorkspaceSidebar.SetSelection(false, normalizedId);
        await NavigateToInstanceAsync(normalizedId);
    }

    private async Task NavigateToInstanceAsync(string instanceId)
    {
        var instance = _services.Registry.FindById(instanceId);
        if (instance is null)
        {
            return;
        }

        _selectedInstanceId = instanceId;
        _isDashboardSelected = false;
        _isSettingsSelected = false;
        ActiveWorkspaceContext.SetActiveInstance(instanceId);
        WorkspaceSidebar.SetSelection(false, instanceId);
        ContentFrame.Visibility = Visibility.Collapsed;
        InstanceWebViewHost.Visibility = Visibility.Visible;

        SetInstanceLoading(true, instance.DisplayName);
        try
        {
            await _services.SessionManager.SwitchToAsync(instance);
            AppTitleBar.Subtitle = instance.DisplayName;
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Could not load instance", FormatExceptionMessage(ex));
        }
        finally
        {
            SetInstanceLoading(false, null);
        }
    }

    private void SetInstanceLoading(bool isLoading, string? displayName)
    {
        _shellViewModel.SetInstanceLoading(
            isLoading,
            isLoading && !string.IsNullOrWhiteSpace(displayName)
                ? $"Loading {displayName}..."
                : null);
        ApplyInstanceLoadingUi();
    }

    private void ApplyInstanceLoadingUi()
    {
        var showLoading = _shellViewModel.IsInstanceLoading || _shellViewModel.ShowStartupWarmProgress;
        InstanceLoadingPanel.Visibility = showLoading ? Visibility.Visible : Visibility.Collapsed;

        if (_shellViewModel.ShowStartupWarmProgress)
        {
            StartupWarmProgressBar.Visibility = Visibility.Visible;
            StartupWarmProgressBar.Value = _shellViewModel.StartupWarmProgress;
            InstanceLoadingText.Text = _shellViewModel.StartupWarmStatusText;
            return;
        }

        StartupWarmProgressBar.Visibility = Visibility.Collapsed;
        InstanceLoadingText.Text = string.IsNullOrWhiteSpace(_shellViewModel.InstanceLoadingMessage)
            ? "Loading instance..."
            : _shellViewModel.InstanceLoadingMessage;
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        _isWindowActivated = args.WindowActivationState != WindowActivationState.Deactivated;
        ApplyWindowVisibilityState();
        OnForegroundStateChanged();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidVisibilityChange)
        {
            _isWindowVisible = sender.IsVisible;
            ApplyWindowVisibilityState();
            OnForegroundStateChanged();
        }
    }

    private void OnForegroundStateChanged()
    {
        if (!IsAppInForeground || !_pendingPanelReveal || _notificationPanelVisible)
        {
            return;
        }

        ShowNotificationPanel();
        _pendingPanelReveal = false;
    }

    private void ApplyWindowVisibilityState()
    {
        _services.SessionManager.ApplyAppWindowState(IsAppInForeground);
    }
}
