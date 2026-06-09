using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Pages;

public sealed partial class DashboardPage : Page
{
    private ApplicationServices _services = new();
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
        DashboardRefreshCoordinator.Instance.Attach(DispatcherQueue);
        DashboardRefreshCoordinator.Instance.Subscribe();
        DashboardRefreshCoordinator.Instance.RefreshRequested += OnCoordinatorRefreshRequested;

        AdapterHealthMonitor.Instance.Changed += OnPersonalDataChanged;
        InstanceConnectionStatusService.Instance.Changed += OnPersonalDataChanged;

        _dashboardTabSelectionCallbackToken = DashboardTabs.RegisterPropertyChangedCallback(
            TabView.SelectedIndexProperty,
            OnDashboardTabSelectionChanged);

        _resourceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(DashboardPageHelper.ResourceRefreshIntervalSeconds)
        };
        _resourceTimer.Tick += OnResourceTimerTick;
        _resourceTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;

        DashboardRefreshCoordinator.Instance.RefreshRequested -= OnCoordinatorRefreshRequested;
        DashboardRefreshCoordinator.Instance.Unsubscribe();

        AdapterHealthMonitor.Instance.Changed -= OnPersonalDataChanged;
        InstanceConnectionStatusService.Instance.Changed -= OnPersonalDataChanged;

        DashboardTabs.UnregisterPropertyChangedCallback(
            TabView.SelectedIndexProperty,
            _dashboardTabSelectionCallbackToken);

        if (_resourceTimer is not null)
        {
            _resourceTimer.Tick -= OnResourceTimerTick;
            _resourceTimer.Stop();
            _resourceTimer = null;
        }
    }

    private void OnDashboardTabSelectionChanged(DependencyObject sender, DependencyProperty args)
    {
        if (!IsOperationsCommandCenterTabSelected || _registry is null)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() => _ = RefreshOperationsCommandCenterAsync());
    }

    private void OnCoordinatorRefreshRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            PersonalOverviewPanel.ScheduleRefresh(PersonalInstances);
            _ = RefreshOperationsCommandCenterIfVisibleAsync();
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
        ScheduleBackfillRetryIfNeeded();
        _ = RefreshOperationsCommandCenterAsync();
    }

    private async Task RefreshOperationsCommandCenterIfVisibleAsync()
    {
        if (!IsOperationsCommandCenterTabSelected)
        {
            return;
        }

        await RefreshOperationsCommandCenterAsync().ConfigureAwait(true);
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

    private void ScheduleBackfillRetryIfNeeded()
    {
        if (!_services.AppSettings.Settings.EnableStartupBackfill)
        {
            return;
        }

        foreach (var instance in ProfessionalInstances)
        {
            var state = BackfillSyncManager.Instance.GetState(instance.Id);
            if (state is BackfillSyncState.NotStarted or BackfillSyncState.Failed or BackfillSyncState.Skipped)
            {
                BackfillSyncManager.Instance.Schedule(instance);
            }
        }
    }

    private IEnumerable<MessengerInstance> ProfessionalInstances =>
        _registry?.Instances.Where(i => i.IsProfessional) ?? [];

    private IEnumerable<MessengerInstance> PersonalInstances =>
        _registry?.Instances.Where(i => !i.IsProfessional) ?? [];
}
