using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Pages;

public sealed partial class DashboardPage : Page
{
    private InstanceRegistryService? _registry;
    private readonly List<DashboardActivityItem> _allActivity = [];
    private DispatcherTimer? _resourceTimer;
    private GoogleReviewAlertView? _selectedReviewAlert;
    private string? _selectedBranchInstanceId;
    private bool _suppressBranchSelectionChanged;
    private readonly ObservableCollection<DashboardBranchFilterEntry> _branchFilterEntries = new();

    public ObservableCollection<DashboardBranchFilterEntry> BranchFilterEntries => _branchFilterEntries;

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnResourceTimerTick(object? sender, object e)
    {
        RefreshResources();
        ApplyProfessionalTelemetryToView();
        ApplyEnterpriseTelemetryToView();
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
        MessageTriageService.Instance.Changed += OnTriageChanged;
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
        MessageTriageService.Instance.Changed -= OnTriageChanged;
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
            ApplyProfessionalTelemetryToView();
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
            ApplyProfessionalTelemetryToView();
            ApplyEnterpriseTelemetryToView();
        });
    }

    private void OnTriageChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(() => ApplyTriageTelemetryToView());

    private void OnProfessionalWorkspaceChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(ApplyEnterpriseTelemetryToView);
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
        ApplyProfessionalTelemetryToView();
        ApplyEnterpriseTelemetryToView();
        UpdateSearchSuggestions(GlobalSearchBox.Text);
        UpdateProfessionalEmptyState();
    }

    private async void RefreshDashboardDataButton_Click(object sender, RoutedEventArgs e) =>
        await RequestProfessionalTelemetryRefreshAsync();

    private async void RefreshAllProfessionalDataButton_Click(object sender, RoutedEventArgs e) =>
        await RequestProfessionalTelemetryRefreshAsync(refreshAllInstances: true);

    private async Task RequestProfessionalTelemetryRefreshAsync(bool refreshAllInstances = false)
    {
        if (_registry is null)
        {
            return;
        }

        RefreshDashboardDataButton.IsEnabled = false;
        RefreshAllProfessionalDataButton.IsEnabled = false;
        try
        {
            ApplyProfessionalTelemetryToView();
            ApplyEnterpriseTelemetryToView();
            ScheduleBackfillRetryIfNeeded();
            if (refreshAllInstances)
            {
                await RequestAllProfessionalScrapeRefreshAsync().ConfigureAwait(true);
            }
            else
            {
                await RequestBranchScrapeRefreshAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            RefreshDashboardDataButton.IsEnabled = true;
            RefreshAllProfessionalDataButton.IsEnabled = true;
        }
    }

    private void ScheduleBackfillRetryIfNeeded()
    {
        if (!AppSettingsService.Instance.Settings.EnableStartupBackfill)
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

    /// <summary>
    /// Google/Meta scrapers require active WebView sessions and visible inbox DOM for reliable telemetry.
    /// </summary>
    private async Task RequestAllProfessionalScrapeRefreshAsync()
    {
        if (_registry is null)
        {
            return;
        }

        var scrapeTargets = ProfessionalInstances
            .Where(DashboardScrapeOrchestrator.IsDashboardScrapeCapable)
            .ToList();

        if (scrapeTargets.Count == 0)
        {
            return;
        }

        try
        {
            await DashboardScrapeOrchestrator.Instance
                .RefreshProfessionalInstancesAsync(scrapeTargets)
                .ConfigureAwait(true);

            MessageAnalyticsService.Instance.NotifyDashboardRefresh();
            ApplyProfessionalTelemetryToView();
            ApplyEnterpriseTelemetryToView();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"All-instance scrape refresh failed: {ex.Message}");
        }
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

    private void ApplyEnterpriseTelemetryToView()
    {
        if (_registry is null)
        {
            return;
        }

        var trust = ProfessionalWorkspaceService.Instance.CaptureCustomerTrust(
            FilteredGoogleBusinessInstances);
        var trustDisplay = DashboardPageHelper.BuildCustomerTrustDisplay(trust);
        AggregateRatingValue.Text = trustDisplay.AggregateRating;
        UnrepliedReviewsValue.Text = trustDisplay.UnrepliedReviews;

        var reviewItems = trustDisplay.PendingReviews
            .Select(review => new GoogleReviewAlertView(review))
            .ToList();
        GoogleReviewAlertsList.ItemsSource = reviewItems;
        var hasReviews = reviewItems.Count > 0;
        var hasGoogleInstances = FilteredGoogleBusinessInstances.Any();
        var googleEmptyReason = DashboardCardEmptyStateHelper.ResolveGoogleTrustEmptyReason(
            hasGoogleInstances,
            trust);
        GoogleReviewAlertsEmptyText.Text =
            DashboardCardEmptyStateHelper.FormatGoogleTrustEmptyMessage(googleEmptyReason);
        var showGoogleEmpty = !hasReviews && googleEmptyReason != DashboardCardEmptyReason.HasData;
        GoogleReviewAlertsEmptyText.Visibility = showGoogleEmpty
            ? Visibility.Visible
            : Visibility.Collapsed;
        GoogleReviewAlertsList.Visibility = hasReviews
            ? Visibility.Visible
            : Visibility.Collapsed;

        var googleIds = FilteredGoogleBusinessInstances.Select(i => i.Id).ToList();
        var scrapeFooter = DashboardScrapeStatusService.Instance.BuildGoogleTrustScrapeFooter(googleIds);
        GoogleTrustScrapeStatusText.Text = scrapeFooter;
        GoogleTrustScrapeStatusText.Visibility = hasGoogleInstances && !string.IsNullOrWhiteSpace(scrapeFooter)
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (_selectedReviewAlert is not null &&
            reviewItems.All(item => item.AlertId != _selectedReviewAlert.AlertId))
        {
            _selectedReviewAlert = null;
        }

        var meta = ProfessionalWorkspaceService.Instance.CaptureMetaResponseEfficiency(
            FilteredMetaBusinessInstances);
        var metaDisplay = DashboardPageHelper.BuildMetaResponseDisplay(meta);
        MetaAverageResponseValue.Text = metaDisplay.AverageResponse;
        MetaEfficiencyRatingValue.Text = metaDisplay.EfficiencyRating;
        MetaSampleCountValue.Text = metaDisplay.SampleCount;
        MetaLastInboundValue.Text = metaDisplay.LastInbound;
        MetaLastReplyValue.Text = metaDisplay.LastReply;

        var hasMetaInstances = FilteredMetaBusinessInstances.Any();
        var metaEmptyReason = DashboardCardEmptyStateHelper.ResolveMetaResponseEmptyReason(
            hasMetaInstances,
            meta);
        MetaResponseEmptyText.Text =
            DashboardCardEmptyStateHelper.FormatMetaResponseEmptyMessage(metaEmptyReason);
        MetaResponseEmptyText.Visibility = metaEmptyReason == DashboardCardEmptyReason.HasData
            ? Visibility.Collapsed
            : Visibility.Visible;

        MetaPendingResponseText.Text = metaDisplay.PendingResponseLabel;
        MetaPendingResponseText.Visibility = string.IsNullOrWhiteSpace(metaDisplay.PendingResponseLabel)
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
            _branchFilterEntries.Clear();
            return;
        }

        _suppressBranchSelectionChanged = true;
        _branchFilterEntries.Clear();
        _branchFilterEntries.Add(DashboardBranchFilterEntry.CreateAllBranches());

        foreach (var instance in ProfessionalInstances
                     .Where(instance => instance.IsProfessional && !string.IsNullOrWhiteSpace(instance.Id))
                     .OrderBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            _branchFilterEntries.Add(DashboardBranchFilterEntry.FromInstance(instance));
        }

        var selectedId = _selectedBranchInstanceId ?? DashboardPageHelper.AllBranchesOptionId;
        BranchFilterBox.SelectedItem = _branchFilterEntries.FirstOrDefault(entry =>
            (entry.IsAllBranches && string.IsNullOrWhiteSpace(selectedId)) ||
            (!entry.IsAllBranches &&
             entry.InstanceId.Equals(selectedId, StringComparison.OrdinalIgnoreCase)));

        if (BranchFilterBox.SelectedItem is null && _branchFilterEntries.Count > 0)
        {
            BranchFilterBox.SelectedIndex = 0;
            _selectedBranchInstanceId = null;
        }

        _suppressBranchSelectionChanged = false;
    }

    private void BranchFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressBranchSelectionChanged ||
            BranchFilterBox.SelectedItem is not DashboardBranchFilterEntry entry)
        {
            return;
        }

        _selectedBranchInstanceId = DashboardPageHelper.ResolveBranchInstanceId(entry);

        ApplyProfessionalTelemetryToView();
        ApplyEnterpriseTelemetryToView();

        _ = RequestBranchScrapeRefreshAsync();
    }

    private void ApplyProfessionalTelemetryToView()
    {
        if (_registry is null)
        {
            return;
        }

        var telemetry = DashboardPageHelper.CaptureProfessionalDashboardTelemetry(
            ProfessionalInstances,
            NotificationHub.Instance,
            _selectedBranchInstanceId);

        BindProfessionalTelemetryToView(telemetry);
        UpdateProfessionalEmptyState();
    }

    private void BindProfessionalTelemetryToView(ProfessionalDashboardTelemetry telemetry)
    {
        var display = telemetry.Display;

        if (_registry is not null)
        {
            ProfessionalBranchScopeText.Text = DashboardCardEmptyStateHelper.BuildBranchScopeSubtitle(
                ProfessionalInstances,
                _selectedBranchInstanceId);
        }

        AvgReplyTimeValue.Text = display.AverageReplyTime;
        ApplySubtext(AvgReplyTimeSubtext, display.AverageReplyTimeSubtext);
        SlaBreachesValue.Text = display.SlaBreaches;
        ApplySubtext(SlaThresholdSubtext, display.SlaThresholdSubtext);
        ResponseRateValue.Text = display.ResponseRate;
        PeakHourValue.Text = display.PeakHour;
        DailyTrendValue.Text = display.DailyTrend;
        SentCountValue.Text = display.SentCount;
        ReceivedCountValue.Text = display.ReceivedCount;
        WeeklyChart.SetSeries(display.WeeklyActivity);

        var highlights = display.Highlights
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

        ApplyProfessionalHealthChips();
        ApplyTriageTelemetryToView(display.Triage);
    }

    private static void ApplySubtext(TextBlock target, string text)
    {
        var visible = !string.IsNullOrWhiteSpace(text);
        target.Text = text;
        target.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyProfessionalHealthChips()
    {
        if (_registry is null)
        {
            ProfessionalHealthChipsItems.ItemsSource = null;
            return;
        }

        var chips = DashboardDataHealthHelper
            .BuildProfessionalHealthChips(ProfessionalInstances)
            .Select(chip => new ProfessionalHealthChipView(chip))
            .ToList();

        ProfessionalHealthChipsItems.ItemsSource = chips;
    }

    private void ApplyTriageTelemetryToView(MessageTriageDashboardSnapshot? triage = null)
    {
        if (_registry is null)
        {
            return;
        }

        triage ??= DashboardPageHelper.BuildFilteredTriageSnapshot(
            ProfessionalInstances,
            _selectedBranchInstanceId);

        var urgentItems = triage.UrgentQueue
            .Select(item => new MessageTriageItemView(item))
            .ToList();

        UrgencyTriageList.ItemsSource = urgentItems;
        var hasUrgent = urgentItems.Count > 0;
        var urgencyEmptyReason = DashboardCardEmptyStateHelper.ResolveUrgencyEmptyReason(triage);
        UrgencyTriageEmptyText.Text =
            DashboardCardEmptyStateHelper.FormatUrgencyEmptyMessage(urgencyEmptyReason);
        UrgencyTriageEmptyText.Visibility = hasUrgent ? Visibility.Collapsed : Visibility.Visible;
        UrgencyTriageList.Visibility = hasUrgent ? Visibility.Visible : Visibility.Collapsed;

        var recentItems = triage.RecentInbound
            .Select(item => new MessageTriageItemView(item))
            .ToList();
        RecentInboundTriageList.ItemsSource = recentItems;
        var hasRecent = recentItems.Count > 0;
        RecentInboundHeaderText.Visibility = hasRecent ? Visibility.Visible : Visibility.Collapsed;
        RecentInboundTriageList.Visibility = hasRecent ? Visibility.Visible : Visibility.Collapsed;
        RecentInboundEmptyText.Visibility = triage.TotalTriageCount > 0 && !hasRecent && !hasUrgent
            ? Visibility.Visible
            : Visibility.Collapsed;

        SentimentChart.SetSeries(triage);
        ApplyExecutiveInsightsToView();
    }

    private void ApplyExecutiveInsightsToView()
    {
        if (_registry is null)
        {
            return;
        }

        var cards = DashboardPageHelper
            .BuildExecutiveInsights(ProfessionalInstances, _selectedBranchInstanceId)
            .Select(card => new ExecutiveInsightCardView(card))
            .ToList();

        ExecutiveInsightsList.ItemsSource = cards;
        var hasInsights = cards.Count > 0;
        var triageSnapshot = DashboardPageHelper.BuildFilteredTriageSnapshot(
            ProfessionalInstances,
            _selectedBranchInstanceId);
        var insightsEmptyReason = DashboardCardEmptyStateHelper.ResolveExecutiveInsightsEmptyReason(
            AppSettingsService.Instance.Settings.EnableLocalAi,
            triageSnapshot.TotalTriageCount,
            cards.Count);
        ExecutiveInsightsEmptyText.Text =
            DashboardCardEmptyStateHelper.FormatExecutiveInsightsEmptyMessage(insightsEmptyReason);
        ExecutiveInsightsEmptyText.Visibility = hasInsights ? Visibility.Collapsed : Visibility.Visible;
        ExecutiveInsightsList.Visibility = hasInsights ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task RequestBranchScrapeRefreshAsync()
    {
        if (_registry is null)
        {
            return;
        }

        var scrapeTargets = FilteredProfessionalInstances
            .Where(DashboardScrapeOrchestrator.IsDashboardScrapeCapable)
            .ToList();

        if (scrapeTargets.Count == 0)
        {
            return;
        }

        try
        {
            await DashboardScrapeOrchestrator.Instance
                .RefreshProfessionalInstancesAsync(scrapeTargets)
                .ConfigureAwait(true);

            MessageAnalyticsService.Instance.NotifyDashboardRefresh();
            ApplyProfessionalTelemetryToView();
            ApplyEnterpriseTelemetryToView();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Branch scrape refresh failed: {ex.Message}");
        }
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

    private void UrgencyTriageList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is MessageTriageItemView item &&
            !string.IsNullOrWhiteSpace(item.InstanceId))
        {
            ShellNavigationService.Instance.RequestInstance(item.InstanceId);
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

    private sealed class ExecutiveInsightCardView
    {
        public ExecutiveInsightCardView(ExecutiveInsightCardDisplay card)
        {
            CustomerName = card.CustomerName;
            BranchName = card.BranchName;
            CoreSummary = card.CoreSummary;
            IntentLabel = card.IntentLabel;
            UrgencyLabel = card.UrgencyLabel;
            SourceLabel = card.SourceLabel;
            Fields = card.Fields.Select(field => new ExecutiveInsightFieldView(field)).ToList();
        }

        public string CustomerName { get; }

        public string BranchName { get; }

        public string CoreSummary { get; }

        public string IntentLabel { get; }

        public string UrgencyLabel { get; }

        public string SourceLabel { get; }

        public IReadOnlyList<ExecutiveInsightFieldView> Fields { get; }
    }

    private sealed class ProfessionalHealthChipView
    {
        public ProfessionalHealthChipView(DashboardInstanceHealthChip chip)
        {
            Summary =
                $"{chip.DisplayName}: backfill {chip.BackfillState}, {chip.AdapterHealth}, {chip.TriageItemCount} triage";
        }

        public string Summary { get; }
    }

    private sealed class ExecutiveInsightFieldView
    {
        public ExecutiveInsightFieldView(ExecutiveInsightFieldDisplay field)
        {
            Label = field.Label;
            Value = field.Value;
            IconGlyph = field.IconGlyph;
            IsEmphasized = field.Emphasize;
        }

        public string Label { get; }

        public string Value { get; }

        public string IconGlyph { get; }

        public bool IsEmphasized { get; }
    }

    private sealed class MessageTriageItemView
    {
        public MessageTriageItemView(MessageTriageItem item)
        {
            InstanceId = item.InstanceId;
            InstanceDisplayName = item.InstanceDisplayName;
            CustomerName = item.CustomerName;
            MessagePreview = item.MessagePreview;
            UrgencyLabel = $"{item.UrgencyLabel} · {item.UrgencyScore}";
        }

        public string InstanceId { get; }

        public string InstanceDisplayName { get; }

        public string CustomerName { get; }

        public string MessagePreview { get; }

        public string UrgencyLabel { get; }
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
