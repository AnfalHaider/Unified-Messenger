using System.Text.Json;
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
    private GoogleReviewAlertView? _selectedReviewAlert;
    private string? _selectedBranchInstanceId;
    private bool _suppressBranchSelectionChanged;

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnResourceTimerTick(object? sender, object e)
    {
        RefreshResources();
        RefreshProfessionalMetrics();
        RefreshEnterpriseWidgets();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is RegistryNavigationArgs args)
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
        ProfessionalWorkspaceService.Instance.Changed += OnProfessionalWorkspaceChanged;

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
        NotificationHub.Instance.Changed -= OnHubChanged;
        AdapterHealthMonitor.Instance.Changed -= OnAdapterHealthChanged;
        MessageAnalyticsService.Instance.Changed -= OnAnalyticsChanged;
        ProfessionalWorkspaceService.Instance.Changed -= OnProfessionalWorkspaceChanged;

        if (_resourceTimer is not null)
        {
            _resourceTimer.Tick -= OnResourceTimerTick;
            _resourceTimer.Stop();
            _resourceTimer = null;
        }
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
        DispatcherQueue.TryEnqueue(() =>
        {
            RefreshProfessionalMetrics();
            RefreshEnterpriseWidgets();
        });
    }

    private void OnProfessionalWorkspaceChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshEnterpriseWidgets);
    }

    private async void ExportAnalyticsButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = $"unified-messenger-analytics-{DateTime.Now:yyyyMMdd}";
        picker.FileTypeChoices.Add("JSON", [".json"]);
        picker.FileTypeChoices.Add("CSV", [".csv"]);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            if (file.FileType.Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                if (_registry is null)
                {
                    return;
                }

                await MessageAnalyticsService.Instance.ExportCsvAsync(_registry.Instances, file.Path);
            }
            else
            {
                await MessageAnalyticsService.Instance.ExportToFileAsync(file.Path);
            }

            var dialog = new ContentDialog
            {
                Title = "Export complete",
                Content = $"Analytics saved to {file.Name}.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Export failed",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
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

        WelcomeSubtitle.Text = DashboardPageHelper.BuildWelcomeSubtitle(professionalCount, personalCount);

        RefreshBranchFilter();
        RefreshActivity();
        RefreshResources();
        RefreshProfessionalMetrics();
        RefreshEnterpriseWidgets();
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

    private IEnumerable<MessengerInstance> FilteredProfessionalInstances =>
        DashboardPageHelper.FilterProfessionalInstances(ProfessionalInstances, _selectedBranchInstanceId);

    private IEnumerable<MessengerInstance> GoogleBusinessInstances =>
        ProfessionalInstances.Where(i =>
            i.Platform.Equals("googlebusiness", StringComparison.OrdinalIgnoreCase));

    private IEnumerable<MessengerInstance> MetaBusinessInstances =>
        ProfessionalInstances.Where(i =>
            i.Platform.Equals("metabusiness", StringComparison.OrdinalIgnoreCase));

    private void RefreshEnterpriseWidgets()
    {
        if (_registry is null)
        {
            return;
        }

        var trust = ProfessionalWorkspaceService.Instance.CaptureCustomerTrust(
            FilteredGoogleBusinessInstances);
        AggregateRatingValue.Text = trust.AggregateRatingDisplay;
        UnrepliedReviewsValue.Text = DashboardPageHelper.FormatUnrepliedReviewCount(trust.TotalUnrepliedReviews);

        var reviewItems = trust.PendingReviews
            .Select(review => new GoogleReviewAlertView(review))
            .ToList();
        GoogleReviewAlertsList.ItemsSource = reviewItems;
        var hasReviews = reviewItems.Count > 0;
        GoogleReviewAlertsEmptyText.Visibility = hasReviews
            ? Visibility.Collapsed
            : Visibility.Visible;
        GoogleReviewAlertsList.Visibility = hasReviews
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (_selectedReviewAlert is not null &&
            reviewItems.All(item => item.AlertId != _selectedReviewAlert.AlertId))
        {
            _selectedReviewAlert = null;
        }

        var meta = ProfessionalWorkspaceService.Instance.CaptureMetaResponseEfficiency(
            FilteredMetaBusinessInstances);
        MetaAverageResponseValue.Text = meta.AverageResponseDisplay;
        MetaEfficiencyRatingValue.Text = meta.EfficiencyRating;
        MetaSampleCountValue.Text = meta.SampleCount.ToString();
        MetaLastInboundValue.Text = meta.LastInboundDisplay;
        MetaLastReplyValue.Text = meta.LastReplyDisplay;
        MetaResponseEmptyText.Visibility = FilteredMetaBusinessInstances.Any()
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private IEnumerable<MessengerInstance> FilteredGoogleBusinessInstances =>
        FilteredProfessionalInstances.Where(i =>
            i.Platform.Equals("googlebusiness", StringComparison.OrdinalIgnoreCase));

    private IEnumerable<MessengerInstance> FilteredMetaBusinessInstances =>
        FilteredProfessionalInstances.Where(i =>
            i.Platform.Equals("metabusiness", StringComparison.OrdinalIgnoreCase));

    private void RefreshBranchFilter()
    {
        if (_registry is null)
        {
            BranchFilterBox.ItemsSource = null;
            return;
        }

        var options = DashboardPageHelper.BuildBranchOptions(ProfessionalInstances);
        _suppressBranchSelectionChanged = true;
        BranchFilterBox.ItemsSource = options;
        BranchFilterBox.DisplayMemberPath = nameof(DashboardBranchOption.DisplayName);
        BranchFilterBox.SelectedValuePath = nameof(DashboardBranchOption.InstanceId);

        var selectedId = _selectedBranchInstanceId ?? DashboardPageHelper.AllBranchesOptionId;
        BranchFilterBox.SelectedItem = options.FirstOrDefault(option =>
            option.InstanceId.Equals(selectedId, StringComparison.OrdinalIgnoreCase));
        if (BranchFilterBox.SelectedItem is null)
        {
            BranchFilterBox.SelectedIndex = 0;
        }

        _suppressBranchSelectionChanged = false;
    }

    private void BranchFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressBranchSelectionChanged || BranchFilterBox.SelectedItem is not DashboardBranchOption option)
        {
            return;
        }

        _selectedBranchInstanceId = string.IsNullOrWhiteSpace(option.InstanceId) ? null : option.InstanceId;
        RefreshProfessionalMetrics();
        RefreshEnterpriseWidgets();
    }

    private void RefreshProfessionalMetrics()
    {
        if (_registry is null)
        {
            return;
        }

        var snapshot = MessageAnalyticsService.Instance.CaptureProfessionalSnapshot(
            FilteredProfessionalInstances,
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
                    instance?.AccentColor ?? PlatformBrandingHelper.DefaultAccentHex)
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
        UnreadValue.Text = snapshot.TotalUnreadCount.ToString();
        VisibleAccountValue.Text = snapshot.VisibleInstanceName;

        InstanceTilesList.ItemsSource = snapshot.InstanceTiles
            .Select(tile => new DashboardInstanceTileItem
            {
                InstanceId = tile.InstanceId,
                DisplayName = tile.DisplayName,
                PlatformLabel = tile.Platform,
                StatusLine = DashboardPageHelper.BuildInstanceStatusLine(tile),
                IconGlyph = tile.IconGlyph,
                AccentBrush = PlatformBrandingHelper.GetAccentBrush(tile.AccentColor)
            })
            .ToList();
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
        if (_registry is null)
        {
            return [];
        }

        return DashboardPageHelper
            .FilterPersonalSearchMatches(PersonalInstances, query)
            .Select(match => new DashboardSearchSuggestion
            {
                Label = match.Label,
                SubLabel = match.SubLabel,
                InstanceId = match.InstanceId,
                AccentBrush = PlatformBrandingHelper.GetAccentBrush(match.AccentColorHex)
            })
            .ToList();
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
            RecentActivityEmptyText.Text = DashboardPageHelper.ResolveEmptyActivityMessage(hasQuery);
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

    private void GoogleReviewAlertsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedReviewAlert = GoogleReviewAlertsList.SelectedItem as GoogleReviewAlertView;
        if (_selectedReviewAlert is not null && string.IsNullOrWhiteSpace(ReviewReplyBox.Text))
        {
            ReviewReplyBox.Text = $"Hi {_selectedReviewAlert.ReviewerName}, thank you for your feedback. ";
        }
    }

    private async void SubmitReviewReplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedReviewAlert is null)
        {
            return;
        }

        var replyText = ReviewReplyBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(replyText))
        {
            return;
        }

        ShellNavigationService.Instance.RequestInstance(_selectedReviewAlert.InstanceId);

        var reviewId = JsonSerializer.Serialize(_selectedReviewAlert.ReviewId);
        var reply = JsonSerializer.Serialize(replyText);
        var script = $"window.__umSubmitReviewReply({reviewId}, {reply});";

        await InstanceSessionManager.Instance.ExecuteScriptOnInstanceAsync(
            _selectedReviewAlert.InstanceId,
            script);

        ProfessionalWorkspaceService.Instance.MarkReviewReplied(_selectedReviewAlert.AlertId);
        ReviewReplyBox.Text = string.Empty;
        _selectedReviewAlert = null;
        GoogleReviewAlertsList.SelectedItem = null;
        RefreshEnterpriseWidgets();
    }

    private void OpenSelectedReviewInstanceButton_Click(object sender, RoutedEventArgs e)
    {
        var instanceId = _selectedReviewAlert?.InstanceId ??
            GoogleBusinessInstances.FirstOrDefault()?.Id;

        if (!string.IsNullOrWhiteSpace(instanceId))
        {
            ShellNavigationService.Instance.RequestInstance(instanceId);
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

        public bool Matches(string query) =>
            DashboardPageHelper.ActivityMatches(Title, Body, InstanceDisplayName, query);
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

    private sealed class GoogleReviewAlertView
    {
        public GoogleReviewAlertView(GoogleReviewAlert alert)
        {
            AlertId = alert.Id;
            InstanceId = alert.InstanceId;
            ReviewId = alert.ReviewId;
            ReviewerName = alert.ReviewerName;
            Snippet = alert.Snippet;
            LocationLabel = $"{alert.InstanceDisplayName} · {alert.LocationLabel}";
            RelativeTimeText = alert.RelativeTimeText;
            RatingDisplay = alert.Rating > 0 ? $"{alert.Rating}★" : "★";
        }

        public string AlertId { get; }

        public string InstanceId { get; }

        public string ReviewId { get; }

        public string ReviewerName { get; }

        public string Snippet { get; }

        public string LocationLabel { get; }

        public string RelativeTimeText { get; }

        public string RatingDisplay { get; }
    }
}
