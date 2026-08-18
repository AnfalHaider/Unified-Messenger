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
        SectionLinks.Render();
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
            SectionLinks.ConfigureServices(_services);
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
        CommandCenterPanel.DashboardActivityRequested += OnDashboardActivityRequested;

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

    // A KPI tile (Busiest window / Messages per day) asked to open the activity graph. It lives on the
    // Analytics page now — the dashboard no longer keeps a second copy of it — so this navigates there
    // rather than scrolling to a panel that isn't on this page any more.
    private void OnDashboardActivityRequested(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => _services.Navigation.RequestSection(ShellSection.Analytics));

    /// <summary>
    /// The dashboard's single Re-sync action: re-read oversight history, then refresh the Google review
    /// snapshots the section-links card reports on. One button drives both.
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
            SectionLinks.Render();
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
        CommandCenterPanel.DashboardActivityRequested -= OnDashboardActivityRequested;

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
            SectionLinks.Render();
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
        var greeting = hour switch
        {
            < 12 => "Good morning",
            < 17 => "Good afternoon",
            < 21 => "Good evening",
            _ => "Good evening"
        };

        // A first-time user has nothing to be welcomed BACK to. The seeded default account does not count
        // as having been here before — it is created by the app, not by the owner.
        WelcomeTitle.Text = DashboardPageHelper.HasOnlySeededDefaultAccount(_registry?.Instances)
            ? "Welcome to Unified Messenger"
            : greeting;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(WelcomeTitle, WelcomeTitle.Text);

        if (_registry is null)
        {
            WelcomeSubtitle.Text = "Add an account to start receiving unified notifications.";
            return;
        }

        // "Add an account to start receiving unified notifications" is the single worst thing this page can
        // say to an owner whose accounts simply could not be read — it asserts the opposite of the truth,
        // in a friendly voice. The greeting is suppressed too: there is nothing welcoming about this state.
        if (AccountsUnavailableNotice.ShouldShow(_registry.LoadOutcome))
        {
            WelcomeTitle.Text = AccountsUnavailableNotice.Title;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(WelcomeTitle, WelcomeTitle.Text);
            WelcomeSubtitle.Text = AccountsUnavailableNotice.DashboardSubtitle;
            PersonalButton.Visibility = Visibility.Collapsed;
            CommandCenterPanel.Render();
            SectionLinks.Render();
            return;
        }

        var professionalCount = _registry.Instances.Count(i => i.IsProfessional);
        var personalCount = _registry.Instances.Count - professionalCount;

        WelcomeSubtitle.Text = DashboardPageHelper.HasOnlySeededDefaultAccount(_registry.Instances)
            ? "Add an account to start receiving unified notifications."
            : DashboardPageHelper.BuildWelcomeSubtitle(professionalCount, personalCount);

        // The "Personal" top button shows the personal-account count and hides when there are none.
        PersonalButtonLabel.Text = personalCount > 0 ? $"Personal · {personalCount}" : "Personal";
        PersonalButton.Visibility = personalCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        PersonalOverviewPanel.Refresh(PersonalInstances);
        CommandCenterPanel.Render();
        SectionLinks.Render();
    }

    // Refresh the personal overview each time its flyout opens so it's current when viewed.
    private void PersonalFlyout_Opened(object sender, object e) =>
        PersonalOverviewPanel.Refresh(PersonalInstances);

    private IEnumerable<MessengerInstance> PersonalInstances =>
        _registry?.Instances.Where(i => !i.IsProfessional) ?? [];
}
