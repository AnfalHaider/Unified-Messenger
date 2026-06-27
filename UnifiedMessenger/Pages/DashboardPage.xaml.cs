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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PersonalOverviewPanel.ApplyAccessibilityTabOrder();

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

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;

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

        PersonalOverviewPanel.Refresh(PersonalInstances);
        CommandCenterPanel.Render();
    }

    private IEnumerable<MessengerInstance> PersonalInstances =>
        _registry?.Instances.Where(i => !i.IsProfessional) ?? [];
}
