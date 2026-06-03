using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Pages;

public sealed partial class DashboardPage : Page
{
    private InstanceRegistryService? _registry;
    private readonly List<DashboardActivityItem> _allActivity = [];
    private DispatcherTimer? _resourceTimer;

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is DashboardNavigationArgs args)
        {
            _registry = args.Registry;
        }

        RefreshAll();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NotificationHub.Instance.Changed += OnHubChanged;
        AdapterHealthMonitor.Instance.Changed += OnAdapterHealthChanged;
        MessageAnalyticsService.Instance.Changed += OnAnalyticsChanged;

        _resourceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _resourceTimer.Tick += (_, _) =>
        {
            RefreshResources();
            RefreshProfessionalMetrics();
        };
        _resourceTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        NotificationHub.Instance.Changed -= OnHubChanged;
        AdapterHealthMonitor.Instance.Changed -= OnAdapterHealthChanged;
        MessageAnalyticsService.Instance.Changed -= OnAnalyticsChanged;
        _resourceTimer?.Stop();
        _resourceTimer = null;
    }

    private void OnHubChanged(object? sender, NotificationHubChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            RefreshActivity();
            RefreshProfessionalMetrics();
        });
    }

    private void OnAdapterHealthChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshResources);
    }

    private void OnAnalyticsChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshProfessionalMetrics);
    }

    public void RefreshAll()
    {
        if (_registry is null)
        {
            WelcomeSubtitle.Text = "Add an account to start receiving unified notifications.";
            UpdateProfessionalEmptyState();
            return;
        }

        var professionalCount = _registry.Instances.Count(i => i.IsProfessional);
        var personalCount = _registry.Instances.Count - professionalCount;

        WelcomeSubtitle.Text = (professionalCount, personalCount) switch
        {
            (0, 0) => "Add an account to start receiving unified notifications.",
            ( > 0, > 0) => $"{professionalCount} professional and {personalCount} personal accounts connected.",
            ( > 0, 0) => $"{professionalCount} professional account{(professionalCount == 1 ? "" : "s")} connected.",
            _ => $"{personalCount} personal account{(personalCount == 1 ? "" : "s")} connected."
        };

        RefreshActivity();
        RefreshResources();
        RefreshProfessionalMetrics();
        UpdateSearchSuggestions(GlobalSearchBox.Text);
        UpdateProfessionalEmptyState();
    }

    private void UpdateProfessionalEmptyState()
    {
        var hasProfessional = ProfessionalInstances.Any();
        ProfessionalEmptyPanel.Visibility = hasProfessional
            ? Visibility.Collapsed
            : Visibility.Visible;
        ProfessionalContentGrid.Visibility = hasProfessional
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private IEnumerable<MessengerInstance> PersonalInstances =>
        _registry?.Instances.Where(i => !i.IsProfessional) ?? [];

    private IEnumerable<MessengerInstance> ProfessionalInstances =>
        _registry?.Instances.Where(i => i.IsProfessional) ?? [];

    private void RefreshProfessionalMetrics()
    {
        if (_registry is null)
        {
            return;
        }

        var snapshot = MessageAnalyticsService.Instance.CaptureProfessionalSnapshot(
            ProfessionalInstances,
            NotificationHub.Instance);

        AvgReplyTimeValue.Text = snapshot.AverageReplyTimeDisplay;
        SlaBreachesValue.Text = snapshot.SlaBreaches.ToString();
        ResponseRateValue.Text = snapshot.ResponseRateDisplay;
        PeakHourValue.Text = snapshot.PeakHourDisplay;
        DailyTrendValue.Text = snapshot.DailyTrendDisplay;
        SentCountValue.Text = snapshot.SentCount.ToString();
        ReceivedCountValue.Text = snapshot.ReceivedCount.ToString();
        WeeklyChart.SetSeries(snapshot.WeeklyActivity);

        var highlights = snapshot.Highlights
            .Select(h => new OperationalHighlightItemView(h))
            .ToList();

        OperationalHighlightsList.ItemsSource = highlights;
        var hasHighlights = highlights.Count > 0;
        OperationalHighlightsEmptyText.Visibility = hasHighlights
            ? Visibility.Collapsed
            : Visibility.Visible;
        OperationalHighlightsList.Visibility = hasHighlights
            ? Visibility.Visible
            : Visibility.Collapsed;

        UpdateProfessionalEmptyState();
    }

    private void RefreshActivity()
    {
        _allActivity.Clear();
        var personalIds = PersonalInstances.Select(i => i.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var alert in NotificationHub.Instance.GetAlertsSortedByInstance())
        {
            if (!personalIds.Contains(alert.InstanceId))
            {
                continue;
            }

            var instance = _registry?.FindById(alert.InstanceId);
            _allActivity.Add(new DashboardActivityItem
            {
                Alert = alert,
                Title = alert.Title,
                Body = alert.Body,
                InstanceDisplayName = alert.InstanceDisplayName,
                RelativeTimeText = alert.RelativeTimeText,
                IconGlyph = instance?.IconGlyph ?? alert.IconGlyph,
                AccentBrush = PlatformBrandingHelper.GetAccentBrush(
                    instance?.AccentColor ?? "#6B7280")
            });
        }

        ApplyActivityFilter(GlobalSearchBox.Text);
    }

    private void RefreshResources()
    {
        if (_registry is null)
        {
            return;
        }

        var personalList = PersonalInstances.ToList();
        var snapshot = ResourceMonitorService.Instance.Capture(
            personalList,
            InstanceSessionManager.Instance,
            NotificationHub.Instance,
            AdapterHealthMonitor.Instance);

        ActiveAccountsValue.Text = snapshot.ActiveAccountCount.ToString();
        MemoryValue.Text = $"{snapshot.WorkingSetMegabytes} MB";
        UnreadValue.Text = personalList.Sum(i => NotificationHub.Instance.GetBadgeCount(i.Id)).ToString();

        var visibleId = InstanceSessionManager.Instance.VisibleInstanceId;
        VisibleAccountValue.Text = personalList
            .FirstOrDefault(i => i.Id.Equals(visibleId, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName ?? "None";

        InstanceTilesList.ItemsSource = snapshot.InstanceTiles
            .Select(tile => new DashboardInstanceTileItem
            {
                InstanceId = tile.InstanceId,
                DisplayName = tile.DisplayName,
                PlatformLabel = tile.Platform,
                StatusLine = BuildStatusLine(tile),
                IconGlyph = tile.IconGlyph,
                AccentBrush = PlatformBrandingHelper.GetAccentBrush(tile.AccentColor)
            })
            .ToList();
    }

    private static string BuildStatusLine(InstanceResourceTile tile)
    {
        var parts = new List<string>();
        if (tile.IsVisible)
        {
            parts.Add("Visible");
        }

        parts.Add(tile.MemoryTier);
        if (tile.UnreadCount > 0)
        {
            parts.Add($"{tile.UnreadCount} unread");
        }

        parts.Add(tile.HealthState.ToString());
        return string.Join(" · ", parts);
    }

    private void GlobalSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
        {
            return;
        }

        ApplyActivityFilter(sender.Text);
        UpdateSearchSuggestions(sender.Text);
    }

    private void GlobalSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is DashboardSearchSuggestion suggestion)
        {
            NavigateFromSearchSuggestion(suggestion);
            return;
        }

        var query = args.QueryText?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        var suggestions = BuildSearchSuggestions(query);
        if (suggestions.Count > 0)
        {
            NavigateFromSearchSuggestion(suggestions[0]);
        }
    }

    private void GlobalSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is DashboardSearchSuggestion suggestion)
        {
            NavigateFromSearchSuggestion(suggestion);
        }
    }

    private void UpdateSearchSuggestions(string? query)
    {
        GlobalSearchBox.ItemsSource = BuildSearchSuggestions(query);
    }

    private List<DashboardSearchSuggestion> BuildSearchSuggestions(string? query)
    {
        if (_registry is null || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        query = query.Trim();
        var suggestions = new List<DashboardSearchSuggestion>();

        foreach (var instance in PersonalInstances)
        {
            var platform = PlatformDefinition.FindById(instance.Platform);
            var platformLabel = platform?.DisplayName ?? instance.Platform;
            if (!instance.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !platformLabel.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            suggestions.Add(new DashboardSearchSuggestion
            {
                Label = instance.DisplayName,
                SubLabel = platformLabel,
                InstanceId = instance.Id,
                AccentBrush = PlatformBrandingHelper.GetAccentBrush(instance)
            });

            if (suggestions.Count >= 6)
            {
                break;
            }
        }

        return suggestions;
    }

    private static void NavigateFromSearchSuggestion(DashboardSearchSuggestion suggestion)
    {
        if (!string.IsNullOrWhiteSpace(suggestion.InstanceId))
        {
            ShellNavigationService.Instance.RequestInstance(suggestion.InstanceId);
        }
    }

    private void ApplyActivityFilter(string? query)
    {
        IEnumerable<DashboardActivityItem> filtered = _allActivity;
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        if (hasQuery)
        {
            filtered = _allActivity.Where(item => item.Matches(query!));
        }

        var list = filtered.ToList();
        RecentActivityList.ItemsSource = list;

        if (list.Count == 0)
        {
            RecentActivityEmptyText.Text = hasQuery
                ? "No personal activity matches your search."
                : "No recent notifications from personal accounts.";
            RecentActivityEmptyText.Visibility = Visibility.Visible;
            RecentActivityList.Visibility = Visibility.Collapsed;
        }
        else
        {
            RecentActivityEmptyText.Visibility = Visibility.Collapsed;
            RecentActivityList.Visibility = Visibility.Visible;
        }
    }

    private void RecentActivityList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DashboardActivityItem item)
        {
            ShellNavigationService.Instance.RequestInstance(item.Alert.InstanceId);
        }
    }

    private void InstanceTilesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DashboardInstanceTileItem tile)
        {
            ShellNavigationService.Instance.RequestInstance(tile.InstanceId);
        }
    }

    private void OperationalHighlightsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is OperationalHighlightItemView highlight &&
            !string.IsNullOrWhiteSpace(highlight.InstanceId))
        {
            ShellNavigationService.Instance.RequestInstance(highlight.InstanceId);
        }
    }

    private sealed class DashboardActivityItem
    {
        public required NotificationAlert Alert { get; init; }

        public required string Title { get; init; }

        public required string Body { get; init; }

        public required string InstanceDisplayName { get; init; }

        public required string RelativeTimeText { get; init; }

        public required string IconGlyph { get; init; }

        public required SolidColorBrush AccentBrush { get; init; }

        public bool Matches(string query)
        {
            query = query.Trim();
            return Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   Body.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   InstanceDisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class DashboardInstanceTileItem
    {
        public required string InstanceId { get; init; }

        public required string DisplayName { get; init; }

        public required string PlatformLabel { get; init; }

        public required string StatusLine { get; init; }

        public required string IconGlyph { get; init; }

        public required SolidColorBrush AccentBrush { get; init; }
    }

    private sealed class DashboardSearchSuggestion
    {
        public required string Label { get; init; }

        public required string SubLabel { get; init; }

        public string? InstanceId { get; init; }

        public SolidColorBrush? AccentBrush { get; init; }

        public override string ToString() => $"{Label} ({SubLabel})";
    }

    private sealed class OperationalHighlightItemView
    {
        public OperationalHighlightItemView(OperationalHighlightItem item)
        {
            Title = item.Title;
            Subtitle = item.Subtitle;
            InstanceDisplayName = item.InstanceDisplayName;
            InstanceId = item.InstanceId;
        }

        public string Title { get; }

        public string Subtitle { get; }

        public string InstanceDisplayName { get; }

        public string? InstanceId { get; }
    }
}
