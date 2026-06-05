using System.Collections.ObjectModel;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Windows.UI;

namespace UnifiedMessenger.Controls;

public sealed partial class UnifiedMessengerControlCenter : UserControl
{
    private const int RefreshDebounceMilliseconds = 450;

    private readonly ObservableCollection<BranchMetricViewModel> _branchMetrics = [];
    private readonly ObservableCollection<PlatformHealthViewModel> _platformHealth = [];
    private readonly ObservableCollection<ThreadCardViewModel> _immediateQueue = [];
    private readonly ObservableCollection<ThreadCardViewModel> _newInquiries = [];
    private readonly ObservableCollection<ThreadCardViewModel> _hangingLeads = [];
    private readonly ObservableCollection<ThreadCardViewModel> _resolved = [];

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _refreshDebounceTimer;

    private IEnumerable<MessengerInstance> _professionalInstances = [];
    private UnifiedMessengerDashboardSnapshot _snapshot = UnifiedMessengerDashboardSnapshot.Empty;
    private string? _branchInstanceId;
    private int _refreshGeneration;
    private bool _suppressTabSelection;
    private bool _isRefreshing;

    public UnifiedMessengerControlCenter()
    {
        InitializeComponent();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        BranchMetricsItems.ItemsSource = _branchMetrics;
        PlatformHealthItems.ItemsSource = _platformHealth;
        ImmediateQueueItems.ItemsSource = _immediateQueue;
        NewInquiriesItems.ItemsSource = _newInquiries;
        HangingLeadsItems.ItemsSource = _hangingLeads;
        ResolvedItems.ItemsSource = _resolved;

        _refreshDebounceTimer = _dispatcherQueue.CreateTimer();
        _refreshDebounceTimer.Interval = TimeSpan.FromMilliseconds(RefreshDebounceMilliseconds);
        _refreshDebounceTimer.Tick += OnRefreshDebounceTick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public async Task RefreshAsync(
        IEnumerable<MessengerInstance> professionalInstances,
        string? branchInstanceId = null)
    {
        _professionalInstances = professionalInstances;
        _branchInstanceId = branchInstanceId;

        if (_isRefreshing)
        {
            ScheduleDebouncedRefresh();
            return;
        }

        _isRefreshing = true;
        try
        {
            var snapshot = await Task.Run(() =>
                    UnifiedMessengerDashboardService.Instance.BuildSnapshot(professionalInstances, branchInstanceId))
                .ConfigureAwait(true);

            ApplySnapshot(snapshot);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        MessageTriageService.Instance.Changed += OnOperationalDataChanged;
        ThreadRegistryService.Instance.Changed += OnOperationalDataChanged;
        UnifiedMessengerDashboardService.Instance.Changed += OnOperationalDataChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        MessageTriageService.Instance.Changed -= OnOperationalDataChanged;
        ThreadRegistryService.Instance.Changed -= OnOperationalDataChanged;
        UnifiedMessengerDashboardService.Instance.Changed -= OnOperationalDataChanged;
        _refreshDebounceTimer.Stop();
    }

    private void OnOperationalDataChanged(object? sender, EventArgs e) =>
        ScheduleDebouncedRefresh();

    private void ScheduleDebouncedRefresh()
    {
        Interlocked.Increment(ref _refreshGeneration);
        _refreshDebounceTimer.Stop();
        _refreshDebounceTimer.Start();
    }

    private void OnRefreshDebounceTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        _ = RefreshAsync(_professionalInstances, _branchInstanceId);
    }

    private void BranchWorkspaceTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTabSelection)
        {
            return;
        }

        ApplyKanbanForSelectedBranch();
    }

    private void ApplySnapshot(UnifiedMessengerDashboardSnapshot snapshot)
    {
        _snapshot = snapshot;

        ScopeText.Text = UnifiedMessengerDashboardPresentationHelper.BuildScopeLabel(
            _branchInstanceId,
            snapshot.BranchNames);
        RevenueAtRiskText.Text = UnifiedMessengerDashboardPresentationHelper.FormatRevenue(snapshot.TotalRevenueAtRisk);
        OpenThreadCountText.Text = snapshot.OpenThreadCount.ToString();
        HangingLeadCountText.Text = snapshot.HangingLeadCount.ToString();

        ReplaceCollection(_branchMetrics, snapshot.BranchMetrics.Select(metric => new BranchMetricViewModel(metric)));
        ReplaceCollection(_platformHealth, snapshot.PlatformHealth.Select(indicator => new PlatformHealthViewModel(indicator)));
        ReplaceCollection(_immediateQueue, snapshot.ImmediateActionQueue.Select(thread => new ThreadCardViewModel(thread)));

        ImmediateQueueEmptyText.Visibility = _immediateQueue.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        RebuildBranchTabs(snapshot.BranchNames);
        ApplyKanbanForSelectedBranch();
    }

    private void RebuildBranchTabs(IReadOnlyList<string> branchNames)
    {
        _suppressTabSelection = true;
        BranchWorkspaceTabs.TabItems.Clear();

        if (branchNames.Count == 0)
        {
            BranchWorkspaceTabs.TabItems.Add(new TabViewItem
            {
                Header = "General",
                IsClosable = false,
                Tag = "General"
            });
        }
        else
        {
            foreach (var branch in branchNames)
            {
                BranchWorkspaceTabs.TabItems.Add(new TabViewItem
                {
                    Header = branch,
                    IsClosable = false,
                    Tag = branch
                });
            }
        }

        var selectedBranch = ResolveFilteredBranchName();
        var selectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(selectedBranch))
        {
            for (var index = 0; index < BranchWorkspaceTabs.TabItems.Count; index++)
            {
                if (BranchWorkspaceTabs.TabItems[index] is TabViewItem tab &&
                    tab.Tag is string branch &&
                    branch.Equals(selectedBranch, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = index;
                    break;
                }
            }
        }

        BranchWorkspaceTabs.SelectedIndex = selectedIndex;
        _suppressTabSelection = false;
    }

    private string? ResolveFilteredBranchName()
    {
        if (string.IsNullOrWhiteSpace(_branchInstanceId))
        {
            return null;
        }

        var instance = _professionalInstances.FirstOrDefault(item =>
            item.Id.Equals(_branchInstanceId, StringComparison.OrdinalIgnoreCase));

        return instance is null ? null : BranchNameResolver.Resolve(instance);
    }

    private void ApplyKanbanForSelectedBranch()
    {
        var branchName = BranchWorkspaceTabs.SelectedItem is TabViewItem tab
            ? tab.Tag as string
            : null;

        var threads = UnifiedMessengerDashboardPresentationHelper.FilterThreadsForBranch(_snapshot.AllThreads, branchName);

        ReplaceCollection(
            _newInquiries,
            threads.Where(thread => thread.KanbanColumn == UnifiedMessengerKanbanColumn.NewInquiries)
                .Select(thread => new ThreadCardViewModel(thread)));
        ReplaceCollection(
            _hangingLeads,
            threads.Where(thread => thread.KanbanColumn == UnifiedMessengerKanbanColumn.HangingLeads)
                .Select(thread => new ThreadCardViewModel(thread)));
        ReplaceCollection(
            _resolved,
            threads.Where(thread => thread.KanbanColumn == UnifiedMessengerKanbanColumn.Resolved)
                .Select(thread => new ThreadCardViewModel(thread)));
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private sealed class BranchMetricViewModel(UnifiedMessengerBranchMetrics metric)
    {
        public string BranchName { get; } = metric.BranchName;

        public string LatencyDisplay { get; } =
            UnifiedMessengerDashboardPresentationHelper.FormatLatency(metric.AverageLatencyMinutes);

        public string UnresolvedDisplay { get; } = $"{metric.UnresolvedCount} open";

        public string RevenueDisplay { get; } =
            metric.RevenueAtRisk <= 0
                ? "No revenue at risk"
                : $"{UnifiedMessengerDashboardPresentationHelper.FormatRevenue(metric.RevenueAtRisk)} at risk";

        public SolidColorBrush LatencyBrush { get; } =
            CreateBrush(UnifiedMessengerDashboardPresentationHelper.ResolveLatencyHex(metric.LatencyColor));

        public SolidColorBrush LatencyBorderBrush { get; } =
            CreateBrush(UnifiedMessengerDashboardPresentationHelper.ResolveLatencyHex(metric.LatencyColor));
    }

    private sealed class PlatformHealthViewModel(UnifiedMessengerPlatformHealthIndicator indicator)
    {
        public string Label { get; } = $"{indicator.DisplayName}: {indicator.StatusText}";

        public SolidColorBrush StatusBrush { get; } = new(indicator.IsSynced
            ? Color.FromArgb(255, 34, 197, 94)
            : Color.FromArgb(255, 239, 68, 68));
    }

    private sealed class ThreadCardViewModel(ThreadData thread)
    {
        public string CustomerName { get; } = thread.CustomerName;

        public string BranchName { get; } = thread.BranchName;

        public string PlatformGlyph { get; } = ResolvePlatformGlyph(thread.Platform);

        public string IntentLabel { get; } =
            UnifiedMessengerDashboardPresentationHelper.FormatIntentLabel(thread.AiIntentCategory);

        public string SentimentLabel { get; } = thread.ClientSentiment;

        public string NextActionSummary { get; } = string.IsNullOrWhiteSpace(thread.NextActionSummary)
            ? "Awaiting AI summary"
            : thread.NextActionSummary;

        public string UrgencyLabel { get; } = $"U{thread.UrgencyScore}";

        public string RevenueDisplay { get; } =
            thread.IsRevenueLeakageRisk && thread.EstimatedValue > 0
                ? $"{UnifiedMessengerDashboardPresentationHelper.FormatRevenue(thread.EstimatedValue)} at risk"
                : string.Empty;

        public Visibility RevenueVisibility { get; } =
            thread.IsRevenueLeakageRisk && thread.EstimatedValue > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

        public SolidColorBrush UrgencyBrush { get; } =
            CreateBrush(UnifiedMessengerDashboardPresentationHelper.ResolveUrgencyHex(thread.UrgencyScore));

        public SolidColorBrush SentimentBrush { get; } =
            CreateBrush(UnifiedMessengerDashboardPresentationHelper.ResolveSentimentHex(thread.ClientSentiment));

        public SolidColorBrush CardBorderBrush { get; } = thread.IsSlaBreached || thread.IsRevenueLeakageRisk
            ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
            : new SolidColorBrush(Colors.Transparent);

        public Thickness CardBorderThickness { get; } = thread.IsSlaBreached || thread.IsRevenueLeakageRisk
            ? new Thickness(thread.IsSlaBreached ? 4 : 2, 1, 1, 1)
            : new Thickness(1);

        public string SlaText { get; } = thread.IsSlaBreached
            ? $"Waiting {thread.LatencyMinutes:0.#}m · SLA breach"
            : string.Empty;

        public SolidColorBrush SlaBrush { get; } = new(Color.FromArgb(255, 220, 38, 38));

        public Visibility SlaVisibility { get; } = thread.IsSlaBreached
            ? Visibility.Visible
            : Visibility.Collapsed;

        private static string ResolvePlatformGlyph(string platformId) =>
            PlatformDefinition.NormalizePlatformId(platformId) switch
            {
                "whatsapp" or "whatsappbusiness" => "\uE8BD",
                "metabusiness" => "\uE717",
                "googlebusiness" => "\uE774",
                _ => "\uE774"
            };
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        var color = ColorFromHex(hex);
        return new SolidColorBrush(color);
    }

    private static Color ColorFromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6)
        {
            return Colors.Gray;
        }

        return Color.FromArgb(
            255,
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }
}
