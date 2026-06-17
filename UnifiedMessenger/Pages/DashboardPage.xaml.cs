using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

public sealed partial class DashboardPage : Page
{
    private ApplicationServices _services = ApplicationServiceProvider.Current;
    private IInstanceRegistryService? _registry;
    private DispatcherTimer? _resourceTimer;
    private long _dashboardTabSelectionCallbackToken;

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private bool IsOperationsCommandCenterTabSelected => DashboardTabs.SelectedIndex == 0;

    private void OnResourceTimerTick(object? sender, object e)
    {
        _services.ThreadRegistry.RefreshOperationalFlags(raiseChanged: false);
        PersonalOverviewPanel.ScheduleRefresh(PersonalInstances);
        if (IsOperationsCommandCenterTabSelected)
        {
            _ = RefreshOperationsCommandCenterAsync();
        }
        else if (_registry is not null)
        {
            _ = OperationsCommandCenterPanel.RefreshLightAsync(ProfessionalInstances, _registry);
        }
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

            OperationsCommandCenterPanel.ConfigureServices(_services);
            PersonalOverviewPanel.ConfigureServices(_services);
        }

        RefreshAll();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AccessibilityTabOrderHelper.ApplyTabIndex(DashboardTabs, AccessibilityTabOrderHelper.DashboardTabs);
        OperationsCommandCenterPanel.ApplyAccessibilityTabOrder();
        PersonalOverviewPanel.ApplyAccessibilityTabOrder();

        _services.DashboardRefresh.Attach(DispatcherQueue);
        _services.DashboardRefresh.Subscribe();
        _services.DashboardRefresh.RefreshRequested += OnCoordinatorRefreshRequested;

        var navigation = _services.Navigation;
        navigation.OccBranchFilterRequested += OnOccBranchFilterRequested;
        navigation.OccImmediateLaneFocusRequested += OnOccImmediateLaneFocusRequested;
        navigation.OccUrgentQueueFilterRequested += OnOccUrgentQueueFilterRequested;

        _services.AdapterHealth.Changed += OnPersonalDataChanged;
        _services.ConnectionStatus.Changed += OnPersonalDataChanged;

        _dashboardTabSelectionCallbackToken = DashboardTabs.RegisterPropertyChangedCallback(
            TabView.SelectedIndexProperty,
            OnDashboardTabSelectionChanged);

        OperationsCommandCenterPanel.HeaderStatusChanged += OnOccHeaderStatusChanged;
        UpdateDashboardOccHeaderStatus();

        _resourceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(DashboardPageHelper.ResourceRefreshIntervalSeconds)
        };
        _resourceTimer.Tick += OnResourceTimerTick;
        _resourceTimer.Start();

        // Land on the oversight command center by default — it's the L0 "who's waiting, where" home.
        DashboardTabs.SelectedItem = CommandCenterTab;
    }

    private bool IsCommandCenterTabSelected => ReferenceEquals(DashboardTabs.SelectedItem, CommandCenterTab);

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;

        _services.DashboardRefresh.RefreshRequested -= OnCoordinatorRefreshRequested;
        _services.DashboardRefresh.Unsubscribe();

        var navigation = _services.Navigation;
        navigation.OccBranchFilterRequested -= OnOccBranchFilterRequested;
        navigation.OccImmediateLaneFocusRequested -= OnOccImmediateLaneFocusRequested;
        navigation.OccUrgentQueueFilterRequested -= OnOccUrgentQueueFilterRequested;

        _services.AdapterHealth.Changed -= OnPersonalDataChanged;
        _services.ConnectionStatus.Changed -= OnPersonalDataChanged;

        DashboardTabs.UnregisterPropertyChangedCallback(
            TabView.SelectedIndexProperty,
            _dashboardTabSelectionCallbackToken);

        OperationsCommandCenterPanel.HeaderStatusChanged -= OnOccHeaderStatusChanged;

        if (_resourceTimer is not null)
        {
            _resourceTimer.Tick -= OnResourceTimerTick;
            _resourceTimer.Stop();
            _resourceTimer = null;
        }
    }

    private void OnDashboardTabSelectionChanged(DependencyObject sender, DependencyProperty args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateDashboardOccHeaderStatus();
            if (IsCommandCenterTabSelected)
            {
                CommandCenterPanel.Render();
                return;
            }

            if (!IsOperationsCommandCenterTabSelected || _registry is null)
            {
                return;
            }

            _ = RefreshOperationsCommandCenterAsync();
        });
    }

    private void OnOccHeaderStatusChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdateDashboardOccHeaderStatus);

    private void UpdateDashboardOccHeaderStatus()
    {
        var showOccStatus = IsOperationsCommandCenterTabSelected;
        DashboardOccStatusRow.Visibility = showOccStatus ? Visibility.Visible : Visibility.Collapsed;
        if (!showOccStatus)
        {
            return;
        }

        var lastRefreshed = OperationsCommandCenterPanel.HeaderLastRefreshedText;
        DashboardLastRefreshedText.Text = lastRefreshed;
        DashboardLastRefreshedText.Visibility = string.IsNullOrWhiteSpace(lastRefreshed)
            ? Visibility.Collapsed
            : Visibility.Visible;

        var showBackfill = OperationsCommandCenterPanel.HeaderShowBackfillStatus;
        DashboardBackfillStatusPanel.Visibility = showBackfill ? Visibility.Visible : Visibility.Collapsed;
        if (showBackfill)
        {
            DashboardBackfillStatusText.Text = OperationsCommandCenterPanel.HeaderBackfillStatusText;
        }

        DashboardOccStatusSeparator.Visibility =
            DashboardLastRefreshedText.Visibility == Visibility.Visible &&
            showBackfill
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void OnOccBranchFilterRequested(object? sender, string? branchKey)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            DashboardTabs.SelectedIndex = 0;
            OperationsCommandCenterPanel.SelectWorkspaceBranch(branchKey, forceRefresh: true);
        });
    }

    private void OnOccImmediateLaneFocusRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            DashboardTabs.SelectedIndex = 0;
            OperationsCommandCenterPanel.RequestImmediateLaneFocus();
        });
    }

    private void OnOccUrgentQueueFilterRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            DashboardTabs.SelectedIndex = 0;
            OperationsCommandCenterPanel.SelectUrgentQueueFilter();
        });
    }

    private void OnCoordinatorRefreshRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            PersonalOverviewPanel.ScheduleRefresh(PersonalInstances);
            if (IsOperationsCommandCenterTabSelected)
            {
                _ = RefreshOperationsCommandCenterAsync();
            }
            else if (_registry is not null)
            {
                _ = OperationsCommandCenterPanel.RefreshLightAsync(ProfessionalInstances, _registry);
            }
        });
    }

    private void OnPersonalDataChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => PersonalOverviewPanel.ScheduleRefresh(PersonalInstances));

    public void RefreshAll()
    {
        if (_registry is null)
        {
            WelcomeSubtitle.Text = "Add an account to start receiving unified notifications.";
            return;
        }

        var professionalCount = _registry.Instances.Count(i => i.IsProfessional);
        var personalCount = _registry.Instances.Count - professionalCount;

        WelcomeSubtitle.Text = DashboardPageHelper.BuildWelcomeSubtitle(professionalCount, personalCount);

        PersonalOverviewPanel.Refresh(PersonalInstances);
        _ = RefreshOperationsCommandCenterAsync();
    }

    private async Task RefreshOperationsCommandCenterAsync()
    {
        if (_registry is null)
        {
            return;
        }

        await OperationsCommandCenterPanel.RefreshAsync(
            ProfessionalInstances,
            _registry).ConfigureAwait(true);
    }

    private IEnumerable<MessengerInstance> ProfessionalInstances =>
        _registry?.Instances.Where(i => i.IsProfessional) ?? [];

    private IEnumerable<MessengerInstance> PersonalInstances =>
        _registry?.Instances.Where(i => !i.IsProfessional) ?? [];
}


