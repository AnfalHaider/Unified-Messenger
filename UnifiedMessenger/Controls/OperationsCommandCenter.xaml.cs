using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter : UserControl
{
    private readonly OperationsCommandCenterViewModel _viewModel = new();
    private ApplicationServices _services = new();
    private DispatcherQueue _dispatcherQueue = null!;
    private IEnumerable<MessengerInstance> _professionalInstances = [];
    private OperationsCommandCenterSnapshot _snapshot = OperationsCommandCenterSnapshot.Empty;
    private IInstanceRegistryService? _registry;
    private bool _suppressPillSelection;
    private string? _lastPillBarSignature;
    private bool _isRefreshing;
    private bool _showWorkspaceLoading;
    private bool _suppressDateRangeEvents;
    private DispatcherQueueTimer? _dateRangeDebounceTimer;
    private IReadOnlyDictionary<string, BranchWorkspaceHelper.BranchTabCounts> _branchTabCounts =
        new Dictionary<string, BranchWorkspaceHelper.BranchTabCounts>(StringComparer.OrdinalIgnoreCase);

    private string? WorkspaceBranchKey
    {
        get => _services.OccFilter.BranchKey;
        set => _services.OccFilter.BranchKey = value;
    }

    public OperationsCommandCenter()
    {
        InitializeComponent();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        ImmediateQueueList.ItemsSource = _viewModel.ImmediateQueue;
        KanbanBoard.BindCollections(_viewModel.NewInquiries, _viewModel.HangingLeads, _viewModel.Resolved);
        KanbanBoard.IsReorderEnabled = true;
        BranchWorkspacePillBar.SelectionChanged += OnBranchWorkspacePillSelectionChanged;
        _services.OccFilter.Changed += OnOccFilterStateChanged;
        _services.OccDateRangeFilter.Changed += OnOccDateRangeFilterChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeDateRangePickers();
        ApplyAccessibilityTabOrder();
        WireResponsiveLayoutHelpers();
        WireKanbanKeyboardShortcuts();
        EnsureBackfillStatusSubscription();
        ApplyBackfillStatusUi();
    }

    private void WireResponsiveLayoutHelpers()
    {
        KanbanBoard.WireScrollBubbling(MainContentScrollViewer);
        ScrollInputHelper.EnableVerticalScrollBubbling(ImmediateQueueList, MainContentScrollViewer);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        BranchWorkspacePillBar.SelectionChanged -= OnBranchWorkspacePillSelectionChanged;
        _services.OccFilter.Changed -= OnOccFilterStateChanged;
        _services.OccDateRangeFilter.Changed -= OnOccDateRangeFilterChanged;
        StopDateRangeDebounceTimer();
    }

    public void ConfigureServices(ApplicationServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;

        _suppressDateRangeEvents = true;
        try
        {
            OccDateRangeSettingsHelper.ApplyPersistedRange(
                _services.AppSettings.Settings,
                _services.OccDateRangeFilter);
            if (!_services.OccDateRangeFilter.HasActiveFilter)
            {
                _services.OccDateRangeFilter.ResetToDefaultWindow();
            }
        }
        finally
        {
            _suppressDateRangeEvents = false;
        }
    }

    public Task RefreshAsync(
        IEnumerable<MessengerInstance> professionalInstances,
        IInstanceRegistryService? registry = null) =>
        RefreshCoreAsync(professionalInstances, registry, allowLoadingOverlay: true);

    public Task RefreshLightAsync(
        IEnumerable<MessengerInstance> professionalInstances,
        IInstanceRegistryService? registry = null) =>
        RefreshKpisOnlyAsync(professionalInstances, registry);

    public bool IsKanbanSnapshotStale => false;

    public void SelectWorkspaceBranch(string? branchKey, bool forceRefresh = false)
    {
        if (!forceRefresh &&
            string.Equals(WorkspaceBranchKey, branchKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        WorkspaceBranchKey = branchKey;
        _showWorkspaceLoading = true;
        _suppressPillSelection = true;
        BranchWorkspacePillBar.SelectBranchKey(branchKey);
        _suppressPillSelection = false;
        ApplyBranchFilterChip();
        if (_registry is not null)
        {
            _ = RefreshAsync(_professionalInstances, _registry);
        }
    }

    public void RequestImmediateLaneFocus() =>
        ImmediateLaneSection?.StartBringIntoView();

    public void ApplyAccessibilityTabOrder()
    {
        AccessibilityTabOrderHelper.ApplyTabIndex(
            RefreshCommandButton,
            AccessibilityTabOrderHelper.OccRefreshButton);
        AccessibilityTabOrderHelper.ApplyTabIndex(
            BranchWorkspacePillBar,
            AccessibilityTabOrderHelper.OccBranchPillBar);
    }

    private async Task RefreshKpisOnlyAsync(
        IEnumerable<MessengerInstance> professionalInstances,
        IInstanceRegistryService? registry)
    {
        _professionalInstances = professionalInstances;
        _registry = registry;

        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            var instanceList = professionalInstances.ToList();
            var status = await Task.Run(() =>
                    _services.OperationsCommandCenter.BuildStatusOnly(
                        instanceList,
                        WorkspaceBranchKey,
                        fromUtc: _services.OccDateRangeFilter.FromUtc,
                        toUtc: _services.OccDateRangeFilter.ToUtc))
                .ConfigureAwait(true);

            var notificationHub = _services.NotificationHub as NotificationHub ?? NotificationHub.Instance;
            var analyticsTrends = await Task.Run(() =>
                {
                    var telemetry = DashboardPageHelper.CaptureProfessionalDashboardTelemetry(
                        instanceList,
                        notificationHub,
                        WorkspaceBranchKey,
                        _services.OccDateRangeFilter.FromUtc,
                        _services.OccDateRangeFilter.ToUtc);
                    var analytics = telemetry.Snapshot;
                    return new OperationsAnalyticsTrendSnapshot
                    {
                        WeeklyActivity = analytics.WeeklyActivity,
                        SentCount = analytics.SentCount,
                        ReceivedCount = analytics.ReceivedCount,
                        HasMessageVolume = analytics.HasMessageVolume,
                        HasReplyMetrics = analytics.HasReplyMetrics,
                        Triage = analytics.Triage,
                        Highlights = analytics.Highlights
                    };
                })
                .ConfigureAwait(true);

            ApplyStatusKpis(OccSnapshotPresenter.BuildStatusKpis(status));
            ApplyScopeLabel(snapshotScopeLabel: null, statusBranchScope: null);
            ApplyShellPresentation(instanceList.Count > 0, status);
            ApplyAnalyticsTrends(analyticsTrends);
            RebuildBranchPills(instanceList);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task RefreshCoreAsync(
        IEnumerable<MessengerInstance> professionalInstances,
        IInstanceRegistryService? registry,
        bool allowLoadingOverlay)
    {
        _professionalInstances = professionalInstances;
        _registry = registry;

        if (_isRefreshing)
        {
            if (allowLoadingOverlay)
            {
                _services.DashboardRefresh.ScheduleRefresh();
            }

            return;
        }

        _isRefreshing = true;
        var showLoading = allowLoadingOverlay && _showWorkspaceLoading;
        if (showLoading)
        {
            SetWorkspaceLoadingVisible(true);
        }

        try
        {
            var instanceList = professionalInstances.ToList();
            var allowedIds = instanceList
                .Select(instance => instance.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var availableBranchKeys = BranchWorkspaceHelper.CollectBranchKeys(
                instanceList,
                _services.ThreadRegistry.GetAllThreads()
                    .Where(thread => allowedIds.Contains(thread.InstanceId)));

            if (!string.IsNullOrWhiteSpace(WorkspaceBranchKey) &&
                availableBranchKeys.All(branch =>
                    !branch.Equals(WorkspaceBranchKey, StringComparison.OrdinalIgnoreCase)))
            {
                _services.OccFilter.Clear();
            }

            var scopedThreads = _services.ThreadRegistry.GetAllThreads()
                .Where(thread => allowedIds.Contains(thread.InstanceId))
                .ToList();
            var instanceById = instanceList.ToDictionary(
                instance => instance.Id,
                StringComparer.OrdinalIgnoreCase);
            _branchTabCounts = BranchWorkspaceHelper.ComputeBranchTabCounts(scopedThreads, instanceById);

            var fromUtc = _services.OccDateRangeFilter.FromUtc;
            var toUtc = _services.OccDateRangeFilter.ToUtc;
            var snapshot = await Task.Run(() =>
                    _services.OperationsCommandCenter.BuildSnapshot(
                        instanceList,
                        WorkspaceBranchKey,
                        fromUtc,
                        toUtc))
                .ConfigureAwait(true);

            if (allowLoadingOverlay)
            {
                RebuildBranchPills(instanceList);
            }

            ApplySnapshot(snapshot);
            ApplyBackfillStatusUi();
        }
        finally
        {
            SetWorkspaceLoadingVisible(false);
            _showWorkspaceLoading = false;
            _isRefreshing = false;
        }
    }

    private void ApplySnapshot(OperationsCommandCenterSnapshot snapshot)
    {
        _snapshot = snapshot;
        var threadOps = snapshot.ThreadOperations;
        var hasProfessional = _professionalInstances.Any();

        _viewModel.ApplyShellPresentation(OccSnapshotPresenter.BuildShellPresentation(
            hasProfessional,
            snapshot.ScopeLabel,
            DateTime.Now));
        ApplyShellUi();
        ApplyStatusKpis(OccSnapshotPresenter.BuildStatusKpis(snapshot.Status));
        ApplyScopeLabel(snapshot.ScopeLabel, null);
        ApplyKanban(threadOps);
        ApplyImmediateQueue(threadOps);
        ApplyAnalyticsTrends(snapshot.AnalyticsTrends);
        MarkSnapshotReadyForAutomation();
    }

    private void MarkSnapshotReadyForAutomation()
    {
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(
            ScopeText,
            ViewAutomationIds.OccSnapshotReady);
    }

    private void ApplyShellPresentation(bool hasProfessional, OperationsStatusSnapshot status)
    {
        var branchScope = _services.OccFilter.BranchKey is { Length: > 0 } branchKey
            ? $"Showing: {branchKey}"
            : "Showing: All Branches";
        _viewModel.ApplyShellPresentation(OccSnapshotPresenter.BuildShellPresentation(
            hasProfessional,
            branchScope,
            DateTime.Now));
        ApplyShellUi();
        ApplyStatusKpis(OccSnapshotPresenter.BuildStatusKpis(status));
        ApplyScopeLabel(null, branchScope);
    }

    private void ApplyShellUi()
    {
        EmptyStatePanel.Visibility = _viewModel.ShowEmptyState ? Visibility.Visible : Visibility.Collapsed;
        MainContentScrollViewer.Visibility = _viewModel.ShowMainContent ? Visibility.Visible : Visibility.Collapsed;
        LastRefreshedText.Text = _viewModel.LastRefreshedText;
        ApplyBranchFilterChip();
    }

    private void ApplyBranchFilterChip()
    {
        var branchKey = _services.OccFilter.BranchKey;
        var hasFilter = !string.IsNullOrWhiteSpace(branchKey);
        BranchFilterChipPanel.Visibility = hasFilter ? Visibility.Visible : Visibility.Collapsed;
        if (hasFilter)
        {
            BranchFilterChipText.Text = $"Branch: {branchKey}";
        }
    }

    private void ApplyStatusKpis(OccStatusKpiPresentation kpis)
    {
        OpenThreadsCard.Value = kpis.OpenThreadCount;
        HangingLeadsCard.Value = kpis.HangingLeadCount;
        ImmediateActionCard.Value = kpis.ImmediateActionCount;
        SlaBreachesCard.Value = kpis.SlaBreaches;

        OpenThreadsCard.IsEnabled = ParseKpiCount(kpis.OpenThreadCount) > 0;
        HangingLeadsCard.IsEnabled = ParseKpiCount(kpis.HangingLeadCount) > 0;
        ImmediateActionCard.IsEnabled = ParseKpiCount(kpis.ImmediateActionCount) > 0;
        SlaBreachesCard.IsEnabled = ParseKpiCount(kpis.SlaBreaches) > 0;

        if (!string.IsNullOrWhiteSpace(kpis.ImmediateActionTooltip))
        {
            ImmediateActionCard.NavigationTooltip = kpis.ImmediateActionTooltip;
        }
    }

    private static int ParseKpiCount(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "—")
        {
            return 0;
        }

        return int.TryParse(value, out var count) ? count : 0;
    }

    private void ApplyScopeLabel(string? snapshotScopeLabel, string? statusBranchScope)
    {
        var branchScope = snapshotScopeLabel
            ?? statusBranchScope
            ?? _viewModel.ScopeLabel;
        if (string.IsNullOrWhiteSpace(branchScope))
        {
            branchScope = "Showing: All Branches";
        }

        ScopeText.Text = OccDateRangeFilterHelper.FormatScopeLabel(
            branchScope,
            _services.OccDateRangeFilter.FromUtc,
            _services.OccDateRangeFilter.ToUtc);
    }

    private void RebuildBranchPills(IReadOnlyList<MessengerInstance> instanceList)
    {
        var branchKeys = BranchWorkspaceHelper.CollectBranchKeys(
            instanceList,
            _services.ThreadRegistry.GetAllThreads()
                .Where(thread => instanceList.Any(i =>
                    i.Id.Equals(thread.InstanceId, StringComparison.OrdinalIgnoreCase))));

        var presentation = OccSnapshotPresenter.BuildPillBar(_branchTabCounts, branchKeys);

        if (presentation.Signature == _lastPillBarSignature)
        {
            return;
        }

        _lastPillBarSignature = presentation.Signature;
        _suppressPillSelection = true;
        BranchWorkspacePillBar.SetItems(presentation.Items, WorkspaceBranchKey);
        _suppressPillSelection = false;
    }

    private void SetWorkspaceLoadingVisible(bool visible)
    {
        WorkspaceLoadingOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        BranchWorkspacePillBar.IsInteractionEnabled = !visible;
        _viewModel.ShowWorkspaceLoading = visible;
    }

    private void OnBranchWorkspacePillSelectionChanged(object? sender, string? branchKey)
    {
        if (_suppressPillSelection)
        {
            return;
        }

        if (string.Equals(WorkspaceBranchKey, branchKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        WorkspaceBranchKey = branchKey;
        _showWorkspaceLoading = true;
        _ = RefreshAsync(_professionalInstances, _registry);
    }

    private void OnOccFilterStateChanged(object? sender, EventArgs e) =>
        _dispatcherQueue.TryEnqueue(() =>
        {
            ApplyBranchFilterChip();
            if (!_suppressPillSelection)
            {
                _suppressPillSelection = true;
                BranchWorkspacePillBar.SelectBranchKey(_services.OccFilter.BranchKey);
                _suppressPillSelection = false;
            }
        });

    private void ClearBranchFilterButton_Click(object sender, RoutedEventArgs e) =>
        SelectWorkspaceBranch(null);

    private void AddProfessionalInstanceButton_Click(object sender, RoutedEventArgs e) =>
        _services.Navigation.RequestAddInstance();

    private async void RefreshCommandButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshCommandButton.IsEnabled = false;
        try
        {
            _showWorkspaceLoading = true;
            await RefreshAsync(_professionalInstances, _registry).ConfigureAwait(true);
        }
        finally
        {
            RefreshCommandButton.IsEnabled = true;
        }
    }
}
