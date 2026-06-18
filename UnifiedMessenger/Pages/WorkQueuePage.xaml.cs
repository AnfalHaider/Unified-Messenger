using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

public sealed partial class WorkQueuePage : Page
{
    private ApplicationServices _services = ApplicationServiceProvider.Current;
    private IInstanceRegistryService? _registry;
    private DispatcherTimer? _resourceTimer;

    public WorkQueuePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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

            OccPanel.ConfigureServices(_services);
        }

        _ = RefreshOccAsync();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        OccPanel.ApplyAccessibilityTabOrder();

        _services.DashboardRefresh.Attach(DispatcherQueue);
        _services.DashboardRefresh.Subscribe();
        _services.DashboardRefresh.RefreshRequested += OnCoordinatorRefreshRequested;

        OccPanel.HeaderStatusChanged += OnOccHeaderStatusChanged;
        UpdateOccHeaderStatus();

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

        _services.DashboardRefresh.RefreshRequested -= OnCoordinatorRefreshRequested;
        _services.DashboardRefresh.Unsubscribe();

        OccPanel.HeaderStatusChanged -= OnOccHeaderStatusChanged;

        if (_resourceTimer is not null)
        {
            _resourceTimer.Tick -= OnResourceTimerTick;
            _resourceTimer.Stop();
            _resourceTimer = null;
        }
    }

    private void OnResourceTimerTick(object? sender, object e) =>
        _ = RefreshOccAsync();

    private void OnCoordinatorRefreshRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => _ = RefreshOccAsync());
    }

    private void OnOccHeaderStatusChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(UpdateOccHeaderStatus);

    private void UpdateOccHeaderStatus()
    {
        OccStatusRow.Visibility = Visibility.Visible;

        var lastRefreshed = OccPanel.HeaderLastRefreshedText;
        LastRefreshedText.Text = lastRefreshed;
        LastRefreshedText.Visibility = string.IsNullOrWhiteSpace(lastRefreshed)
            ? Visibility.Collapsed
            : Visibility.Visible;

        var showBackfill = OccPanel.HeaderShowBackfillStatus;
        BackfillStatusPanel.Visibility = showBackfill ? Visibility.Visible : Visibility.Collapsed;
        if (showBackfill)
        {
            BackfillStatusText.Text = OccPanel.HeaderBackfillStatusText;
        }

        OccStatusSeparator.Visibility =
            LastRefreshedText.Visibility == Visibility.Visible && showBackfill
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    public void SelectWorkspaceBranch(string? branchKey, bool forceRefresh = false) =>
        OccPanel.SelectWorkspaceBranch(branchKey, forceRefresh);

    public void RequestImmediateLaneFocus() =>
        OccPanel.RequestImmediateLaneFocus();

    public void SelectUrgentQueueFilter() =>
        OccPanel.SelectUrgentQueueFilter();

    private async Task RefreshOccAsync()
    {
        if (_registry is null)
        {
            return;
        }

        await OccPanel.RefreshAsync(ProfessionalInstances, _registry).ConfigureAwait(true);
    }

    private IEnumerable<MessengerInstance> ProfessionalInstances =>
        _registry?.Instances.Where(i => i.IsProfessional) ?? [];
}
