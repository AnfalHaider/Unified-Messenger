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

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnResourceTimerTick(object? sender, object e)
    {
        _services.ThreadRegistry.RefreshOperationalFlags(raiseChanged: false);
        PersonalOverviewPanel.ScheduleRefresh(PersonalInstances);
        CommandCenterPanel.Render();
        ActivityPatternsPanel.Render();
        ReviewHealthPanel.Render();
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

            PersonalOverviewPanel.ConfigureServices(_services);
            ActivityPatternsPanel.ConfigureServices(_services);
            ReviewHealthPanel.ConfigureServices(_services);
        }

        if (_registry is not null)
        {
            OversightAlertMonitor.Instance.Start(_registry, DispatcherQueue);
        }

        RefreshAll();
    }

    private bool _dashboardResyncRunning;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PersonalOverviewPanel.ApplyAccessibilityTabOrder();

        // Single dashboard-wide Re-sync: the command center raises the request; we orchestrate the full
        // refresh (oversight history + activity graph + Google reviews) so there's one button, not three.
        CommandCenterPanel.DashboardResyncRequested += OnDashboardResyncRequested;

        _services.DashboardRefresh.Attach(DispatcherQueue);
        _services.DashboardRefresh.Subscribe();
        _services.DashboardRefresh.RefreshRequested += OnCoordinatorRefreshRequested;

        _services.AdapterHealth.Changed += OnPersonalDataChanged;
        _services.ConnectionStatus.Changed += OnPersonalDataChanged;

        _resourceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(DashboardPageHelper.ResourceRefreshIntervalSeconds)
        };
        _resourceTimer.Tick += OnResourceTimerTick;
        _resourceTimer.Start();

        CommandCenterPanel.Render();
    }

    private void OnDashboardResyncRequested(object? sender, EventArgs e) => _ = RunDashboardResyncAsync();

    /// <summary>
    /// The dashboard's single Re-sync action: re-read oversight history, then refresh the activity graph and
    /// Google reviews. One button drives all three (the per-section Refresh/Refresh-reviews buttons were removed).
    /// </summary>
    private async Task RunDashboardResyncAsync()
    {
        if (_dashboardResyncRunning)
        {
            return;
        }

        _dashboardResyncRunning = true;
        try
        {
            await CommandCenterPanel.RunResyncAsync();
            await ReviewHealthPanel.RefreshAsync();
            ActivityPatternsPanel.Render();
        }
        finally
        {
            _dashboardResyncRunning = false;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        CommandCenterPanel.DashboardResyncRequested -= OnDashboardResyncRequested;

        _services.DashboardRefresh.RefreshRequested -= OnCoordinatorRefreshRequested;
        _services.DashboardRefresh.Unsubscribe();

        _services.AdapterHealth.Changed -= OnPersonalDataChanged;
        _services.ConnectionStatus.Changed -= OnPersonalDataChanged;

        if (_resourceTimer is not null)
        {
            _resourceTimer.Tick -= OnResourceTimerTick;
            _resourceTimer.Stop();
            _resourceTimer = null;
        }
    }

    private void OnCoordinatorRefreshRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            PersonalOverviewPanel.ScheduleRefresh(PersonalInstances);
            CommandCenterPanel.Render();
        });
    }

    private void OnPersonalDataChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => PersonalOverviewPanel.ScheduleRefresh(PersonalInstances));

    /// <summary>Forces the command center to redraw (e.g. after an account avatar icon changed), bypassing
    /// the data-signature guard that would otherwise skip an icon-only change.</summary>
    public void ForceRefreshIcons() => CommandCenterPanel.ForceRender();

    public void RefreshAll()
    {
        var hour = DateTime.Now.Hour;
        WelcomeTitle.Text = hour switch
        {
            < 12 => "Good morning",
            < 17 => "Good afternoon",
            < 21 => "Good evening",
            _ => "Welcome back"
        };

        if (_registry is null)
        {
            WelcomeSubtitle.Text = "Add an account to start receiving unified notifications.";
            return;
        }

        var professionalCount = _registry.Instances.Count(i => i.IsProfessional);
        var personalCount = _registry.Instances.Count - professionalCount;

        WelcomeSubtitle.Text = DashboardPageHelper.BuildWelcomeSubtitle(professionalCount, personalCount);

        // The "Personal" top button shows the personal-account count and hides when there are none.
        PersonalButtonLabel.Text = personalCount > 0 ? $"Personal · {personalCount}" : "Personal";
        PersonalButton.Visibility = personalCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        PersonalOverviewPanel.Refresh(PersonalInstances);
        CommandCenterPanel.Render();
    }

    // Refresh the personal overview each time its flyout opens so it's current when viewed.
    private void PersonalFlyout_Opened(object sender, object e) =>
        PersonalOverviewPanel.Refresh(PersonalInstances);

    private IEnumerable<MessengerInstance> PersonalInstances =>
        _registry?.Instances.Where(i => !i.IsProfessional) ?? [];
}
