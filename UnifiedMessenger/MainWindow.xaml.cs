using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Controls;
using UnifiedMessenger.Dialogs;
using UnifiedMessenger.Models;
using UnifiedMessenger.Pages;
using UnifiedMessenger.Services;
using Windows.System;

namespace UnifiedMessenger;

public sealed partial class MainWindow : Window
{
    private const double SidebarWidthExpanded = 320;
    private const double SidebarWidthCompact = 56;

    private readonly InstanceRegistryService _registry = new();
    private readonly InstanceSessionManager _sessionManager = InstanceSessionManager.Instance;
    private readonly NotificationHub _notificationHub = NotificationHub.Instance;
    private readonly AdapterHealthMonitor _adapterHealth = AdapterHealthMonitor.Instance;
    private readonly KeyboardShortcutService _keyboardShortcuts;
    private bool _notificationPanelVisible;
    private bool _panePinned;
    private bool _sidebarHoverExpanded;
    private bool _isDashboardSelected = true;
    private string? _selectedInstanceId;
    private bool _isWindowActivated = true;
    private bool _isWindowVisible = true;
    private bool _pendingPanelReveal;

    private bool IsAppInForeground => _isWindowVisible && _isWindowActivated;

    public MainWindow()
    {
        InitializeComponent();

        _keyboardShortcuts = new KeyboardShortcutService((UIElement)Content);
        RegisterKeyboardShortcuts();

        _sessionManager.AttachHost(InstanceWebViewHost);

        WorkspaceSidebar.DashboardRequested += (_, _) => _ = ShowDashboardAsync();
        WorkspaceSidebar.InstanceRequested += (_, instanceId) => _ = SelectInstanceAsync(instanceId);
        WorkspaceSidebar.AddInstanceRequested += (_, _) => _ = ShowAddInstanceDialogAsync();
        WorkspaceSidebar.NotificationsRequested += (_, _) =>
            SetNotificationPanelVisible(!_notificationPanelVisible);
        WorkspaceSidebar.SettingsRequested += (_, _) => _ = ShowSettingsAsync();
        WorkspaceSidebar.InstanceContextRequested += OnInstanceContextRequested;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        AppWindow.Changed += OnAppWindowChanged;
        Activated += OnWindowActivated;

        NotificationPanel.CollapseRequested += NotificationPanel_CollapseRequested;
        NotificationPanel.AlertClicked += NotificationPanel_AlertClicked;
        _notificationHub.Changed += OnNotificationHubChanged;
        AppNotificationService.Instance.InstanceActivationRequested += OnToastActivationRequested;
        _adapterHealth.Changed += OnAdapterHealthChanged;
        ShellNavigationService.Instance.InstanceLaunchRequested += OnShellInstanceLaunchRequested;
        ShellNavigationService.Instance.DashboardRefreshRequested += OnShellDashboardRefreshRequested;
        ShellNavigationService.Instance.ArchivedInstanceRestoreRequested += OnArchivedInstanceRestoreRequested;
        MessageAnalyticsService.Instance.Changed += OnAnalyticsChanged;
        AppSettingsService.Instance.Changed += OnAppSettingsChanged;

        WorkspaceSidebar.Loaded += (_, _) => RebuildInstanceNavigation();

        _ = InitializeAsync();
    }

    private void OnAnalyticsChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshDashboardIfVisible);
    }

    private void OnShellDashboardRefreshRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshDashboardIfVisible);
    }

    private void OnAppSettingsChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ThemeService.Apply(AppSettingsService.Instance.Settings.ThemePreference);
            _ = TaskbarBadgeService.Instance.SyncBadgeAsync(_notificationHub.TotalUnreadCount);
            _ = _sessionManager.BroadcastAdapterSettingsAsync();
        });
    }

    private void RegisterKeyboardShortcuts()
    {
        _keyboardShortcuts.Register(VirtualKey.D, VirtualKeyModifiers.Control, () => _ = ShowDashboardAsync());
        _keyboardShortcuts.Register(VirtualKey.K, VirtualKeyModifiers.Control, OpenCommandPalette);
        _keyboardShortcuts.Register((VirtualKey)188, VirtualKeyModifiers.Control, () => _ = ShowSettingsAsync());
        _keyboardShortcuts.Register(
            VirtualKey.N,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            () => SetNotificationPanelVisible(!_notificationPanelVisible));

        for (var i = 0; i < 9; i++)
        {
            var index = i;
            _keyboardShortcuts.Register(
                (VirtualKey)((int)VirtualKey.Number1 + index),
                VirtualKeyModifiers.Control,
                () => ActivateInstanceByIndex(index));
        }
    }

    private void ActivateInstanceByIndex(int index)
    {
        var instances = GetSidebarOrderedInstances();
        if (index >= 0 && index < instances.Count)
        {
            _ = SelectInstanceAsync(instances[index].Id);
        }
    }

    private IReadOnlyList<MessengerInstance> GetSidebarOrderedInstances()
    {
        return _registry.GetOrderedInstances().ToList();
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

        foreach (var instance in _registry.GetOrderedInstances())
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

        foreach (var alert in _notificationHub.GetAlertsSortedByInstance().Take(20))
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
                _notificationHub.MarkAllAlertsRead();
                break;
            case CommandPaletteAction.ClearNotifications:
                _notificationHub.ClearAlerts();
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
                    _notificationHub.MarkAlertRead(selection.AlertId);
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
                var restored = await _registry.RestoreArchivedInstanceAsync(instanceId);
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
        await _registry.LoadAsync().ConfigureAwait(true);
        await MessageAnalyticsService.Instance.LoadAsync().ConfigureAwait(true);

        _panePinned = AppSettingsService.Instance.Settings.SidebarPinnedExpanded;
        UpdatePanePinUi();
        ApplySidebarLayout(forceVisible: true);

        RebuildInstanceNavigation();
        RefreshNotificationUi();

        await WarmUpWebViewEnvironmentAsync();

        var instances = _registry.Instances;
        if (instances.Count > 0)
        {
            SetInstanceLoading(true, "Starting accounts...");
            try
            {
                await _sessionManager.WarmAllSessionsAsync(instances, visibleInstanceId: null);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Could not start instances", ex.Message);
            }
            finally
            {
                SetInstanceLoading(false, null);
            }
        }

        await ShowDashboardAsync();
    }

    private void OnShellInstanceLaunchRequested(object? sender, string instanceId)
    {
        DispatcherQueue.TryEnqueue(() => _ = SelectInstanceAsync(instanceId));
    }

    private async Task ShowDashboardAsync()
    {
        _isDashboardSelected = true;
        _selectedInstanceId = null;
        WorkspaceSidebar.SetSelection(true, null);

        await _sessionManager.HideVisibleSessionAsync();
        InstanceWebViewHost.Visibility = Visibility.Collapsed;
        SetInstanceLoading(false, null);
        ContentFrame.Visibility = Visibility.Visible;

        var navArgs = new DashboardNavigationArgs { Registry = _registry };
        ContentFrame.Navigate(typeof(DashboardPage), navArgs);
        AppTitleBar.Subtitle = "Dashboard";

        if (ContentFrame.Content is DashboardPage dashboard)
        {
            dashboard.RefreshAll();
        }
    }

    private async Task WarmUpWebViewEnvironmentAsync()
    {
        try
        {
            await WebViewProfileManager.Instance.EnsureEnvironmentAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView environment warmup failed: {ex}");
        }
    }

    private double GetSidebarTargetWidth()
    {
        if (_panePinned || _sidebarHoverExpanded)
        {
            return SidebarWidthExpanded;
        }

        return SidebarWidthCompact;
    }

    private void ApplySidebarLayout(bool forceVisible = false)
    {
        if (SidebarColumn.Width.Value <= 0 && !forceVisible)
        {
            WorkspaceSidebar.SetCompactDisplay(true);
            return;
        }

        var width = GetSidebarTargetWidth();
        SidebarColumn.Width = new GridLength(width);
        WorkspaceSidebar.SetCompactDisplay(width <= SidebarWidthCompact + 1);
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
        AppSettingsService.Instance.Settings.PanelAutoOpen switch
        {
            NotificationPanelAutoOpenMode.Always => true,
            NotificationPanelAutoOpenMode.Never => false,
            _ => !IsAppInForeground
        };

    private void RebuildInstanceNavigation()
    {
        _notificationHub.SyncMutedInstances(_registry.Instances);

        WorkspaceSidebar.Refresh(
            _registry.Instances,
            _selectedInstanceId,
            _isDashboardSelected);

        foreach (var instance in _registry.Instances)
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

    private void RefreshAdapterHealthIndicators()
    {
        foreach (var instance in _registry.Instances)
        {
            WorkspaceSidebar.UpdateInstanceHealth(instance.Id, instance);
        }

        RefreshDashboardIfVisible();
    }

    private async Task ToggleInstanceCategoryAsync(string instanceId)
    {
        var instance = _registry.FindById(instanceId);
        if (instance is null)
        {
            return;
        }

        var newCategory = instance.IsProfessional
            ? WorkspaceCategory.Personal
            : WorkspaceCategory.Professional;

        try
        {
            await _registry.UpdateInstanceCategoryAsync(instanceId, newCategory);
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
        var instance = _registry.FindById(instanceId);
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
            await _registry.UpdateInstanceDisplayNameAsync(instanceId, dialog.ResultDisplayName);
            RebuildInstanceNavigation();
            RefreshDashboardIfVisible();
            RestoreSidebarSelection();
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Could not rename instance", ex.Message);
        }
    }

    private async Task ToggleInstanceMuteAsync(string instanceId)
    {
        var instance = _registry.FindById(instanceId);
        if (instance is null)
        {
            return;
        }

        try
        {
            var muted = !instance.NotificationsMuted;
            await _registry.UpdateInstanceNotificationsMutedAsync(instanceId, muted);
            if (muted)
            {
                _notificationHub.UpdateBadgeCount(instanceId, 0);
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
            await _registry.MoveInstanceAsync(instanceId, direction);
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
        var instance = _registry.FindById(instanceId);
        var count = instance?.NotificationsMuted == true
            ? 0
            : _notificationHub.GetBadgeCount(instanceId);
        WorkspaceSidebar.UpdateInstanceBadge(instanceId, count, instance);
    }

    private void RefreshNotificationUi()
    {
        foreach (var instance in _registry.Instances)
        {
            UpdateInstanceBadge(instance.Id);
        }

        WorkspaceSidebar.UpdateNotificationHubBadge(_notificationHub.TotalUnreadCount);
        NotificationPanel.Refresh(_notificationHub, _registry.Instances);
        _ = TaskbarBadgeService.Instance.SyncBadgeAsync(_notificationHub.TotalUnreadCount);

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
                    var instance = _registry.FindById(e.Alert.InstanceId);
                    AppNotificationService.Instance.ShowAlertToast(e.Alert, instance);
                }
            }
        });
    }

    private void OnToastActivationRequested(object? sender, string instanceId)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            Activate();
            AppWindow.Show();

            if (_registry.FindById(instanceId) is not null)
            {
                await SelectInstanceAsync(instanceId);
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
        if (_panePinned)
        {
            return;
        }

        if (SidebarColumn.Width.Value > 0)
        {
            SidebarColumn.Width = new GridLength(0);
        }
        else
        {
            _sidebarHoverExpanded = false;
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
        _selectedInstanceId = null;
        WorkspaceSidebar.SetSelection(false, null);

        await _sessionManager.HideVisibleSessionAsync();

        InstanceWebViewHost.Visibility = Visibility.Collapsed;
        SetInstanceLoading(false, null);
        ContentFrame.Visibility = Visibility.Visible;

        var navArgs = new SettingsNavigationArgs { Registry = _registry };
        ContentFrame.Navigate(typeof(SettingsPage), navArgs);
        AppTitleBar.Subtitle = "Settings";
    }

    private void NotificationToggleButton_Click(object sender, RoutedEventArgs e)
    {
        SetNotificationPanelVisible(!_notificationPanelVisible);
    }

    private void NotificationPanel_CollapseRequested(object? sender, EventArgs e)
    {
        SetNotificationPanelVisible(false);
    }

    private void NotificationPanel_AlertClicked(object? sender, NotificationAlert alert)
    {
        SelectInstance(alert.InstanceId);
    }

    public void ShowNotificationPanel()
    {
        SetNotificationPanelVisible(true);
    }

    private void SetNotificationPanelVisible(bool isVisible)
    {
        _notificationPanelVisible = isVisible;
        NotificationColumn.Width = isVisible ? new GridLength(320) : new GridLength(0);
        NotificationPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task ShowAddInstanceDialogAsync()
    {
        var dialog = new AddInstanceDialog(_registry.ArchivedInstances)
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
                var restored = await _registry.RestoreArchivedInstanceAsync(dialog.ResultRestoreInstanceId);
                RebuildInstanceNavigation();
                RefreshDashboardIfVisible();
                await SelectInstanceAsync(restored.Id);
                return;
            }

            if (dialog.ResultDisplayName is null || dialog.ResultPlatformId is null)
            {
                return;
            }

            var instance = await _registry.AddInstanceAsync(
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
        var instance = _registry.FindById(instanceId);
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
                await _sessionManager.HideVisibleSessionAsync();
                _selectedInstanceId = null;
            }
            else
            {
                await _sessionManager.CloseSessionAsync(instanceId);
            }

            if (dialog.Choice == DeleteInstanceChoice.RemoveFromSidebar)
            {
                await _registry.RemoveFromSidebarAsync(instanceId);
            }
            else
            {
                var webView = _sessionManager.TryGetWebView(instanceId)
                    ?? InstanceWebViewRegistry.Instance.TryGet(instanceId);
                await WebViewProfileManager.Instance.PermanentlyDeleteProfileAsync(
                    instance.ProfileName,
                    webView);
                await _registry.RemovePermanentlyAsync(instanceId);
            }

            _notificationHub.RemoveAlertsForInstance(instanceId);
            _adapterHealth.RemoveInstance(instanceId);
            RebuildInstanceNavigation();
            RefreshNotificationUi();

            var nextInstance = _registry.Instances.FirstOrDefault();
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

    private async Task ShowErrorDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private void RestoreSidebarSelection()
    {
        WorkspaceSidebar.SetSelection(_isDashboardSelected, _selectedInstanceId);
    }

    private void SelectInstance(string instanceId)
    {
        _ = SelectInstanceAsync(instanceId);
    }

    private async Task SelectInstanceAsync(string instanceId)
    {
        WorkspaceSidebar.SetSelection(false, instanceId);
        await NavigateToInstanceAsync(instanceId);
    }

    private async Task NavigateToInstanceAsync(string instanceId)
    {
        var instance = _registry.FindById(instanceId);
        if (instance is null)
        {
            return;
        }

        _selectedInstanceId = instanceId;
        _isDashboardSelected = false;
        WorkspaceSidebar.SetSelection(false, instanceId);
        ContentFrame.Visibility = Visibility.Collapsed;
        InstanceWebViewHost.Visibility = Visibility.Visible;

        SetInstanceLoading(true, instance.DisplayName);
        try
        {
            await _sessionManager.SwitchToAsync(instance);
            AppTitleBar.Subtitle = instance.DisplayName;
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Could not load instance", ex.Message);
        }
        finally
        {
            SetInstanceLoading(false, null);
        }
    }

    private void SetInstanceLoading(bool isLoading, string? displayName)
    {
        InstanceLoadingPanel.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        if (isLoading && !string.IsNullOrWhiteSpace(displayName))
        {
            InstanceLoadingText.Text = $"Loading {displayName}...";
        }
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
        _sessionManager.ApplyAppWindowState(IsAppInForeground);
    }
}
