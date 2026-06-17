using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Controls;
using UnifiedMessenger.Dialogs;
using UnifiedMessenger.Models;
using UnifiedMessenger.Pages;
using UnifiedMessenger.ViewModels;
using Windows.System;
using Windows.UI.Shell;

namespace UnifiedMessenger.Services.Shell;

/// <summary>
/// Orchestrates shell navigation, chrome, notifications, and instance operations.
/// </summary>
public sealed class ShellController
{
    private readonly ApplicationServices _services;
    private readonly IShellUiHost _ui;
    private readonly MainWindowViewModel _viewModel;
    private readonly AdapterHealthMonitor _adapterHealth;
    private readonly ShellNavigationCoordinator _navigation;
    private readonly ShellChromeCoordinator _chrome;
    private readonly ShellCommandPaletteCoordinator _commandPalette;

    private bool _pendingPanelReveal;
    private bool _trackingStartupWarm;

    public ShellController(
        ApplicationServices services,
        IShellUiHost ui,
        MainWindowViewModel viewModel,
        AdapterHealthMonitor adapterHealth)
    {
        _services = services;
        _ui = ui;
        _viewModel = viewModel;
        _adapterHealth = adapterHealth;
        _ui.WorkspaceSidebar.ConfigureServices(services);
        _ui.NotificationPanel.ConfigureServices(services);
        _navigation = new ShellNavigationCoordinator(services, ui, viewModel);
        _chrome = new ShellChromeCoordinator(
            ui,
            services,
            () => new ShellSelectionState(
                _navigation.IsDashboardSelected,
                _navigation.IsSettingsSelected,
                _navigation.SelectedInstanceId));
        _navigation.BindChrome(_chrome);
        _commandPalette = new ShellCommandPaletteCoordinator(services);
    }

    public ShellNavigationCoordinator Navigation => _navigation;

    public ShellChromeCoordinator Chrome => _chrome;

    public MainWindowViewModel ViewModel => _viewModel;

    public void RegisterKeyboardShortcuts(
        KeyboardShortcutService keyboardShortcuts,
        Func<bool> canUseGlobalShortcuts,
        Action openCommandPalette)
    {
        keyboardShortcuts.Register(
            VirtualKey.D,
            VirtualKeyModifiers.Control,
            () => _ = _navigation.ShowDashboardAsync(),
            canUseGlobalShortcuts);
        keyboardShortcuts.Register(
            VirtualKey.K,
            VirtualKeyModifiers.Control,
            openCommandPalette,
            canUseGlobalShortcuts);
        keyboardShortcuts.Register(
            KeyboardShortcutService.SettingsShortcutKey,
            VirtualKeyModifiers.Control,
            () => _ = _navigation.ShowSettingsAsync(),
            canUseGlobalShortcuts);
        keyboardShortcuts.Register(
            VirtualKey.N,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            () => _chrome.SetNotificationPanelVisible(!_chrome.NotificationPanelVisible),
            canUseGlobalShortcuts);
        keyboardShortcuts.Register(
            VirtualKey.W,
            VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
            () => _ = ShowWorkspaceManagementAsync(),
            canUseGlobalShortcuts);
        keyboardShortcuts.RegisterIndexedShortcuts(
            VirtualKey.Number1,
            9,
            VirtualKeyModifiers.Control,
            index =>
            {
                var instances = _services.Registry.GetOrderedInstances().ToList();
                if (index >= 0 && index < instances.Count)
                {
                    _ = _navigation.SelectInstanceAsync(instances[index].Id);
                }
            },
            canUseGlobalShortcuts);
    }

    public IReadOnlyList<CommandPaletteEntry> BuildCommandPaletteEntries() =>
        _commandPalette.BuildEntries();

    public async Task HandleCommandPaletteSelectionAsync(CommandPaletteSelection selection)
    {
        switch (selection.Action)
        {
            case CommandPaletteAction.OpenDashboard:
                await _navigation.ShowDashboardAsync();
                break;
            case CommandPaletteAction.OpenSettings:
                await _navigation.ShowSettingsAsync();
                break;
            case CommandPaletteAction.OpenSettingsSection:
                await _navigation.ShowSettingsAsync(selection.SettingsSectionKey);
                break;
            case CommandPaletteAction.ToggleNotifications:
                _chrome.SetNotificationPanelVisible(!_chrome.NotificationPanelVisible);
                break;
            case CommandPaletteAction.MarkAllRead:
                _services.NotificationHub.MarkAllAlertsRead();
                break;
            case CommandPaletteAction.ClearNotifications:
                await _commandPalette.ConfirmClearNotificationsAsync();
                break;
            case CommandPaletteAction.OpenInstance:
                if (!string.IsNullOrWhiteSpace(selection.InstanceId))
                {
                    await _navigation.SelectInstanceAsync(selection.InstanceId);
                }

                break;
            case CommandPaletteAction.OpenAlert:
                if (!string.IsNullOrWhiteSpace(selection.AlertId))
                {
                    _services.NotificationHub.MarkAlertRead(selection.AlertId);
                }

                if (!string.IsNullOrWhiteSpace(selection.InstanceId))
                {
                    await _navigation.SelectInstanceAsync(selection.InstanceId);
                    if (!_chrome.NotificationPanelVisible)
                    {
                        _chrome.SetNotificationPanelVisible(true);
                    }
                }

                break;
            case CommandPaletteAction.RefreshOcc:
                await _navigation.ShowDashboardAsync();
                _services.Navigation.RequestDashboardRefresh();
                break;
            case CommandPaletteAction.FilterBranch:
                await _navigation.ShowDashboardAsync();
                _services.Navigation.RequestOccBranchFilter(selection.BranchKey);
                break;
            case CommandPaletteAction.OpenImmediateQueue:
                await _navigation.ShowDashboardAsync();
                _services.Navigation.RequestOccImmediateLaneFocus();
                break;
            case CommandPaletteAction.OpenThread:
                if (!string.IsNullOrWhiteSpace(selection.InstanceId))
                {
                    _services.Navigation.OpenInstance(
                        selection.InstanceId,
                        selection.ConversationKey,
                        selection.CustomerName);
                }

                break;
            case CommandPaletteAction.ManageWorkspaces:
                await ShowWorkspaceManagementAsync();
                break;
        }
    }

    public async Task InitializeAsync()
    {
        // App.OnLaunched loads settings first; this call is idempotent via AppSettingsService._isLoaded
        // and keeps ShellController safe when initialization order changes (e.g. tests, future entry points).
        await _services.AppSettings.LoadAsync().ConfigureAwait(true);
        await _services.Registry.LoadAsync().ConfigureAwait(true);
        await _services.MessageAnalytics.LoadAsync().ConfigureAwait(true);
        await _services.TriagePersistence.LoadAsync().ConfigureAwait(true);

        _chrome.PanePinned = _services.AppSettings.Settings.SidebarPinnedExpanded;
        _chrome.ApplySidebarLayout(forceVisible: true);
        _chrome.ApplyNotificationPanelDockLayout();
        _chrome.RebuildInstanceNavigation();
        RefreshNotificationUi();

        try
        {
            await _services.WebViewProfileManager.EnsureEnvironmentAsync().ConfigureAwait(true);
            await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView environment warmup failed: {ex}");
            AppLogger.LogError("Shell.WebView", ex);
        }

        var instances = _services.Registry.Instances.ToList();
        if (instances.Count > 0)
        {
            _trackingStartupWarm = true;
            _viewModel.BeginStartupWarm(instances.Count);
            _navigation.ApplyInstanceLoadingUi();
            try
            {
                await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);
                await _services.SessionManager.WarmAllSessionsAsync(instances, visibleInstanceId: null)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                await _services.Dialog.ShowErrorAsync("Could not start instances", ShellErrorFormatter.Format(ex));
            }
            finally
            {
                _trackingStartupWarm = false;
                _viewModel.ResetStartupWarmProgress();
                _navigation.ApplyInstanceLoadingUi();
            }
        }

        await _navigation.ShowDashboardAsync().ConfigureAwait(true);
        _ = MaybeShowWorkspaceOnboardingAsync();
        _ = MaybePromptPinToTaskbarAsync();

        if (_ui is MainWindow mainWindow)
        {
            _services.SystemTray.Attach(mainWindow);
        }

        _services.GitHubUpdate.PromptForUpdateApplicationAsync = PromptForAutoUpdateAsync;
    }

    public bool IsTrackingStartupWarm => _trackingStartupWarm;

    public void ApplyPanePinUi(Button panePinButton, FontIcon panePinIcon) =>
        _chrome.UpdatePanePinUi(panePinButton, panePinIcon);

    public void OnNotificationHubChanged(NotificationHubChangedEventArgs e)
    {
        RefreshNotificationUi();

        if (e.Kind != NotificationChangeKind.AlertAdded || e.Alert is null)
        {
            return;
        }

        if (_chrome.IsAppInForeground)
        {
            if (!_chrome.NotificationPanelVisible &&
                MainWindowShellLayout.ShouldAutoOpenNotificationPanel(
                    _services.AppSettings.Settings.PanelAutoOpen,
                    _chrome.IsAppInForeground))
            {
                _chrome.SetNotificationPanelVisible(true);
            }
        }
        else
        {
            if (MainWindowShellLayout.ShouldQueueDeferredPanelReveal(
                    _services.AppSettings.Settings.PanelAutoOpen))
            {
                _pendingPanelReveal = true;
            }

            if (_services.AppSettings.Settings.EnableBackgroundToasts)
            {
                var instance = _services.Registry.FindById(e.Alert.InstanceId);
                _services.AppNotification.ShowAlertToast(e.Alert, instance);
            }
        }
    }

    public void OnForegroundStateChanged()
    {
        if (!_chrome.IsAppInForeground || !_pendingPanelReveal || _chrome.NotificationPanelVisible)
        {
            return;
        }

        if (!MainWindowShellLayout.ShouldRevealDeferredPanel(_services.AppSettings.Settings.PanelAutoOpen))
        {
            _pendingPanelReveal = false;
            return;
        }

        _chrome.SetNotificationPanelVisible(true);
        _pendingPanelReveal = false;
    }

    public void ApplyWindowVisibilityState() =>
        _services.SessionManager.ApplyAppWindowState(_chrome.IsAppInForeground);

    public void RefreshNotificationUi()
    {
        foreach (var instance in _services.Registry.Instances)
        {
            _chrome.UpdateInstanceBadge(instance.Id);
        }

        _ui.WorkspaceSidebar.UpdateNotificationHubBadge(_services.NotificationHub.TotalUnreadCount);
        _ui.NotificationPanel.Refresh(_services.NotificationHub, _services.Registry.Instances);
        _ = _services.TaskbarBadge.SyncBadgeAsync(_services.NotificationHub.TotalUnreadCount);
        _navigation.RefreshDashboardIfVisible();
    }

    public async Task ShowWorkspaceManagementAsync()
    {
        var dialog = new WorkspaceManagementDialog(
            _services.Registry.Instances,
            _services.AppSettings.Settings.WorkspaceProfiles)
        {
            XamlRoot = _ui.XamlRoot
        };
        await dialog.ShowAsync();
    }

    public async Task ShowAddInstanceDialogAsync()
    {
        var previousFocus = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(_ui.XamlRoot) as Control;
        var dialog = new AddInstanceDialog(_services.Registry.ArchivedInstances) { XamlRoot = _ui.XamlRoot };
        var result = await dialog.ShowAsync();

        if (previousFocus is { IsEnabled: true, Visibility: Visibility.Visible })
        {
            _ = previousFocus.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(dialog.ResultRestoreInstanceId))
            {
                var restored = await _services.Registry.RestoreArchivedInstanceAsync(dialog.ResultRestoreInstanceId);
                _chrome.RebuildInstanceNavigation();
                _navigation.RefreshDashboardIfVisible();
                await _navigation.SelectInstanceAsync(restored.Id);
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

            _chrome.RebuildInstanceNavigation();
            _navigation.RefreshDashboardIfVisible();
            await _navigation.SelectInstanceAsync(instance.Id);
        }
        catch (Exception ex)
        {
            await _services.Dialog.ShowErrorAsync("Could not add instance", ex.Message);
        }
    }

    public async Task RestoreArchivedInstanceAsync(string instanceId)
    {
        var restored = await _services.Registry.RestoreArchivedInstanceAsync(instanceId);
        _chrome.RebuildInstanceNavigation();
        _navigation.RefreshDashboardIfVisible();

        if (_ui.ContentFrame.Content is SettingsPage settingsPage)
        {
            settingsPage.RefreshAll();
        }

        await _navigation.SelectInstanceAsync(restored.Id);
    }

    public void ShowInstanceContextMenu(
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

        flyout.Items.Add(BuildMemoryTierSubmenu(args.InstanceId, args.Instance.MemoryTier));

        var refreshItem = new MenuFlyoutItem { Text = "Refresh WebView" };
        refreshItem.Click += (_, _) => _ = _services.SessionManager.ReloadSessionAsync(args.InstanceId);
        flyout.Items.Add(refreshItem);

        if (_services.AppSettings.Settings.EnableEditInstanceMetadata)
        {
            var editItem = new MenuFlyoutItem { Text = "Edit instance metadata..." };
            editItem.Click += (_, _) => _ = EditInstanceMetadataAsync(args.InstanceId);
            flyout.Items.Add(editItem);
        }

        var removeItem = new MenuFlyoutItem { Text = "Remove instance..." };
        removeItem.Click += (_, _) => _ = DeleteInstanceAsync(args.InstanceId);
        flyout.Items.Add(removeItem);

        flyout.ShowAt(args.Anchor);
    }

    private MenuFlyoutSubItem BuildMemoryTierSubmenu(string instanceId, MemoryTierPreference currentTier)
    {
        var submenu = new MenuFlyoutSubItem { Text = "Memory tier" };
        foreach (var tier in new[] { MemoryTierPreference.Low, MemoryTierPreference.Normal, MemoryTierPreference.High })
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = WorkspaceSidebarHelper.FormatMemoryTierLabel(tier),
                IsChecked = tier == currentTier,
                Tag = tier
            };
            item.Click += (_, _) => _ = UpdateInstanceMemoryTierAsync(instanceId, tier);
            submenu.Items.Add(item);
        }

        return submenu;
    }

    private async Task UpdateInstanceMemoryTierAsync(string instanceId, MemoryTierPreference tier)
    {
        var instance = _services.Registry.FindById(instanceId);
        if (instance is null || instance.MemoryTier == tier)
        {
            return;
        }

        try
        {
            await _services.Registry.UpdateInstanceMemoryTierAsync(instanceId, tier);
            var updated = _services.Registry.FindById(instanceId);
            if (updated is not null)
            {
                _services.SessionManager.SyncInstance(updated);
                _services.SessionManager.RefreshMemoryTarget(instanceId);
            }

            _chrome.RebuildInstanceNavigation();
            _navigation.RefreshDashboardIfVisible();
        }
        catch (Exception ex)
        {
            await _services.Dialog.ShowErrorAsync("Could not update memory tier", ex.Message);
        }
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
            _chrome.RebuildInstanceNavigation();
            _navigation.RefreshDashboardIfVisible();
            _chrome.UpdateShellChromeSelection();
        }
        catch (Exception ex)
        {
            await _services.Dialog.ShowErrorAsync("Could not update workspace", ex.Message);
        }
    }

    private async Task RenameInstanceAsync(string instanceId)
    {
        var instance = _services.Registry.FindById(instanceId);
        if (instance is null)
        {
            return;
        }

        var dialog = new RenameInstanceDialog(instance.DisplayName) { XamlRoot = _ui.XamlRoot };
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary || dialog.ResultDisplayName is null)
        {
            return;
        }

        try
        {
            await _services.Registry.UpdateInstanceDisplayNameAsync(instanceId, dialog.ResultDisplayName);
            _chrome.RebuildInstanceNavigation();
            _navigation.RefreshDashboardIfVisible();
            _chrome.UpdateShellChromeSelection();
        }
        catch (Exception ex)
        {
            await _services.Dialog.ShowErrorAsync("Could not rename instance", ex.Message);
        }
    }

    private async Task EditInstanceMetadataAsync(string instanceId)
    {
        var instance = _services.Registry.FindById(instanceId);
        if (instance is null)
        {
            return;
        }

        var dialog = new EditInstanceMetadataDialog(instance) { XamlRoot = _ui.XamlRoot };
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
                XamlRoot = _ui.XamlRoot
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
            _chrome.RebuildInstanceNavigation();
            _navigation.RefreshDashboardIfVisible();
            _chrome.UpdateShellChromeSelection();
        }
        catch (Exception ex)
        {
            await _services.Dialog.ShowErrorAsync("Could not update instance metadata", ex.Message);
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

            _chrome.RebuildInstanceNavigation();
            RefreshNotificationUi();
        }
        catch (Exception ex)
        {
            await _services.Dialog.ShowErrorAsync("Could not update notification mute", ex.Message);
        }
    }

    private async Task DeleteInstanceAsync(string instanceId)
    {
        var instance = _services.Registry.FindById(instanceId);
        if (instance is null)
        {
            return;
        }

        var dialog = new DeleteInstanceDialog(instance.DisplayName) { XamlRoot = _ui.XamlRoot };
        await dialog.ShowAsync();
        if (dialog.Choice == DeleteInstanceChoice.Cancelled)
        {
            return;
        }

        if (dialog.Choice == DeleteInstanceChoice.PermanentDelete &&
            !await ConfirmPermanentDeleteAsync(instance.DisplayName))
        {
            return;
        }

        try
        {
            await InstanceDeletionService.DeleteAsync(_services, instance, dialog.Choice);

            _chrome.RebuildInstanceNavigation();
            RefreshNotificationUi();

            var nextInstance = _services.Registry.Instances.FirstOrDefault();
            if (nextInstance is not null)
            {
                await _navigation.SelectInstanceAsync(nextInstance.Id);
            }
            else
            {
                await _navigation.ShowDashboardAsync();
            }
        }
        catch (Exception ex)
        {
            await _services.Dialog.ShowErrorAsync("Could not remove instance", ex.Message);
        }
    }

    private async Task<bool> ConfirmPermanentDeleteAsync(string? displayName)
    {
        var dialog = new ContentDialog
        {
            Title = "Permanently delete account?",
            Content = SettingsPageHelper.BuildPermanentDeleteConfirmation(displayName),
            PrimaryButtonText = "Delete permanently",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _ui.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
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
            XamlRoot = _ui.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task MaybeShowWorkspaceOnboardingAsync()
    {
        var settings = _services.AppSettings.Settings;
        if (settings.HasCompletedWorkspaceOnboarding)
        {
            return;
        }

        await FirstRunOnboardingHelper.TryShowAsync(_ui.XamlRoot).ConfigureAwait(true);
        await _services.AppSettings.UpdateAsync(s => s.HasCompletedWorkspaceOnboarding = true).ConfigureAwait(true);
    }

    private async Task MaybePromptPinToTaskbarAsync()
    {
        try
        {
            await MaybePromptPinToTaskbarCoreAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Taskbar pin prompt failed: {ex.Message}");
            AppLogger.LogWarning("Shell.TaskbarPin", ex.Message);
        }
    }

    private async Task MaybePromptPinToTaskbarCoreAsync()
    {
        var settings = _services.AppSettings.Settings;
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
            await _services.AppSettings.UpdateAsync(s => s.HasPromptedPinToTaskbar = true);
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Pin Unified Messenger?",
            Content = "Pin this app to your taskbar for quick access to all your messaging accounts.",
            PrimaryButtonText = "Pin to taskbar",
            CloseButtonText = "Not now",
            XamlRoot = _ui.XamlRoot
        };

        var result = await dialog.ShowAsync();
        await _services.AppSettings.UpdateAsync(s => s.HasPromptedPinToTaskbar = true);
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
            AppLogger.LogWarning("Shell.TaskbarPin", ex.Message);
            await _services.Dialog.ShowErrorAsync(
                "Could not pin to taskbar",
                "Right-click the taskbar icon and choose Pin to taskbar.");
        }
    }
}

