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
    private bool _suppressScopeSelectorChange;
    private bool _suppressAiToggleChange;

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
    HyperlinkButton IShellUiHost.BackToDashboardButton => BackToDashboardButton;
    void IShellUiHost.ActivateWindow() => Activate();
    void IShellUiHost.ShowAppWindow() => AppWindow.Show();
    public MainWindow()
    {
        InitializeComponent();
        UiThreadRunner.Register(DispatcherQueue);
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
        WorkspaceSidebar.ScopeSelectorStateChanged += OnScopeSelectorStateChanged;
        WorkspaceSidebar.Loaded += (_, _) =>
        {
            _shell.Chrome.RebuildInstanceNavigation();
            SyncScopeSelector();
            SyncAiToggle();
        };

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
        SecondInstanceActivator.StartServer(() => DispatcherQueue.TryEnqueue(ShowFromTray));
    }

    public async Task RunInitializationAsync()
    {
        await UiThreadRunner.RunAsync(async () =>
        {
            await _shell.InitializeAsync().ConfigureAwait(true);
            _shell.ApplyPanePinUi(PanePinButton, PanePinIcon);
        }).ConfigureAwait(true);
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
        await ApplicationLifecycleService.ShutdownAsync().ConfigureAwait(true);
        _services.SystemTray.Dispose();
        Close();
    }

    private void AttachShellHandlers()
    {
        var nav = _services.Navigation;
        nav.InstanceNavigationRequested += OnInstanceNavigationRequested;
        nav.DashboardRefreshRequested += OnDashboardRefreshRequested;
        nav.ArchivedInstanceRestoreRequested += OnArchivedInstanceRestoreRequested;
        nav.LayoutRefreshRequested += OnLayoutRefreshRequested;
        nav.InstanceRegistryRefreshRequested += OnInstanceRegistryRefreshRequested;
        nav.AddInstanceRequested += OnAddInstanceRequested;
        nav.SettingsOpenRequested += OnSettingsOpenRequested;

        _services.NotificationHub.Changed += OnNotificationHubChanged;
        _services.AppNotification.ActivationRequested += OnToastActivationRequested;
        _services.AdapterHealth.Changed += OnAdapterHealthChanged;
        _services.AdapterHealth.AdapterStaleDetected += OnAdapterStaleDetected;
        _services.ConnectionStatus.Changed += OnConnectionStatusChanged;
        _services.SessionManager.SessionInitializing += OnSessionInitializing;
        _services.SessionManager.SessionFailed += OnSessionFailed;
        _services.MessageAnalytics.Changed += OnMessageAnalyticsChanged;
        _services.AppSettings.Changed += OnAppSettingsChanged;
    }

    private void OnAddInstanceRequested(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _ = _shell.ShowAddInstanceDialogAsync());

    private void OnSettingsOpenRequested(object? sender, string? sectionKey) =>
        DispatcherQueue.TryEnqueue(() => _ = _shell.Navigation.ShowSettingsAsync(sectionKey));

    private void OnInstanceNavigationRequested(object? sender, InstanceNavigationRequest request) =>
        DispatcherQueue.TryEnqueue(() => _ = _shell.Navigation.NavigateToInstanceAsync(request));

    private void OnDashboardRefreshRequested(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(_shell.Navigation.RefreshDashboardIfVisible);

    private void OnLayoutRefreshRequested(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(_shell.Chrome.ApplyNotificationPanelDockLayout);

    private void OnInstanceRegistryRefreshRequested(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            _shell.Chrome.RebuildInstanceNavigation();
            _shell.Navigation.RefreshDashboardIfVisible();
        });

    private void OnAdapterHealthChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshAdapterHealthIndicators);

    private void OnMessageAnalyticsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(_shell.Navigation.RefreshDashboardIfVisible);

    private void DetachShellHandlers()
    {
        var nav = _services.Navigation;
        nav.InstanceNavigationRequested -= OnInstanceNavigationRequested;
        nav.DashboardRefreshRequested -= OnDashboardRefreshRequested;
        nav.ArchivedInstanceRestoreRequested -= OnArchivedInstanceRestoreRequested;
        nav.LayoutRefreshRequested -= OnLayoutRefreshRequested;
        nav.InstanceRegistryRefreshRequested -= OnInstanceRegistryRefreshRequested;
        nav.AddInstanceRequested -= OnAddInstanceRequested;
        nav.SettingsOpenRequested -= OnSettingsOpenRequested;

        _services.NotificationHub.Changed -= OnNotificationHubChanged;
        _services.AppNotification.ActivationRequested -= OnToastActivationRequested;
        _services.AdapterHealth.Changed -= OnAdapterHealthChanged;
        _services.AdapterHealth.AdapterStaleDetected -= OnAdapterStaleDetected;
        _services.ConnectionStatus.Changed -= OnConnectionStatusChanged;
        _services.SessionManager.SessionInitializing -= OnSessionInitializing;
        _services.SessionManager.SessionFailed -= OnSessionFailed;
        _services.MessageAnalytics.Changed -= OnMessageAnalyticsChanged;
        _services.AppSettings.Changed -= OnAppSettingsChanged;
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
        TryEnqueueSafe(async () =>
        {
            try
            {
                await _shell.RestoreArchivedInstanceAsync(instanceId);
            }
            catch (Exception ex)
            {
                await _services.Dialog.ShowErrorAsync("Could not restore account", ex.Message);
            }
        }, nameof(OnArchivedInstanceRestoreRequested));
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
        SecondInstanceActivator.StopServer();
        ApplicationLifecycleService.TryShutdownOnWindowClosed(_forceShutdown, _services.AppSettings.Settings.RunInBackgroundOnClose);
        DetachShellHandlers();
        _services.SystemTray.Dispose();
    }

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
                _services.ConnectionStatus.MirrorStatusToInstance(instance);
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

        AppLogger.LogError($"Session.{e.Instance.Id}", e.Error);

        if (!_shell.IsTrackingStartupWarm &&
            _services.AppSettings.Settings.EnableBackgroundToasts)
        {
            _services.AppNotification.ShowInfoToast(
                $"{e.Instance.DisplayName} — Connection failed",
                e.Error.Message,
                e.Instance.Id);
        }

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
            SyncAiToggle();
            _ = _services.SessionManager.BroadcastAdapterSettingsAsync();
        });
    }

    // ── Title-bar scope selector (drives WorkspaceSidebar.SetScope) ───────────────────────────
    private void OnScopeSelectorStateChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(SyncScopeSelector);

    private void SyncScopeSelector()
    {
        ScopeSelector.Visibility = WorkspaceSidebar.ShouldShowScopeSelector
            ? Visibility.Visible
            : Visibility.Collapsed;

        var targetIndex = WorkspaceSidebar.Scope switch
        {
            SidebarScope.Professional => 1,
            SidebarScope.Personal => 2,
            _ => 0
        };

        if (ScopeSelector.SelectedIndex == targetIndex)
        {
            return;
        }

        _suppressScopeSelectorChange = true;
        ScopeSelector.SelectedIndex = targetIndex;
        _suppressScopeSelectorChange = false;
    }

    private void ScopeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressScopeSelectorChange)
        {
            return;
        }

        var scope = ((ScopeSelector.SelectedItem as ComboBoxItem)?.Tag as string) switch
        {
            "Professional" => SidebarScope.Professional,
            "Personal" => SidebarScope.Personal,
            _ => SidebarScope.All
        };
        WorkspaceSidebar.SetScope(scope);
    }

    // ── Title-bar AI toggle (mirrors AppSettings.EnableLocalAi) ───────────────────────────────
    private void SyncAiToggle()
    {
        var enabled = _services.AppSettings.Settings.EnableLocalAi;
        if (AiToggleButton.IsChecked == enabled)
        {
            return;
        }

        _suppressAiToggleChange = true;
        AiToggleButton.IsChecked = enabled;
        _suppressAiToggleChange = false;
    }

    private void AiToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressAiToggleChange)
        {
            return;
        }

        var enabled = AiToggleButton.IsChecked == true;
        _ = _services.AppSettings.UpdateAsync(s => s.EnableLocalAi = enabled);
    }

    private void TryEnqueueSafe(Func<Task> asyncAction, string operationName = "")
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await asyncAction();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] TryEnqueueSafe error in '{operationName}': {ex}");
            }
        });
    }

    private void BackToDashboardButton_Click(object sender, RoutedEventArgs e) =>
        _ = _shell.Navigation.ShowDashboardAsync();

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
}
