using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
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
                _navigation.CurrentSection,
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
            case CommandPaletteAction.OpenSection when selection.Section is { } paletteSection:
                await _navigation.ShowSectionAsync(paletteSection);
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

    /// <summary>
    /// Loads one auxiliary store, absorbing any failure so startup continues without it.
    /// </summary>
    /// <remarks>
    /// These eleven loads used to be bare <c>await</c>s. Each store's own handler caught the malformed-JSON
    /// case, but several caught nothing else — so a file an antivirus or backup tool held open for a moment
    /// threw <see cref="IOException"/> straight through <see cref="InitializeAsync"/>, through
    /// <c>MainWindow.RunInitializationAsync</c>, and into <c>App.LaunchAsync</c>'s catch, which shows
    /// "The application could not start." and exits. A statistics file being briefly locked stopped the
    /// owner opening the app that holds their accounts.
    ///
    /// The stores were each fixed to route through <see cref="CorruptFileRecovery"/>, but that fix is
    /// per-store and a new store added to the list would not inherit it. This makes the guarantee
    /// structural: whatever a store does or fails to do, it cannot take startup down with it. The failure
    /// is logged with the store's name, so <c>app.log</c> says which one and why.
    ///
    /// <see cref="OperationCanceledException"/> is deliberately not special-cased — during startup there is
    /// no cancellation to honour, and swallowing it here would be the same silence being removed.
    /// </remarks>
    internal static async Task LoadStoreAsync(string scope, Func<CancellationToken, Task> load)
    {
        try
        {
            await load(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"Shell.Load.{scope}", ex);
        }
    }

    /// <summary>
    /// The account the lazy startup warm should bring up, or null when there is nothing to warm.
    /// </summary>
    internal static string? ResolveStartupWarmInstanceId(
        IReadOnlyCollection<MessengerInstance> instances,
        string? lastVisitedInstanceId)
    {
        if (string.IsNullOrWhiteSpace(lastVisitedInstanceId))
        {
            return null;
        }

        var remembered = lastVisitedInstanceId.Trim();
        return instances.Any(i => i.Id.Equals(remembered, StringComparison.OrdinalIgnoreCase))
            ? remembered
            : null;
    }

    private string? ResolveStartupWarmInstanceId(IReadOnlyCollection<MessengerInstance> instances) =>
        ResolveStartupWarmInstanceId(instances, _services.AppSettings.Settings.LastVisitedInstanceId);

    /// <summary>
    /// How many accounts the warm about to run will actually bring up, for the progress readout.
    /// </summary>
    /// <remarks>
    /// It was passed the full account count regardless of mode, so the default (lazy) configuration
    /// announced "starting 8 accounts" and started none. A progress bar that overstates its own work is the
    /// same class of defect as a metric that does.
    /// </remarks>
    /// <remarks>
    /// <paramref name="settings"/> is required, not an optional convenience with a singleton fallback.
    /// The shell layer is barred from reaching for the app-settings singleton — settings arrive through
    /// <c>_services</c> — and the first version of this method took that singleton as a default, which the
    /// CI gate rejected. Enforced locally now too, by <c>ShellLayerDiTests</c>.
    ///
    /// The gate is a plain substring match over the file's whole text, so it fires on a *comment* naming
    /// the type as readily as on a call. Naming it in prose here would fail the build just as the call did.
    /// </remarks>
    internal static int StartupWarmCount(
        IReadOnlyCollection<MessengerInstance> instances,
        string? warmInstanceId,
        AppSettings settings)
    {
        var mode = InstanceSessionManager.ResolveWarmMode(settings);

        return mode switch
        {
            StartupWarmMode.Lazy or StartupWarmMode.VisibleOnly => string.IsNullOrWhiteSpace(warmInstanceId) ? 0 : 1,
            _ => instances.Count
        };
    }

    private int StartupWarmCount(IReadOnlyCollection<MessengerInstance> instances, string? warmInstanceId) =>
        StartupWarmCount(instances, warmInstanceId, _services.AppSettings.Settings);

    public async Task InitializeAsync()
    {
        // App.OnLaunched loads settings first; this call is idempotent via AppSettingsService._isLoaded
        // and keeps ShellController safe when initialization order changes (e.g. tests, future entry points).
        await _services.AppSettings.LoadAsync().ConfigureAwait(true);
        await _services.Registry.LoadAsync().ConfigureAwait(true);

        // Everything below is an auxiliary store: history, caches and overrides. Losing one costs a
        // feature; it must never cost the app. Each load is isolated by LoadStoreAsync — see its remarks.
        await LoadStoreAsync("Analytics", _services.MessageAnalytics.LoadAsync).ConfigureAwait(true);
        await LoadStoreAsync("Triage.Store", _services.TriagePersistence.LoadAsync).ConfigureAwait(true);
        // Last-known oversight snapshot, so the command center shows numbers immediately on launch
        // (labeled "as of …") instead of going blank until the next scan.
        await LoadStoreAsync("Oversight.Snapshot", OversightChatSnapshotService.Instance.LoadAsync).ConfigureAwait(true);
        // Forward-tracked First Response Time samples + in-flight pending waits.
        await LoadStoreAsync("ResponseTimes", ResponseTimeTracker.Instance.LoadAsync).ConfigureAwait(true);
        // Per-customer first/last-seen history for the new-vs-returning insight.
        await LoadStoreAsync("ContactHistory", ContactHistoryStore.Instance.LoadAsync).ConfigureAwait(true);
        // Manual "handled elsewhere" / snooze overrides for the awaiting lists.
        await LoadStoreAsync("AwaitingOverrides", AwaitingOverrideStore.Instance.LoadAsync).ConfigureAwait(true);

        // Seeded on first run, so the reply library does something the day it is installed rather than
        // being an empty feature the owner has to build before it helps.
        await LoadStoreAsync("SavedReplies", SavedReplyStore.Instance.LoadAsync).ConfigureAwait(true);
        // Daily caught-up% / awaiting history for the KPI micro-trend sparklines.
        await LoadStoreAsync("KpiTrends", KpiTrendStore.Instance.LoadAsync).ConfigureAwait(true);
        // Daily review readings per Google account. Without this the Review Desk starts every run with
        // no rating, no trend and no velocity, because the snapshot service only holds them in memory.
        await LoadStoreAsync("ReviewHistory", ReviewHistoryStore.Instance.LoadAsync).ConfigureAwait(true);
        // Who has already been asked for a review. Loaded before anything can offer to ask again:
        // "ask once, ever" is only a promise if the record survives the restart.
        await LoadStoreAsync("ReviewAsks", ReviewAskStore.Instance.LoadAsync).ConfigureAwait(true);
        // Which unhappy reviews have already been notified about. Loaded before the first background
        // pass can evaluate: unloaded, every restart looks like a first run, and a first run stays
        // silent — so a one-star that arrived while the app was closed would never be raised.
        await LoadStoreAsync("ReviewAlerts", ReviewAlertStore.Instance.LoadAsync).ConfigureAwait(true);

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
            AppLogger.LogError("Shell.WebView", ex);
        }

        var instances = _services.Registry.Instances.ToList();
        if (instances.Count > 0)
        {
            // The account to bring up under the lazy warm modes. Passed null until v4.99.56, because
            // nothing recorded which account had been open — so the default configuration warmed nothing,
            // no account reached Connected, and the background scan skipped every one of them. Resolved
            // against the registry because a remembered account can since have been deleted.
            var warmInstanceId = ResolveStartupWarmInstanceId(instances);

            _trackingStartupWarm = true;
            _viewModel.BeginStartupWarm(StartupWarmCount(instances, warmInstanceId));
            _navigation.ApplyInstanceLoadingUi();
            try
            {
                await UiThreadRunner.YieldToUiAsync().ConfigureAwait(true);
                await _services.SessionManager.WarmAllSessionsAsync(instances, warmInstanceId)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                await _services.Dialog.ShowErrorAsync("Could not start your accounts", UserFacingError.Describe("Shell.StartAccounts", ex));
            }
            finally
            {
                _trackingStartupWarm = false;
                _viewModel.ResetStartupWarmProgress();
                _navigation.ApplyInstanceLoadingUi();
            }
        }

        // Reopen wherever the owner left off. ParseSection defaults to Dashboard for anything it doesn't
        // recognise, so a stale or hand-edited settings value can't stop the shell from starting.
        var startupSection = WorkspaceSidebarHelper.ParseSection(
            _services.AppSettings.Settings.LastVisitedSection);
        await _navigation.ShowSectionAsync(startupSection).ConfigureAwait(true);
        _ = RunStartupPromptsAsync();

        if (_ui is MainWindow mainWindow)
        {
            _services.SystemTray.Attach(mainWindow);
        }

        _services.GitHubUpdate.PromptForUpdateApplicationAsync = PromptForAutoUpdateAsync;

        // Bring the professional accounts up behind the shell. Started last and never awaited: the whole
        // point is that the owner gets a live window immediately and the accounts fill in behind it, so
        // awaiting this here would reintroduce exactly the wait it exists to remove.
        //
        // Until now nothing was brought up automatically at all, so an account only reported Connected
        // once the owner opened it by hand — and OversightAlertMonitor skips every account that has not,
        // which meant the background scan never ran and the numbers only ever moved for accounts that had
        // been clicked. Six professional accounts against a session cap of six, and the idle reaper
        // already refuses to close professional sessions, so they stay up once they are up.
        if (instances.Count > 0)
        {
            _ = WarmBackgroundSessionsAsync(instances);
        }
    }

    /// <summary>
    /// Fire-and-forget wrapper. An exception escaping an un-awaited task reaches the finalizer thread as an
    /// unobserved-task fault, which is exactly how the session-map race stayed invisible for so long.
    /// </summary>
    private async Task WarmBackgroundSessionsAsync(IReadOnlyList<MessengerInstance> instances)
    {
        try
        {
            await _services.SessionManager.WarmBackgroundSessionsAsync(instances).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Shell.BackgroundWarm", ex);
        }
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
        await dialog.ShowManagedAsync();
    }

    public async Task ShowAddInstanceDialogAsync()
    {
        var previousFocus = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(_ui.XamlRoot) as Control;
        var dialog = new AddInstanceDialog(_services.Registry.ArchivedInstances) { XamlRoot = _ui.XamlRoot };
        var result = await dialog.ShowManagedAsync();

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
            await _services.Dialog.ShowErrorAsync("Could not add account", UserFacingError.Describe("Shell.AddAccount", ex));
        }
    }

    /// <summary>
    /// Saves the page currently open in a Custom URL tab as its own account in the sidebar, so a site the
    /// owner browsed to can be kept rather than re-found next time. Confirms first (this adds a permanent
    /// sidebar entry and its own isolated browser profile) and names it after the site's host by default.
    /// </summary>
    public async Task SaveCurrentSiteAsInstanceAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var suggestedName = BrowserAddressNormalizer.SuggestDisplayName(url);
        var display = BrowserAddressNormalizer.ToDisplayForm(url);

        var confirmed = await _services.Dialog.ConfirmAsync(
            "Save this site?",
            $"Add \"{suggestedName}\" ({display}) to your sidebar as its own account. " +
            "It gets its own separate sign-in, and no oversight metrics are collected for it.",
            "Save site").ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        try
        {
            var instance = await _services.Registry.AddInstanceAsync(
                suggestedName,
                "generic",
                url,
                WorkspaceCategory.Personal).ConfigureAwait(true);

            _chrome.RebuildInstanceNavigation();
            _navigation.RefreshDashboardIfVisible();
            await _navigation.SelectInstanceAsync(instance.Id).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _services.Dialog.ShowErrorAsync("Could not save this site", UserFacingError.Describe("Shell.SaveCustomSite", ex)).ConfigureAwait(true);
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
        AutomationProperties.SetName(moveItem, args.Instance.IsProfessional
            ? "Move account to Personal workspace"
            : "Move account to Professional workspace");
        moveItem.Click += (_, _) => _ = ToggleInstanceCategoryAsync(args.InstanceId);
        flyout.Items.Add(moveItem);

        if (args.Instance.IsProfessional)
        {
            var locationItem = new MenuFlyoutItem { Text = "Set location…" };
            AutomationProperties.SetName(locationItem, "Set account location");
            locationItem.Click += (_, _) => _ = SetInstanceLocationAsync(args.InstanceId);
            flyout.Items.Add(locationItem);
        }

        var moveUpItem = new MenuFlyoutItem { Text = "Move up" };
        AutomationProperties.SetName(moveUpItem, "Move account up in sidebar");
        moveUpItem.Click += (_, _) => _ = ReorderInstanceByDirectionAsync(args.InstanceId, -1);
        flyout.Items.Add(moveUpItem);

        var moveDownItem = new MenuFlyoutItem { Text = "Move down" };
        AutomationProperties.SetName(moveDownItem, "Move account down in sidebar");
        moveDownItem.Click += (_, _) => _ = ReorderInstanceByDirectionAsync(args.InstanceId, 1);
        flyout.Items.Add(moveDownItem);

        var renameItem = new MenuFlyoutItem { Text = "Rename account…", AccessKey = "R" };
        AutomationProperties.SetName(renameItem, "Rename account");
        renameItem.Click += (_, _) => _ = RenameInstanceAsync(args.InstanceId);
        flyout.Items.Add(renameItem);

        var iconItem = new MenuFlyoutItem { Text = "Change icon…", AccessKey = "I" };
        AutomationProperties.SetName(iconItem, "Change account icon");
        iconItem.Click += (_, _) => _ = ChangeInstanceIconAsync(args.InstanceId);
        flyout.Items.Add(iconItem);

        var muteItem = new MenuFlyoutItem
        {
            Text = args.Instance.NotificationsMuted
                ? "Unmute notifications"
                : "Mute notifications",
            AccessKey = "M"
        };
        AutomationProperties.SetName(muteItem, args.Instance.NotificationsMuted
            ? "Unmute notifications for this account"
            : "Mute notifications for this account");
        muteItem.Click += (_, _) => _ = ToggleInstanceMuteAsync(args.InstanceId);
        flyout.Items.Add(muteItem);

        flyout.Items.Add(BuildMemoryTierSubmenu(args.InstanceId, args.Instance.MemoryTier));

        var refreshItem = new MenuFlyoutItem { Text = "Refresh WebView" };
        AutomationProperties.SetName(refreshItem, "Reload this account's web view");
        refreshItem.Click += (_, _) => _ = _services.SessionManager.ReloadSessionAsync(args.InstanceId);
        flyout.Items.Add(refreshItem);

        if (_services.AppSettings.Settings.EnableEditInstanceMetadata)
        {
            var editItem = new MenuFlyoutItem { Text = "Edit account details…" };
            AutomationProperties.SetName(editItem, "Edit account details");
            editItem.Click += (_, _) => _ = EditInstanceMetadataAsync(args.InstanceId);
            flyout.Items.Add(editItem);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());

        var removeItem = new MenuFlyoutItem { Text = "Remove account…", AccessKey = "X" };
        AutomationProperties.SetName(removeItem, "Remove account permanently");
        removeItem.Click += (_, _) => _ = DeleteInstanceAsync(args.InstanceId);
        flyout.Items.Add(removeItem);

        flyout.ShowAt(args.Anchor);
    }

    private MenuFlyoutSubItem BuildMemoryTierSubmenu(string instanceId, MemoryTierPreference currentTier)
    {
        var submenu = new MenuFlyoutSubItem { Text = "Memory tier" };
        AutomationProperties.SetName(submenu, "Memory tier submenu");
        foreach (var tier in new[] { MemoryTierPreference.Low, MemoryTierPreference.Normal, MemoryTierPreference.High })
        {
            var label = WorkspaceSidebarHelper.FormatMemoryTierLabel(tier);
            var item = new RadioMenuFlyoutItem
            {
                Text = label,
                IsChecked = tier == currentTier,
                Tag = tier
            };
            AutomationProperties.SetName(item, $"Set memory tier to {label}");
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
            await _services.Dialog.ShowErrorAsync("Could not update memory tier", UserFacingError.Describe("Shell.MemoryTier", ex));
        }
    }

    private async Task ReorderInstanceByDirectionAsync(string instanceId, int direction)
    {
        try
        {
            await _services.Registry.MoveInstanceAsync(instanceId, direction);
            _chrome.RebuildInstanceNavigation();
            _chrome.UpdateShellChromeSelection();
            _navigation.RefreshDashboardIfVisible();
        }
        catch (Exception ex)
        {
            await _services.Dialog.ShowErrorAsync("Could not move account", UserFacingError.Describe("Shell.MoveAccount", ex));
        }
    }

    private async Task SetInstanceLocationAsync(string instanceId)
    {
        var instance = _services.Registry.FindById(instanceId);
        if (instance is null)
        {
            return;
        }

        var existing = _services.Registry.Instances
            .Where(i => i.IsProfessional && !string.IsNullOrWhiteSpace(i.BranchKey))
            .Select(i => i.BranchKey!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dialog = new SetLocationDialog(instance.DisplayName, instance.BranchKey, existing)
        {
            XamlRoot = _ui.XamlRoot
        };

        var result = await dialog.ShowManagedAsync();
        if (result == ContentDialogResult.None)
        {
            return;
        }

        try
        {
            await _services.Registry.UpdateInstanceBranchKeyAsync(instanceId, dialog.SelectedLocation);
            _chrome.RebuildInstanceNavigation();
            _navigation.RefreshDashboardIfVisible();
        }
        catch (Exception ex)
        {
            await _services.Dialog.ShowErrorAsync("Could not set location", UserFacingError.Describe("Shell.SetLocation", ex));
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
            await _services.Dialog.ShowErrorAsync("Could not update workspace", UserFacingError.Describe("Shell.UpdateWorkspace", ex));
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
        var result = await dialog.ShowManagedAsync();
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
            await _services.Dialog.ShowErrorAsync("Could not rename account", UserFacingError.Describe("Shell.RenameAccount", ex));
        }
    }

    private Task ChangeInstanceIconAsync(string instanceId)
    {
        var instance = _services.Registry.FindById(instanceId);
        if (instance is null)
        {
            return Task.CompletedTask;
        }

        // WebView2 renders in "airspace" above XAML, so an open account's WebView would paint over the
        // ContentDialog. Collapse the WebView host while the dialog is up, then restore it.
        var webHost = _ui.InstanceWebViewHost;
        var priorWebHostVisibility = webHost.Visibility;

        return AccountIconChangeFlow.RunAsync(
            _services,
            instance,
            _ui.XamlRoot,
            AccountIconChangeFlow.PickImageBytesAsync,
            beforeShow: () => webHost.Visibility = Visibility.Collapsed,
            afterShow: () => webHost.Visibility = priorWebHostVisibility,
            onChanged: () =>
            {
                // Avatar changes aren't captured by the sidebar's incremental path or the command center's
                // data signature, so force both to redraw.
                _ui.WorkspaceSidebar.InvalidatePlan();
                _chrome.RebuildInstanceNavigation();
                _navigation.ForceRefreshDashboardIcons();
                _chrome.UpdateShellChromeSelection();
            });
    }

    private async Task EditInstanceMetadataAsync(string instanceId)
    {
        var instance = _services.Registry.FindById(instanceId);
        if (instance is null)
        {
            return;
        }

        var dialog = new EditInstanceMetadataDialog(instance) { XamlRoot = _ui.XamlRoot };
        var result = await dialog.ShowManagedAsync();
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

            if (await confirm.ShowManagedAsync() != ContentDialogResult.Primary)
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
            await _services.Dialog.ShowErrorAsync("Could not update account details", UserFacingError.Describe("Shell.UpdateAccountDetails", ex));
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
            await _services.Dialog.ShowErrorAsync("Could not update notification mute", UserFacingError.Describe("Shell.NotificationMute", ex));
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
        await dialog.ShowManagedAsync();
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
            await _services.Dialog.ShowErrorAsync("Could not remove account", UserFacingError.Describe("Shell.RemoveAccount", ex));
        }
    }

    private async Task<bool> ConfirmPermanentDeleteAsync(string? displayName)
    {
        var dialog = new ConfirmPermanentDeleteDialog(displayName) { XamlRoot = _ui.XamlRoot };
        return await dialog.ShowManagedAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> PromptForAutoUpdateAsync(UpdateCheckResult result, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = result.CurrentVersion?.ToString() ?? "unknown";
        var latest = result.LatestVersion?.ToString() ?? "unknown";
        var dialog = new AutoUpdateDialog(current, latest) { XamlRoot = _ui.XamlRoot };
        return await dialog.ShowManagedAsync() == ContentDialogResult.Primary;
    }

    /// <summary>
    /// The startup prompts, run one after another.
    ///
    /// <para>
    /// They used to be started as two separate <c>_ = …</c> calls, which meant both could reach
    /// <c>ShowAsync</c> at once — and WinUI permits only one <see cref="ContentDialog"/> open at a time,
    /// so the second threw. Both call sites caught and logged it, so nothing crashed; the prompt simply
    /// never appeared. For the onboarding wizard that is worse than it sounds, because its <c>finally</c>
    /// sets <c>HasCompletedWorkspaceOnboarding = true</c> whether or not the wizard was ever seen — so a
    /// swallowed wizard is marked done for good.
    /// </para>
    /// <para>
    /// The collision is not hypothetical, and this change is a prerequisite for the recovery notice
    /// rather than tidying: after a settings reset <i>every</i> flag is back to its default, so
    /// onboarding and the taskbar prompt both come due in the same session — the exact session the
    /// recovery notice also wants to speak in. Recovery goes first, so the owner understands why they
    /// are being asked to set the app up again.
    /// </para>
    /// </summary>
    private async Task RunStartupPromptsAsync()
    {
        // First of all of them. If the account list could not be read, the owner is looking at a first-run
        // welcome screen that appears to say their business's message history is gone; nothing else the
        // app has to say this session comes close to that in importance. It also gates onboarding, which
        // would otherwise invite them to set the app up from scratch on top of accounts that already exist.
        var accountsUnavailable = await MaybeShowAccountsUnavailableNoticeAsync().ConfigureAwait(true);
        if (accountsUnavailable)
        {
            return;
        }

        await MaybeShowSettingsRecoveryNoticeAsync().ConfigureAwait(true);
        await MaybeShowWorkspaceOnboardingAsync().ConfigureAwait(true);
        await MaybePromptPinToTaskbarAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Shows the accounts-unavailable notice when the registry could not be read. Returns true when the
    /// session is still in that state afterwards (so the remaining startup prompts should stay quiet).
    /// </summary>
    private async Task<bool> MaybeShowAccountsUnavailableNoticeAsync()
    {
        if (!AccountsUnavailableNotice.ShouldShow(_services.Registry.LoadOutcome))
        {
            return false;
        }

        try
        {
            AppLogger.LogWarning(
                "Registry.Recovery",
                $"Telling the owner their accounts could not be read ({_services.Registry.LoadFailureDetail}).");

            var recovered = await AccountsUnavailableDialog
                .ShowAsync(_ui.XamlRoot, _services.Registry)
                .ConfigureAwait(true);

            if (!recovered)
            {
                return true;
            }

            // The retry worked, so the shell is now showing an account list that is a session out of date.
            _chrome.RebuildInstanceNavigation();
            var instances = _services.Registry.Instances.ToList();
            if (instances.Count > 0)
            {
                await _services.SessionManager
                    .WarmAllSessionsAsync(instances, visibleInstanceId: null)
                    .ConfigureAwait(true);
            }

            return false;
        }
        catch (Exception ex)
        {
            // Never let the notice about a failure become a failure of its own.
            AppLogger.LogWarning("Registry.Recovery", ex.Message);
            return true;
        }
    }

    private async Task MaybeShowSettingsRecoveryNoticeAsync()
    {
        if (!SettingsRecoveryNotice.ShouldShow(_services.AppSettings.RecoveredFromCorruptFile))
        {
            return;
        }

        try
        {
            AppLogger.LogInfo(
                "Settings.Recovery",
                "Telling the user their settings were reset " +
                $"(preserved copy: {_services.AppSettings.CorruptFileBackupPath ?? "none"}).");

            await SettingsRecoveryDialog
                .ShowAsync(_ui.XamlRoot, _services.AppSettings.CorruptFileBackupPath)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // Never let the notice about a recovered failure become a failure of its own.
            AppLogger.LogWarning("Settings.Recovery", ex.Message);
        }
    }

    private async Task MaybeShowWorkspaceOnboardingAsync()
    {
        if (_services.AppSettings.Settings.HasCompletedWorkspaceOnboarding)
        {
            return;
        }

        try
        {
            var result = await FirstRunOnboardingHelper.TryShowAsync(_ui.XamlRoot).ConfigureAwait(true);
            if (!result.WasSkipped)
            {
                if (result.AddAccount)
                {
                    await ShowAddInstanceDialogAsync().ConfigureAwait(true);
                }

                if (result.ConfigureLocations || result.ConfigureHoursSla)
                {
                    await ShowWorkspaceManagementAsync().ConfigureAwait(true);
                }
            }
        }
        catch (Exception ex)
        {
            // A wizard hiccup must never crash startup or nag every launch.
            AppLogger.LogWarning("Shell.Onboarding", ex.Message);
        }
        finally
        {
            await _services.AppSettings.UpdateAsync(s => s.HasCompletedWorkspaceOnboarding = true).ConfigureAwait(true);
        }
    }

    private async Task MaybePromptPinToTaskbarAsync()
    {
        try
        {
            await MaybePromptPinToTaskbarCoreAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
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

        var dialog = new PinToTaskbarDialog { XamlRoot = _ui.XamlRoot };
        var result = await dialog.ShowManagedAsync();
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
            AppLogger.LogWarning("Shell.TaskbarPin", ex.Message);
            await _services.Dialog.ShowErrorAsync(
                "Could not pin to taskbar",
                "Right-click the taskbar icon and choose Pin to taskbar.");
        }
    }
}

