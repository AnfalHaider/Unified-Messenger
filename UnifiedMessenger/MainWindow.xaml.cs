using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Pages;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Shell;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger;

public sealed partial class MainWindow : Window, IShellUiHost
{
    private readonly ApplicationServices _services = ApplicationServiceProvider.Current;
    private readonly MainWindowViewModel _shellViewModel = new();
    private readonly KeyboardShortcutService _keyboardShortcuts;
    private readonly ShellController _shell;
    private bool _forceShutdown;
    private int _startupWarmCompleted;

    DispatcherQueue IShellUiHost.DispatcherQueue => DispatcherQueue;
    XamlRoot IShellUiHost.XamlRoot => Content.XamlRoot;
    Grid IShellUiHost.InstanceWebViewHost => InstanceWebViewHost;
    Grid IShellUiHost.ShellLayoutGrid => ShellLayoutGrid;
    Frame IShellUiHost.ContentFrame => ContentFrame;
    WorkspaceSidebar IShellUiHost.WorkspaceSidebar => WorkspaceSidebar;
    NotificationFeedPanel IShellUiHost.NotificationPanel => NotificationPanel;
    TitleBar IShellUiHost.AppTitleBar => AppTitleBar;
    Button IShellUiHost.NotificationToggleButton => NotificationToggleButton;
    ColumnDefinition IShellUiHost.SidebarColumn => SidebarColumn;
    ColumnDefinition IShellUiHost.NotificationColumn => NotificationColumn;
    RowDefinition IShellUiHost.NotificationRow => NotificationRow;
    StackPanel IShellUiHost.InstanceLoadingPanel => InstanceLoadingPanel;
    ProgressBar IShellUiHost.StartupWarmProgressBar => StartupWarmProgressBar;
    TextBlock IShellUiHost.InstanceLoadingText => InstanceLoadingText;
    void IShellUiHost.ActivateWindow() => Activate();
    void IShellUiHost.ShowAppWindow() => AppWindow.Show();
    public MainWindow()
    {
        InitializeComponent();
        _services.ConfigureUi(() => Content.XamlRoot);
        _shell = new ShellController(_services, this, _shellViewModel, _services.AdapterHealth);

        _keyboardShortcuts = new KeyboardShortcutService((UIElement)Content);
        _shell.RegisterKeyboardShortcuts(
            _keyboardShortcuts,
            () => !CommandPaletteOverlay.IsOpen,
            OpenCommandPalette);

        _services.SessionManager.AttachHost(InstanceWebViewHost);

        WorkspaceSidebar.DashboardRequested += (_, _) => _ = _shell.Navigation.ShowDashboardAsync();
        WorkspaceSidebar.InstanceRequested += (_, id) => _ = _shell.Navigation.SelectInstanceAsync(id);
        WorkspaceSidebar.AddInstanceRequested += (_, _) => _ = _shell.ShowAddInstanceDialogAsync();
        WorkspaceSidebar.NotificationsRequested += (_, _) =>
            _shell.Chrome.SetNotificationPanelVisible(!_shell.Chrome.NotificationPanelVisible);
        WorkspaceSidebar.SettingsRequested += (_, _) => _ = _shell.Navigation.ShowSettingsAsync();
        WorkspaceSidebar.InstanceContextRequested += (_, args) => _shell.ShowInstanceContextMenu(args);
        WorkspaceSidebar.InstanceReorderRequested += OnInstanceReorderRequested;
        WorkspaceSidebar.Loaded += (_, _) => _shell.Chrome.RebuildInstanceNavigation();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Changed += OnAppWindowChanged;
        AppWindow.Closing += OnAppWindowClosing;
        Activated += OnWindowActivated;
        Closed += OnMainWindowClosed;

        NotificationPanel.CollapseRequested += (_, _) => _shell.Chrome.SetNotificationPanelVisible(false);
        NotificationPanel.AlertClicked += (_, alert) =>
            NotificationNavigationHelper.OpenAlert(_services.Navigation, alert);

        AttachShellHandlers();
    }

    public async Task RunInitializationAsync()
    {
        await _shell.InitializeAsync();
        _shell.ApplyPanePinUi(PanePinButton, PanePinIcon);
    }

    public void ShowFromTray()
    {
        AppWindow.Show();
        Activate();
        _shell.Chrome.IsAppInForeground = true;
    }

    public async void RequestTrayQuit()
    {
        if (_forceShutdown)
        {
            return;
        }

        _forceShutdown = true;
        _services.GlobalHotkey.Dispose();
        await ApplicationLifecycleService.ShutdownAsync().ConfigureAwait(true);
        _services.SystemTray.Dispose();
        Close();
    }

    private void AttachShellHandlers()
    {
        var nav = _services.Navigation;
        nav.InstanceNavigationRequested += (_, request) =>
            DispatcherQueue.TryEnqueue(() => _ = _shell.Navigation.NavigateToInstanceAsync(request));
        nav.DashboardRefreshRequested += (_, _) =>
            DispatcherQueue.TryEnqueue(_shell.Navigation.RefreshDashboardIfVisible);
        nav.ArchivedInstanceRestoreRequested += OnArchivedInstanceRestoreRequested;
        nav.LayoutRefreshRequested += (_, _) =>
            DispatcherQueue.TryEnqueue(_shell.Chrome.ApplyNotificationPanelDockLayout);
        nav.InstanceRegistryRefreshRequested += (_, _) =>
            DispatcherQueue.TryEnqueue(() =>
            {
                _shell.Chrome.RebuildInstanceNavigation();
                _shell.Navigation.RefreshDashboardIfVisible();
            });
        nav.AddInstanceRequested += OnAddInstanceRequested;

        _services.NotificationHub.Changed += OnNotificationHubChanged;
        _services.AppNotification.ActivationRequested += OnToastActivationRequested;
        _services.AdapterHealth.Changed += (_, _) => DispatcherQueue.TryEnqueue(RefreshAdapterHealthIndicators);
        _services.AdapterHealth.AdapterStaleDetected += OnAdapterStaleDetected;
        _services.ConnectionStatus.Changed += OnConnectionStatusChanged;
        _services.SessionManager.SessionInitializing += OnSessionInitializing;
        _services.SessionManager.SessionFailed += OnSessionFailed;
        _services.AutoDraft.DraftCompleted += OnAutoDraftCompleted;
        _services.MessageAnalytics.Changed += (_, _) =>
            DispatcherQueue.TryEnqueue(_shell.Navigation.RefreshDashboardIfVisible);
        _services.AppSettings.Changed += OnAppSettingsChanged;
        _services.GlobalHotkey.CtrlSpacePressed += OnGlobalCopilotHotkey;
    }

    private void OnAddInstanceRequested(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = _shell.ShowAddInstanceDialogAsync());

    private void DetachShellHandlers()
    {
        _services.Navigation.AddInstanceRequested -= OnAddInstanceRequested;
        _services.NotificationHub.Changed -= OnNotificationHubChanged;
        _services.AppNotification.ActivationRequested -= OnToastActivationRequested;
        _services.AdapterHealth.AdapterStaleDetected -= OnAdapterStaleDetected;
        _services.ConnectionStatus.Changed -= OnConnectionStatusChanged;
        _services.SessionManager.SessionInitializing -= OnSessionInitializing;
        _services.SessionManager.SessionFailed -= OnSessionFailed;
        _services.AutoDraft.DraftCompleted -= OnAutoDraftCompleted;
        _services.AppSettings.Changed -= OnAppSettingsChanged;
        _services.GlobalHotkey.CtrlSpacePressed -= OnGlobalCopilotHotkey;
    }

    private void OpenCommandPalette()
    {
        CommandPaletteOverlay.SetEntries(_shell.BuildCommandPaletteEntries());
        CommandPaletteOverlay.Open();
    }

    private void CommandPaletteOverlay_ItemSelected(object? sender, CommandPaletteSelection selection) =>
        _ = _shell.HandleCommandPaletteSelectionAsync(selection);

    private void OnArchivedInstanceRestoreRequested(object? sender, string instanceId)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await _shell.RestoreArchivedInstanceAsync(instanceId);
            }
            catch (Exception ex)
            {
                await _services.Dialog.ShowErrorAsync("Could not restore account", ex.Message);
            }
        });
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!ApplicationLifecycleService.ShouldHideOnClose(
                _forceShutdown,
                _services.AppSettings.Settings.RunInBackgroundOnClose))
        {
            return;
        }

        args.Cancel = true;
        ApplicationLifecycleService.FlushPersistentStateFireAndForget();
        sender.Hide();
        _shell.Chrome.IsAppInForeground = false;
    }

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        ApplicationLifecycleService.TryShutdownOnWindowClosed(_forceShutdown, _services.AppSettings.Settings.RunInBackgroundOnClose);
        DetachShellHandlers();
        _services.GlobalHotkey.Dispose();
        _services.SystemTray.Dispose();
    }

    private void OnGlobalCopilotHotkey(object? sender, EventArgs e) =>
        _ = _services.HotkeyCopilot.TryRunCopilotAsync();

    private void OnAdapterStaleDetected(object? sender, string instanceId)
    {
        if (!_services.AdapterHealth.TryBeginRecovery(instanceId))
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
                _services.AdapterHealth.EndRecovery(instanceId);
            }
        });
    }

    private async void OnInstanceReorderRequested(object? sender, (string SourceInstanceId, string TargetInstanceId) args)
    {
        try
        {
            await _services.Registry.ReorderInstanceBeforeAsync(args.SourceInstanceId, args.TargetInstanceId);
            _shell.Chrome.RebuildInstanceNavigation();
            _shell.Chrome.UpdateShellChromeSelection();
        }
        catch (Exception ex)
        {
            await _services.Dialog.ShowErrorAsync("Could not reorder instance", ex.Message);
        }
    }

    private void OnNotificationHubChanged(object? sender, NotificationHubChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _shell.OnNotificationHubChanged(e));

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

            if (!_shell.Chrome.NotificationPanelVisible)
            {
                _shell.Chrome.SetNotificationPanelVisible(true);
            }
        });
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
                _shell.Navigation.RefreshDashboardIfVisible();
                return;
            }

            RefreshAdapterHealthIndicators();
        });
    }

    private void OnSessionInitializing(object? sender, InstanceSessionEventArgs e)
    {
        _services.ConnectionStatus.SetInitializing(e.Instance.Id, "Starting session");
        e.Instance.Status = InstanceConnectionStatus.Initializing;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_shell.IsTrackingStartupWarm)
            {
                _startupWarmCompleted++;
                _shellViewModel.ReportStartupWarmProgress(
                    _startupWarmCompleted,
                    _shellViewModel.StartupWarmTotal,
                    e.Instance.DisplayName);
                _shell.Navigation.ApplyInstanceLoadingUi();
            }

            WorkspaceSidebar.UpdateInstanceHealth(e.Instance.Id, e.Instance);
            _shell.Navigation.RefreshDashboardIfVisible();
        });
    }

    private void OnSessionFailed(object? sender, InstanceSessionErrorEventArgs e)
    {
        _services.ConnectionStatus.SetError(e.Instance.Id, e.Error.Message);
        e.Instance.Status = InstanceConnectionStatus.Error;
        DispatcherQueue.TryEnqueue(() =>
        {
            WorkspaceSidebar.UpdateInstanceHealth(e.Instance.Id, e.Instance);
            _shell.Navigation.RefreshDashboardIfVisible();
        });
    }

    private void RefreshAdapterHealthIndicators()
    {
        foreach (var instance in _services.Registry.Instances)
        {
            _services.ConnectionStatus.MirrorStatusToInstance(instance);
            WorkspaceSidebar.UpdateInstanceHealth(instance.Id, instance);
        }

        _shell.Navigation.RefreshDashboardIfVisible();
    }

    private void OnAppSettingsChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ThemeService.Apply(_services.AppSettings.Settings.ThemePreference);
            _shell.Chrome.ApplyNotificationPanelDockLayout();
            _ = _services.SessionManager.BroadcastAdapterSettingsAsync();
        });
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
            _ = ClearAutoDraftSubtitleAsync(e.InstanceId);
        });
    }

    private async Task ClearAutoDraftSubtitleAsync(string instanceId)
    {
        await Task.Delay(TimeSpan.FromSeconds(8)).ConfigureAwait(true);
        DispatcherQueue.TryEnqueue(() =>
        {
            if (AppTitleBar.Subtitle == "AI draft ready — review before sending" &&
                _services.Registry.FindById(instanceId) is { } instance)
            {
                AppTitleBar.Subtitle = instance.DisplayName;
            }
        });
    }

    public void ShowNotificationPanel() => _shell.Chrome.SetNotificationPanelVisible(true);

    private void NotificationToggleButton_Click(object sender, RoutedEventArgs e) =>
        _shell.Chrome.SetNotificationPanelVisible(!_shell.Chrome.NotificationPanelVisible);

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        if (SidebarColumn.Width.Value > 0)
        {
            SidebarColumn.Width = new GridLength(0);
            _shell.Chrome.ApplySidebarLayout();
        }
        else
        {
            _shell.Chrome.SidebarHoverExpanded = _shell.Chrome.PanePinned;
            _shell.Chrome.ApplySidebarLayout(forceVisible: true);
        }
    }

    private async void PanePinButton_Click(object sender, RoutedEventArgs e)
    {
        _shell.Chrome.PanePinned = !_shell.Chrome.PanePinned;
        _shell.Chrome.SidebarHoverExpanded = _shell.Chrome.PanePinned;
        _shell.ApplyPanePinUi(PanePinButton, PanePinIcon);
        _shell.Chrome.ApplySidebarLayout(forceVisible: true);

        try
        {
            await _services.AppSettings.UpdateAsync(settings =>
                settings.SidebarPinnedExpanded = _shell.Chrome.PanePinned);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not persist sidebar pin: {ex.Message}");
        }
    }

    private void SidebarHost_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_shell.Chrome.PanePinned || SidebarColumn.Width.Value <= 0)
        {
            return;
        }

        _shell.Chrome.SidebarHoverExpanded = true;
        _shell.Chrome.ApplySidebarLayout(forceVisible: true);
    }

    private void SidebarHost_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_shell.Chrome.PanePinned || SidebarColumn.Width.Value <= 0)
        {
            return;
        }

        _shell.Chrome.SidebarHoverExpanded = false;
        _shell.Chrome.ApplySidebarLayout(forceVisible: true);
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        _shell.Chrome.IsAppInForeground = MainWindowShellLayout.IsAppInForeground(
            AppWindow.IsVisible,
            args.WindowActivationState != WindowActivationState.Deactivated);
        _shell.ApplyWindowVisibilityState();
        _shell.OnForegroundStateChanged();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidVisibilityChange)
        {
            return;
        }

        _shell.Chrome.IsAppInForeground = MainWindowShellLayout.IsAppInForeground(
            sender.IsVisible,
            _shell.Chrome.IsAppInForeground);
        _shell.ApplyWindowVisibilityState();
        _shell.OnForegroundStateChanged();
    }
}
