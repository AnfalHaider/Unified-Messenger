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
    private string? _workspaceBranchKey;
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
        KeyDown += OnOccKeyDown;
        Loaded += OnLoaded;
    }

    public void ConfigureServices(ApplicationServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _services.ConfigureUi(() => XamlRoot);
    }

    public async Task RefreshAsync(
        IEnumerable<MessengerInstance> professionalInstances,
        IInstanceRegistryService? registry = null)
    {
        _professionalInstances = professionalInstances;
        _registry = registry;

        if (_isRefreshing)
        {
            DashboardRefreshCoordinator.Instance.ScheduleRefresh();
            return;
        }

        _isRefreshing = true;
        if (_showWorkspaceLoading)
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

            if (!string.IsNullOrWhiteSpace(_workspaceBranchKey) &&
                _availableBranchKeys.All(branch =>
                    !branch.Equals(_workspaceBranchKey, StringComparison.OrdinalIgnoreCase)))
            {
                _workspaceBranchKey = null;
            }

            var scopedThreads = _services.ThreadRegistry.GetAllThreads()
                .Where(thread => allowedIds.Contains(thread.InstanceId))
                .ToList();
            _branchTabCounts = BranchWorkspaceHelper.ComputeBranchTabCounts(scopedThreads);

            var snapshot = await Task.Run(() =>
                    OperationsCommandCenterService.Instance.BuildSnapshot(
                        instanceList,
                        _workspaceBranchKey))
                .ConfigureAwait(true);

            RebuildBranchPills(_availableBranchKeys);
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
            await DashboardScrapeOrchestrator.Instance
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
        _services.ConfigureUi(() => XamlRoot);

        WireScrollBubbling();
        ApplyLayoutPreferences();
        MaybeShowTeachingTips();
    }

    private void OnBranchWorkspacePillSelectionChanged(object? sender, string? branchKey)
    {
        if (_suppressPillSelection)
        {
            return;
        }

        if (string.Equals(_workspaceBranchKey, branchKey, StringComparison.OrdinalIgnoreCase))
        {
            RefreshBranchMetricSelection();
            return;
        }

        _workspaceBranchKey = branchKey;
        _showWorkspaceLoading = true;
        _ = RefreshAsync(_professionalInstances, _registry);
    }
}
