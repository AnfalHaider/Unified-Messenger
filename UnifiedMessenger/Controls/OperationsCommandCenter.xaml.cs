using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UnifiedMessenger.Controls.Occ;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter : UserControl
{
    private readonly OperationsCommandCenterViewModel _viewModel = new();
    private readonly ObservableCollection<BranchMetricViewModel> _branchMetrics = [];
    private readonly ObservableCollection<PlatformHealthViewModel> _platformHealth = [];
    private readonly ObservableCollection<HealthChipViewModel> _healthChips = [];

    private ApplicationServices _services = new();
    private DispatcherQueue _dispatcherQueue = null!;
    private IEnumerable<MessengerInstance> _professionalInstances = [];
    private OperationsCommandCenterSnapshot _snapshot = OperationsCommandCenterSnapshot.Empty;
    private IInstanceRegistryService? _registry;
    private string? WorkspaceBranchKey
    {
        get => _services.OccFilter.BranchKey;
        set => _services.OccFilter.BranchKey = value;
    }
    private IReadOnlyList<string> _availableBranchKeys = [];
    private bool _suppressPillSelection;
    private string? _lastPillBarSignature;
    private bool _isRefreshing;
    private bool _showWorkspaceLoading;
    private GoogleReviewAlertView? _selectedReviewAlert;
    private IReadOnlyDictionary<string, BranchWorkspaceHelper.BranchTabCounts> _branchTabCounts =
        new Dictionary<string, BranchWorkspaceHelper.BranchTabCounts>(StringComparer.OrdinalIgnoreCase);

    public OperationsCommandCenter()
    {
        InitializeComponent();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        PlatformIntelligenceExpander.Collapsed += OnPlatformIntelligenceCollapsed;
        AnalyticsTrendsExpander.Collapsed += OnAnalyticsTrendsCollapsed;

        BranchMetricsList.ItemsSource = _branchMetrics;
        PlatformHealthItems.ItemsSource = _platformHealth;
        HealthChipsItems.ItemsSource = _healthChips;
        ImmediateQueueList.ItemsSource = _viewModel.ImmediateQueue;
        KanbanBoard.BindCollections(_viewModel.NewInquiries, _viewModel.HangingLeads, _viewModel.Resolved);

        BranchWorkspacePillBar.SelectionChanged += OnBranchWorkspacePillSelectionChanged;
        _services.OccFilter.Changed += OnOccFilterStateChanged;
        KeyDown += OnOccKeyDown;
        Loaded += OnLoaded;
    }

    public void ConfigureServices(ApplicationServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    public Task RefreshAsync(
        IEnumerable<MessengerInstance> professionalInstances,
        IInstanceRegistryService? registry = null) =>
        RefreshCoreAsync(professionalInstances, registry, allowLoadingOverlay: true);

    /// <summary>
    /// Updates KPI counts and last-refreshed text while the Personal Overview tab is visible.
    /// Skips kanban, thread cards, and platform intelligence rebuilds.
    /// </summary>
    public Task RefreshLightAsync(
        IEnumerable<MessengerInstance> professionalInstances,
        IInstanceRegistryService? registry = null) =>
        RefreshKpisOnlyAsync(professionalInstances, registry);

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
                        WorkspaceBranchKey))
                .ConfigureAwait(true);

            ApplyStatusKpis(OccSnapshotPresenter.BuildStatusKpis(status));
            _viewModel.LastRefreshedText =
                OccSnapshotPresenter.BuildShellPresentation(
                    instanceList.Count > 0,
                    _viewModel.ScopeLabel,
                    DateTime.Now).LastRefreshedText;
            LastRefreshedText.Text = _viewModel.LastRefreshedText;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    public void ApplyAccessibilityTabOrder()
    {
        AccessibilityTabOrderHelper.ApplyTabIndex(
            RefreshCommandButton,
            AccessibilityTabOrderHelper.OccRefreshButton);
        AccessibilityTabOrderHelper.ApplyTabIndex(
            BranchWorkspacePillBar,
            AccessibilityTabOrderHelper.OccBranchPillBar);
        AccessibilityTabOrderHelper.ApplyTabIndex(
            LayoutEditToggleButton,
            AccessibilityTabOrderHelper.OccLayoutButton);
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

            _availableBranchKeys = BranchWorkspaceHelper.CollectBranchKeys(
                instanceList,
                _services.ThreadRegistry.GetAllThreads()
                    .Where(thread => allowedIds.Contains(thread.InstanceId)));

            if (!string.IsNullOrWhiteSpace(WorkspaceBranchKey) &&
                _availableBranchKeys.All(branch =>
                    !branch.Equals(WorkspaceBranchKey, StringComparison.OrdinalIgnoreCase)))
            {
                _services.OccFilter.Clear();
            }

            var scopedThreads = _services.ThreadRegistry.GetAllThreads()
                .Where(thread => allowedIds.Contains(thread.InstanceId))
                .ToList();
            _branchTabCounts = BranchWorkspaceHelper.ComputeBranchTabCounts(scopedThreads);

            var snapshot = await Task.Run(() =>
                    _services.OperationsCommandCenter.BuildSnapshot(
                        instanceList,
                        WorkspaceBranchKey))
                .ConfigureAwait(true);

            if (allowLoadingOverlay)
            {
                RebuildBranchPills(_availableBranchKeys);
            }

            ApplySnapshot(snapshot);
            ApplyBackfillStatusUi();
            await RefreshBranchPulseAsync().ConfigureAwait(true);
        }
        finally
        {
            SetWorkspaceLoadingVisible(false);
            _showWorkspaceLoading = false;
            _isRefreshing = false;
        }
    }

    private void SetWorkspaceLoadingVisible(bool visible)
    {
        WorkspaceLoadingOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        BranchWorkspacePillBar.IsInteractionEnabled = !visible;
        _viewModel.ShowWorkspaceLoading = visible;
    }

    public async Task<bool> RequestPlatformDataRefreshAsync(bool refreshAllInstances = false)
    {
        var targets = refreshAllInstances
            ? _professionalInstances.Where(DashboardScrapeOrchestrator.IsDashboardScrapeCapable).ToList()
            : _snapshot.FilteredInstances
                .Where(DashboardScrapeOrchestrator.IsDashboardScrapeCapable)
                .ToList();

        if (targets.Count == 0)
        {
            await ShowSimpleDialogAsync(
                "Nothing to refresh",
                refreshAllInstances
                    ? "No professional accounts are configured for platform data scraping."
                    : "No accounts in the current workspace scope support platform data scraping.")
                .ConfigureAwait(true);
            return false;
        }

        try
        {
            await _services.DashboardScrape
                .RefreshProfessionalInstancesAsync(targets)
                .ConfigureAwait(true);

            _services.MessageAnalytics.NotifyDashboardRefresh();
            await RefreshAsync(_professionalInstances, _registry).ConfigureAwait(true);
            return true;
        }
        catch (Exception ex)
        {
            await ShowSimpleDialogAsync("Platform refresh failed", ex.Message).ConfigureAwait(true);
            return false;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WireScrollBubbling();
        ApplyLayoutPreferences();
        ApplyAccessibilityTabOrder();
        MaybeShowTeachingTips();
    }

    private void OnBranchWorkspacePillSelectionChanged(object? sender, string? branchKey)
    {
        if (_suppressPillSelection)
        {
            return;
        }

        if (string.Equals(WorkspaceBranchKey, branchKey, StringComparison.OrdinalIgnoreCase))
        {
            RefreshBranchMetricSelection();
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

            RefreshBranchMetricSelection();
        });
}
