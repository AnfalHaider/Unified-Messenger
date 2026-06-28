using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;
using Windows.System;

namespace UnifiedMessenger.Controls;

public sealed partial class WorkspaceSidebar : Grid
{
    private readonly WorkspaceSidebarViewModel _viewModel = new();
    private ApplicationServices _services = ApplicationServiceProvider.Current;
    private readonly Dictionary<string, Border> _instanceRows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InfoBadge> _instanceBadges = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Ellipse> _instanceStatusDots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBlock> _instanceStatusLabels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBlock> _instanceTitleLabels = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FrameworkElement> _compactHiddenElements = [];

    // Collapsible location groups: which "loc:*" groups are collapsed (persists across refreshes), the
    // member row elements per group, and each group header's chevron so it can flip on toggle.
    private readonly HashSet<string> _collapsedGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<UIElement>> _groupMembers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FontIcon> _groupChevrons = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, int> _groupCounts = new Dictionary<string, int>();

    private Border? _dashboardRow;
    private SidebarMenuPlan? _currentPlan;
    private string? _selectedKey = WorkspaceSidebarHelper.DashboardSelectionKey;
    private bool _isCompact;

    // Scope switch (Shell IA): remembers the last Refresh args so a scope change can re-render.
    private SidebarScope _scope = SidebarScope.All;
    private bool _scopeInitialized;
    private IReadOnlyList<MessengerInstance> _lastInstances = [];
    private string? _lastSelectedInstanceId;
    private bool _lastDashboardSelected;
    private bool _lastSettingsSelected;
    private bool _lastNotificationHubSelected;
    private bool _lastWorkQueueSelected;
    private int _nextSidebarTabIndex = AccessibilityTabOrderHelper.SidebarMenuBase;

    private static readonly Thickness s_compactRowPadding = new(6, 8, 4, 8);
    private static readonly Thickness s_normalRowPadding = new(10, 8, 8, 8);

    private const string FooterNotificationHubLabel = "Notification Hub";
    private const string FooterSettingsLabel = "Settings";

    public WorkspaceSidebarViewModel ViewModel => _viewModel;

    public void ConfigureServices(ApplicationServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    public WorkspaceSidebar()
    {
        InitializeComponent();
        ApplyBrandWordmarkForTheme();
        ActualThemeChanged += (_, _) => ApplyBrandWordmarkForTheme();
        Unloaded += OnUnloaded;
    }

    private void ApplyBrandWordmarkForTheme()
    {
        var useDarkWordmark = ActualTheme == ElementTheme.Dark;
        var assetPath = ApplicationPaths.TryResolveBrandingAssetPath(
            useDarkWordmark ? "wordmark-inline-dark.png" : "wordmark-inline-light.png");
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return;
        }

        BrandWordmarkImage.Source = new BitmapImage(new Uri(assetPath));
    }

    public event EventHandler? DashboardRequested;

    public event EventHandler<string>? InstanceRequested;

    public event EventHandler? AddInstanceRequested;

    public event EventHandler? NotificationsRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler<(string InstanceId, MessengerInstance Instance, FrameworkElement Anchor)>?
        InstanceContextRequested;

    public void Refresh(
        IEnumerable<MessengerInstance> instances,
        string? selectedInstanceId,
        bool dashboardSelected,
        bool settingsSelected = false,
        bool notificationHubSelected = false,
        bool workQueueSelected = false)
    {
        ArgumentNullException.ThrowIfNull(instances);

        var instanceList = instances as IReadOnlyList<MessengerInstance> ?? instances.ToList();
        _lastInstances = instanceList;
        _lastSelectedInstanceId = selectedInstanceId;
        _lastDashboardSelected = dashboardSelected;
        _lastSettingsSelected = settingsSelected;
        _lastNotificationHubSelected = notificationHubSelected;
        _lastWorkQueueSelected = workQueueSelected;
        EnsureScopeInitialized();

        _selectedKey = WorkspaceSidebarHelper.ResolveSelectionKey(
            dashboardSelected,
            selectedInstanceId,
            settingsSelected,
            notificationHubSelected,
            workQueueSelected);
        _viewModel.ApplySelection(
            dashboardSelected,
            selectedInstanceId,
            settingsSelected,
            notificationHubSelected,
            workQueueSelected);

        // The scope switch only applies (and shows) when both scopes have accounts; otherwise it would
        // hide the user's only scope. The selector itself lives in the title bar — tell it to show/hide.
        var hasMixed = WorkspaceSidebarMenuPlanner.HasMixedScopes(instanceList);
        ScopeSelectorStateChanged?.Invoke(this, EventArgs.Empty);
        var effectiveScope = hasMixed ? _scope : SidebarScope.All;

        var plan = WorkspaceSidebarMenuPlanner.BuildPlan(instanceList, effectiveScope);

        if (_currentPlan is not null && WorkspaceSidebarMenuPlanner.HasSameStructure(_currentPlan, plan))
        {
            ApplyInstanceContentUpdates(plan);
            ApplySelectionVisuals();
            ApplyCompactDisplay();
            return;
        }

        SyncMenuStack(plan);
        _currentPlan = plan;
        ApplySelectionVisuals();
        ApplyCompactDisplay();
    }

    private void EnsureScopeInitialized()
    {
        if (_scopeInitialized)
        {
            return;
        }

        _scopeInitialized = true;
        _scope = ParseScope(AppSettingsService.Instance.Settings.SidebarScopeFilter);
    }

    /// <summary>The current account scope. Driven by the title-bar scope selector.</summary>
    public SidebarScope Scope => _scope;

    /// <summary>
    /// Whether the scope selector should be shown — only when both scopes have accounts, so the user
    /// can't hide their only scope. The title bar subscribes to <see cref="ScopeSelectorStateChanged"/>.
    /// </summary>
    public bool ShouldShowScopeSelector => WorkspaceSidebarMenuPlanner.HasMixedScopes(_lastInstances);

    /// <summary>Raised when the mixed-scope state may have changed (on every Refresh) so the title-bar
    /// selector can update its visibility and reflect the current scope.</summary>
    public event EventHandler? ScopeSelectorStateChanged;

    /// <summary>Sets the account scope from the title-bar selector and re-renders with cached args.</summary>
    public void SetScope(SidebarScope scope)
    {
        EnsureScopeInitialized();
        if (_scope == scope)
        {
            return;
        }

        _scope = scope;
        _ = AppSettingsService.Instance.UpdateAsync(s => s.SidebarScopeFilter = _scope.ToString());

        Refresh(
            _lastInstances,
            _lastSelectedInstanceId,
            _lastDashboardSelected,
            _lastSettingsSelected,
            _lastNotificationHubSelected,
            _lastWorkQueueSelected);
    }

    private static SidebarScope ParseScope(string? value) => value switch
    {
        "Professional" => SidebarScope.Professional,
        "Personal" => SidebarScope.Personal,
        _ => SidebarScope.All
    };

    private void ApplyInstanceContentUpdates(SidebarMenuPlan plan)
    {
        foreach (var entry in plan.Entries)
        {
            if (entry.Kind != SidebarMenuEntryKind.Instance || entry.Instance is null)
            {
                continue;
            }

            var instanceId = entry.Instance.Id.Trim();
            if (_instanceRows.TryGetValue(instanceId, out var row))
            {
                UpdateInstanceRowContent(row, entry.Instance);
                UpdateInstanceHealth(instanceId, entry.Instance);
            }
        }
    }

    private void SyncMenuStack(SidebarMenuPlan plan)
    {
        _nextSidebarTabIndex = AccessibilityTabOrderHelper.SidebarMenuBase;
        _instanceRows.Clear();
        _instanceBadges.Clear();
        _instanceStatusDots.Clear();
        _instanceStatusLabels.Clear();
        _instanceTitleLabels.Clear();
        _compactHiddenElements.Clear();
        _groupMembers.Clear();
        _groupChevrons.Clear();
        _dashboardRow = null;

        // Member counts per location group, so each group header can show "DHA-2 · 3".
        _groupCounts = ComputeGroupCounts(plan);

        var desiredElements = new List<UIElement>(plan.Entries.Count);

        string? currentGroupKey = null;
        foreach (var entry in plan.Entries)
        {
            var element = GetOrCreateMenuElement(entry);
            desiredElements.Add(element);

            // Track which rows belong to a collapsible location group (entries between a "loc:" header and
            // the next header), so the header toggle can hide/show them.
            if (entry.Kind == SidebarMenuEntryKind.SectionHeader)
            {
                currentGroupKey = IsLocationGroupKey(entry.Key) ? entry.Key : null;
            }
            else if (entry.Kind == SidebarMenuEntryKind.Instance && currentGroupKey is not null)
            {
                if (!_groupMembers.TryGetValue(currentGroupKey, out var members))
                {
                    members = [];
                    _groupMembers[currentGroupKey] = members;
                }

                members.Add(element);
            }
        }

        // Rebuild the menu order by detaching everything and re-adding in the desired order. The previous
        // incremental reconciliation could Insert a cached element that was still parented at another index
        // within MenuStack — WinUI mishandles re-parenting inside the same panel, which wedged the layout
        // pass and froze the app on right-click "Move up/down". Clear + re-add can never double-parent and
        // is flicker-free at this list size (a handful of rows).
        MenuStack.Children.Clear();
        foreach (var element in desiredElements)
        {
            MenuStack.Children.Add(element);
        }

        ApplyGroupCollapseState();

        // Drop collapse state for groups that no longer exist so it can't leak across scope/location changes.
        _collapsedGroups.RemoveWhere(key => !_groupMembers.ContainsKey(key));
    }

    private static bool IsLocationGroupKey(string? key) =>
        key is not null && key.StartsWith("loc:", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, int> ComputeGroupCounts(SidebarMenuPlan plan)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string? currentGroupKey = null;
        foreach (var entry in plan.Entries)
        {
            if (entry.Kind == SidebarMenuEntryKind.SectionHeader)
            {
                currentGroupKey = IsLocationGroupKey(entry.Key) ? entry.Key : null;
            }
            else if (entry.Kind == SidebarMenuEntryKind.Instance && currentGroupKey is not null)
            {
                counts[currentGroupKey] = counts.GetValueOrDefault(currentGroupKey) + 1;
            }
        }

        return counts;
    }

    /// <summary>Hide/show each collapsible group's member rows and flip its chevron. Collapse only applies
    /// in the expanded sidebar; the compact icon rail always shows every row.</summary>
    private void ApplyGroupCollapseState()
    {
        foreach (var (groupKey, members) in _groupMembers)
        {
            var collapsed = !_isCompact && _collapsedGroups.Contains(groupKey);
            foreach (var member in members)
            {
                if (member is FrameworkElement element)
                {
                    element.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
                }
            }

            if (_groupChevrons.TryGetValue(groupKey, out var chevron))
            {
                chevron.Glyph = collapsed ? "" : ""; // ChevronRight / ChevronDown
            }
        }
    }

    private void ToggleLocationGroup(string groupKey)
    {
        if (!_collapsedGroups.Remove(groupKey))
        {
            _collapsedGroups.Add(groupKey);
        }

        ApplyGroupCollapseState();
    }

    private UIElement GetOrCreateMenuElement(SidebarMenuEntry entry)
    {
        // Every element is recreated on a structural rebuild. SyncMenuStack clears the row-tracking
        // dictionaries (title/status labels, dots, badges) and _compactHiddenElements; those are only
        // re-populated by the Create* methods. Reusing a cached element therefore left its label refs
        // dangling — after an add/remove, pre-existing rows' titles fell out of _compactHiddenElements and
        // could blank out on the next compact↔expand cycle (names vanished), and their status/badge stopped
        // updating. Recreating a handful of lightweight rows is cheap and keeps every reference consistent.
        UIElement created = entry.Kind switch
        {
            SidebarMenuEntryKind.SectionHeader => IsLocationGroupKey(entry.Key)
                ? CreateLocationHeader(entry.SectionTitle ?? string.Empty, entry.Key)
                : CreateSectionHeader(entry.SectionTitle ?? string.Empty, entry.Key),
            SidebarMenuEntryKind.Dashboard => CreateDashboardRow(),
            SidebarMenuEntryKind.EmptyHint => CreateEmptyHint(entry.HintText ?? string.Empty, entry.Key),
            SidebarMenuEntryKind.Instance when entry.Instance is not null => CreateInstanceRow(entry.Instance),
            _ => throw new InvalidOperationException($"Unsupported sidebar entry: {entry.Key}")
        };

        if (created is FrameworkElement frameworkElement)
        {
            frameworkElement.Tag = entry.Key;
        }

        return created;
    }

    private void RegisterInstanceRow(MessengerInstance instance, Border row)
    {
        var instanceId = instance.Id.Trim();
        _instanceRows[instanceId] = row;
        UpdateInstanceHealth(instanceId, instance);
    }

    private void UpdateInstanceRowContent(Border row, MessengerInstance instance)
    {
        var instanceId = instance.Id.Trim();
        if (_instanceTitleLabels.TryGetValue(instanceId, out var titleLabel))
        {
            titleLabel.Text = instance.DisplayName;
        }

        ToolTipService.SetToolTip(row, instance.DisplayName);
        if (_instanceStatusLabels.TryGetValue(instanceId, out var statusLabel))
        {
            UpdateSelectableRowAccessibility(
                row,
                instanceId,
                instance.DisplayName,
                statusLabel.Text,
                WorkspaceSidebarHelper.IsSelectionMatch(_selectedKey, instanceId),
                ResolveBadgeCount(instanceId));
        }
    }

    public void SetCompactDisplay(bool compact)
    {
        _isCompact = compact;
        ApplyCompactDisplay();
    }

    /// <summary>
    /// Drops the cached plan so the next <see cref="Refresh"/> does a full structural rebuild (recreating
    /// rows + avatars). Needed when something the incremental path doesn't track changed — e.g. an account's
    /// avatar icon — otherwise the icon-only change wouldn't appear.
    /// </summary>
    public void InvalidatePlan() => _currentPlan = null;

    private void ApplyCompactDisplay()
    {
        var labelVisibility = _isCompact ? Visibility.Collapsed : Visibility.Visible;
        BrandWordmarkImage.Visibility = labelVisibility;
        AddInstanceLabel.Visibility = labelVisibility;
        NotificationsLabel.Visibility = labelVisibility;
        SettingsLabel.Visibility = labelVisibility;

        MenuRoot.Padding = _isCompact ? ResolveThickness("UmPaddingSidebarMenuCompact", new Thickness(6, 12, 6, 8)) : ResolveThickness("UmPaddingSidebarMenu", new Thickness(8, 12, 8, 8));
        FooterPanel.Padding = _isCompact ? ResolveThickness("UmPaddingFooterCompact", new Thickness(8, 10, 8, 12)) : ResolveThickness("UmPaddingFooter", new Thickness(12, 10, 12, 12));

        foreach (var element in _compactHiddenElements)
        {
            element.Visibility = labelVisibility;
        }

        foreach (var row in _instanceRows.Values)
        {
            row.Padding = _isCompact ? s_compactRowPadding : s_normalRowPadding;
        }

        if (_dashboardRow is not null)
        {
            _dashboardRow.Padding = _isCompact ? s_compactRowPadding : s_normalRowPadding;
        }

        // Re-apply collapse after a density change: compact rail shows every row; expanded honors collapse.
        ApplyGroupCollapseState();
    }

    public void SetSelection(
        bool dashboardSelected,
        string? instanceId,
        bool settingsSelected = false,
        bool notificationHubSelected = false,
        bool workQueueSelected = false)
    {
        _selectedKey = WorkspaceSidebarHelper.ResolveSelectionKey(
            dashboardSelected,
            instanceId,
            settingsSelected,
            notificationHubSelected,
            workQueueSelected);
        _viewModel.ApplySelection(
            dashboardSelected,
            instanceId,
            settingsSelected,
            notificationHubSelected,
            workQueueSelected);
        ApplySelectionVisuals();
    }

    public void UpdateInstanceBadge(string instanceId, int count, MessengerInstance? instance = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId) ||
            !_instanceBadges.TryGetValue(instanceId.Trim(), out var badge))
        {
            return;
        }

        var clampedCount = WorkspaceSidebarHelper.ClampBadgeCount(count);
        if (clampedCount > 0)
        {
            badge.Value = clampedCount;
            badge.Visibility = Visibility.Visible;
            if (instance is not null)
            {
                badge.Background = PlatformBrandingHelper.GetAccentBrush(instance);
            }
        }
        else
        {
            badge.Visibility = Visibility.Collapsed;
        }

        if (_instanceRows.TryGetValue(instanceId.Trim(), out var row) &&
            _instanceTitleLabels.TryGetValue(instanceId.Trim(), out var titleLabel) &&
            _instanceStatusLabels.TryGetValue(instanceId.Trim(), out var statusLabel))
        {
            UpdateSelectableRowAccessibility(
                row,
                instanceId.Trim(),
                titleLabel.Text,
                statusLabel.Text,
                WorkspaceSidebarHelper.IsSelectionMatch(_selectedKey, instanceId.Trim()),
                clampedCount);
        }
    }

    public void UpdateNotificationHubBadge(int total)
    {
        _viewModel.ApplyNotificationHubBadge(total);
        var clampedTotal = _viewModel.NotificationHubBadgeCount;
        if (clampedTotal > 0)
        {
            NotificationHubBadge.Value = clampedTotal;
            NotificationHubBadge.Visibility = Visibility.Visible;
        }
        else
        {
            NotificationHubBadge.Visibility = Visibility.Collapsed;
        }

        UpdateFooterButtonAccessibility(
            NotificationsButton,
            FooterNotificationHubLabel,
            WorkspaceSidebarHelper.IsSelectionMatch(
                _selectedKey,
                WorkspaceSidebarHelper.NotificationHubSelectionKey),
            clampedTotal);
    }

    public void UpdateInstanceHealth(string instanceId, MessengerInstance instance)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var normalizedId = instanceId.Trim();
        var connectionStatus = _services.ConnectionStatus.GetStatus(normalizedId);
        var adapterStatus = _services.AdapterHealth.GetStatus(normalizedId);
        var detail = _services.ConnectionStatus.GetDetail(normalizedId);

        if (_instanceStatusDots.TryGetValue(normalizedId, out var dot))
        {
            dot.Fill = new SolidColorBrush(
                WorkspaceSidebarHelper.ResolveConnectionIndicatorColor(connectionStatus, adapterStatus.State));
        }

        var statusSubtitle = WorkspaceSidebarHelper.ResolveStatusSubtitle(
            connectionStatus,
            adapterStatus.State,
            instance.NotificationsMuted,
            detail);
        var displaySubtitle = WorkspaceSidebarHelper.ComposeRowSubtitle(
            instance.Platform, connectionStatus, instance.NotificationsMuted);

        if (_instanceStatusLabels.TryGetValue(normalizedId, out var statusLabel))
        {
            statusLabel.Text = displaySubtitle;
        }

        if (_instanceRows.TryGetValue(normalizedId, out var row) &&
            _instanceTitleLabels.TryGetValue(normalizedId, out var titleLabel))
        {
            ToolTipService.SetToolTip(
                row,
                WorkspaceSidebarHelper.ComposeInstanceTooltip(
                    instance.DisplayName,
                    instance.Category,
                    statusSubtitle,
                    adapterStatus.Description,
                    instance.MemoryTier,
                    detail));
            UpdateSelectableRowAccessibility(
                row,
                normalizedId,
                titleLabel.Text,
                displaySubtitle,
                WorkspaceSidebarHelper.IsSelectionMatch(_selectedKey, normalizedId),
                ResolveBadgeCount(normalizedId));
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
    }

    private UIElement CreateSectionHeader(string title, string key)
    {
        var marginKey = string.Equals(key, WorkspaceSidebarMenuPlanner.OverviewHeaderKey, StringComparison.OrdinalIgnoreCase)
            ? "UmMarginSectionHeaderFirst"
            : string.Equals(key, WorkspaceSidebarMenuPlanner.ActiveAccountsHeaderKey, StringComparison.OrdinalIgnoreCase)
                ? "UmMarginActiveAccountsHeader"
                : "UmMarginSectionHeader";

        var header = new TextBlock
        {
            Tag = key,
            Text = title,
            Margin = ResolveThickness(marginKey, new Thickness(4, 8, 4, 4))
        };

        if (Application.Current.Resources.TryGetValue("UmSectionLabelTextStyle", out var styleResource) &&
            styleResource is Style sectionStyle)
        {
            header.Style = sectionStyle;
        }
        else
        {
            header.FontSize = ResolveDouble("UmFontSizeSectionLabel", 11);
            header.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            header.Opacity = ResolveDouble("UmOpacityHint", 0.75);
            header.CharacterSpacing = 40;
        }

        AutomationProperties.SetName(header, WorkspaceSidebarAccessibility.ComposeSectionHeaderName(title));
        _compactHiddenElements.Add(header);
        return header;
    }

    /// <summary>
    /// A collapsible location-group header: chevron + location name + member count, clickable (and keyboard
    /// togglable) to collapse/expand the accounts in that location. Modernizes the old plain text sub-header.
    /// </summary>
    private Border CreateLocationHeader(string title, string key)
    {
        var count = _groupCounts.TryGetValue(key, out var c) ? c : 0;
        var secondary = ResolveBrush("TextFillColorSecondaryBrush")
            ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));

        var chevron = new FontIcon
        {
            Glyph = _collapsedGroups.Contains(key) ? "" : "",
            FontSize = 12,
            Foreground = secondary,
            VerticalAlignment = VerticalAlignment.Center
        };
        _groupChevrons[key] = chevron;

        var titleBlock = new TextBlock
        {
            Text = title,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        if (Application.Current.Resources.TryGetValue("UmSectionLabelTextStyle", out var styleResource) &&
            styleResource is Style sectionStyle)
        {
            titleBlock.Style = sectionStyle;
        }
        else
        {
            titleBlock.FontSize = ResolveDouble("UmFontSizeSectionLabel", 11);
            titleBlock.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            titleBlock.Opacity = ResolveDouble("UmOpacityHint", 0.75);
            titleBlock.CharacterSpacing = 40;
        }

        var countBlock = new TextBlock
        {
            Text = count.ToString(),
            FontSize = 11,
            Foreground = secondary,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center
        };

        var grid = new Grid { ColumnSpacing = 6, VerticalAlignment = VerticalAlignment.Center };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(chevron, 0);
        Grid.SetColumn(titleBlock, 1);
        Grid.SetColumn(countBlock, 2);
        grid.Children.Add(chevron);
        grid.Children.Add(titleBlock);
        grid.Children.Add(countBlock);

        var header = new Border
        {
            Tag = key,
            Padding = new Thickness(4, 8, 6, 4),
            CornerRadius = ResolveCornerRadius("UmCornerRadiusSmValue", new CornerRadius(4)),
            Background = ResolveTransparentBrush(),
            IsTabStop = true,
            TabIndex = _nextSidebarTabIndex++,
            UseSystemFocusVisuals = true,
            Child = grid
        };

        header.PointerPressed += (_, _) => ToggleLocationGroup(key);
        header.KeyDown += (_, e) =>
        {
            if (e.Key is VirtualKey.Enter or VirtualKey.Space)
            {
                ToggleLocationGroup(key);
                e.Handled = true;
            }
        };

        AutomationProperties.SetName(header,
            $"{title} location, {count} {(count == 1 ? "account" : "accounts")}, press to collapse or expand");
        _compactHiddenElements.Add(header);
        return header;
    }

    private TextBlock CreateEmptyHint(string text, string key)
    {
        var hint = new TextBlock
        {
            Tag = key,
            Text = text,
            Opacity = ResolveDouble("UmOpacitySubtle", 0.55),
            Margin = ResolveThickness("UmPaddingSm", new Thickness(8, 0, 8, 4)),
            TextWrapping = TextWrapping.WrapWholeWords,
            MinWidth = 0,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        if (ResolveTextStyle("UmBodyTextStyle") is { } bodyStyle)
        {
            hint.Style = bodyStyle;
        }
        AutomationProperties.SetName(hint, text);
        _compactHiddenElements.Add(hint);
        return hint;
    }

    private Border CreateDashboardRow()
    {
        var row = CreateSelectableRow(WorkspaceSidebarHelper.DashboardSelectionKey, null, "Dashboard", "Overview", null);
        row.PointerPressed += DashboardRow_PointerPressed;
        row.KeyDown += DashboardRow_KeyDown;
        _dashboardRow = row;
        return row;
    }

    private void DashboardRow_PointerPressed(object sender, PointerRoutedEventArgs e) =>
        ActivateDashboardRow();

    private void DashboardRow_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Enter or VirtualKey.Space)
        {
            ActivateDashboardRow();
            e.Handled = true;
        }
    }

    private void ActivateDashboardRow() =>
        DashboardRequested?.Invoke(this, EventArgs.Empty);

    private static FrameworkElement CreateFallbackIcon(string glyph, SolidColorBrush accentBrush, double size)
    {
        var host = new Grid { Width = size, Height = size };
        host.Children.Add(new Ellipse { Width = size, Height = size, Fill = accentBrush });
        host.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontSize = size * 0.5,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        return host;
    }

    private Border CreateInstanceRow(MessengerInstance instance)
    {
        var instanceId = instance.Id.Trim();
        var connectionStatus = _services.ConnectionStatus.GetStatus(instanceId);
        var adapterState = _services.AdapterHealth.GetStatus(instanceId).State;
        var connectionDetail = _services.ConnectionStatus.GetDetail(instanceId);
        var statusSubtitle = WorkspaceSidebarHelper.ResolveStatusSubtitle(
            connectionStatus,
            adapterState,
            instance.NotificationsMuted,
            connectionDetail);
        // Subtitle = channel name when healthy (e.g. "WhatsApp", "Meta Business Suite"), surfacing only real
        // problems (signed out / error). statusSubtitle is kept for the tooltip.
        var subtitle = WorkspaceSidebarHelper.ComposeRowSubtitle(
            instance.Platform, connectionStatus, instance.NotificationsMuted);
        var row = CreateSelectableRow(
            instanceId,
            instance,
            instance.DisplayName,
            subtitle,
            PlatformBrandingHelper.GetAccentBrush(instance));

        row.PointerPressed += (sender, e) => InstanceRow_PointerPressed(sender, e, instanceId, instance, row);
        row.Tapped += (_, _) => InstanceRequested?.Invoke(this, instanceId);
        row.KeyDown += (sender, e) => InstanceRow_KeyDown(sender, e, instanceId, instance, row);
        ToolTipService.SetToolTip(
            row,
            WorkspaceSidebarHelper.ComposeInstanceTooltip(
                instance.DisplayName,
                instance.Category,
                statusSubtitle,
                _services.AdapterHealth.GetStatus(instanceId).Description,
                instance.MemoryTier,
                connectionDetail));

        RegisterInstanceRow(instance, row);
        return row;
    }

    private void InstanceRow_PointerPressed(
        object sender,
        PointerRoutedEventArgs e,
        string instanceId,
        MessengerInstance instance,
        Border row)
    {
        // Right-click → context menu. Left-click navigation is handled on Tapped (not PointerPressed):
        // navigating on press triggers a heavy WebView switch the instant a drag begins, which freezes
        // the drag. Tapped fires only on a click without a drag, so dragging no longer navigates.
        if (e.GetCurrentPoint(row).Properties.IsRightButtonPressed)
        {
            InstanceContextRequested?.Invoke(this, (instanceId, instance, row));
        }
    }

    private void InstanceRow_KeyDown(
        object sender,
        KeyRoutedEventArgs e,
        string instanceId,
        MessengerInstance instance,
        Border row)
    {
        if (e.Key is not (VirtualKey.Enter or VirtualKey.Space))
        {
            return;
        }

        InstanceRequested?.Invoke(this, instanceId);
        e.Handled = true;
    }

    private Border CreateSelectableRow(
        string key,
        MessengerInstance? instance,
        string title,
        string subtitle,
        SolidColorBrush? accentBrush)
    {
        accentBrush ??= PlatformBrandingHelper.GetAccentBrush((string?)null);

        var row = new Border
        {
            Tag = key,
            Padding = new Thickness(10, 10, 8, 10),
            CornerRadius = ResolveCornerRadius("UmCornerRadiusMdValue", new CornerRadius(8)),
            Background = ResolveTransparentBrush(),
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush = ResolveTransparentBrush(),
            IsTabStop = true,
            TabIndex = _nextSidebarTabIndex++,
            UseSystemFocusVisuals = true
        };

        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconHost = instance is not null
            ? ProfileAvatarService.CreateAvatar(instance, 28)
            : (FrameworkElement)CreateFallbackIcon("\uE9D9", accentBrush, 28);
        iconHost.VerticalAlignment = VerticalAlignment.Center;

        var textPanel = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 0
        };
        var titleLabel = new TextBlock
        {
            Text = title,
            MinWidth = 0,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            MaxLines = 1
        };
        if (ResolveTextStyle("UmBodyTextStyle") is { } titleStyle)
        {
            titleLabel.Style = titleStyle;
        }
        textPanel.Children.Add(titleLabel);
        if (!WorkspaceSidebarHelper.IsSelectionMatch(key, WorkspaceSidebarHelper.DashboardSelectionKey))
        {
            _instanceTitleLabels[key] = titleLabel;
        }
        var statusLabel = new TextBlock
        {
            Text = subtitle,
            MinWidth = 0,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            MaxLines = 1
        };
        if (ResolveTextStyle("UmCaptionTextStyle") is { } captionStyle)
        {
            statusLabel.Style = captionStyle;
        }
        else
        {
            statusLabel.Opacity = ResolveDouble("UmOpacityMuted", 0.65);
        }
        textPanel.Children.Add(statusLabel);
        _compactHiddenElements.Add(textPanel);

        var trailing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        var statusDot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = ResolveBrush("TextFillColorSecondaryBrush")
                  ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128))
        };

        var badge = new InfoBadge
        {
            Visibility = Visibility.Collapsed
        };

        trailing.Children.Add(statusDot);
        trailing.Children.Add(badge);

        grid.Children.Add(iconHost);
        Grid.SetColumn(iconHost, 0);
        grid.Children.Add(textPanel);
        Grid.SetColumn(textPanel, 1);
        grid.Children.Add(trailing);
        Grid.SetColumn(trailing, 2);

        row.Child = grid;
        ToolTipService.SetToolTip(row, $"{title}\n{subtitle}");

        if (!WorkspaceSidebarHelper.IsSelectionMatch(key, WorkspaceSidebarHelper.DashboardSelectionKey))
        {
            _instanceStatusDots[key] = statusDot;
            _instanceStatusLabels[key] = statusLabel;
            _instanceBadges[key] = badge;
        }

        UpdateSelectableRowAccessibility(
            row,
            key,
            title,
            subtitle,
            WorkspaceSidebarHelper.IsSelectionMatch(_selectedKey, key),
            ResolveBadgeCount(key));

        return row;
    }

    private int ResolveBadgeCount(string key)
    {
        if (!_instanceBadges.TryGetValue(key, out var badge) ||
            badge.Visibility != Visibility.Visible)
        {
            return 0;
        }

        return (int)badge.Value;
    }

    private static void UpdateSelectableRowAccessibility(
        Border row,
        string key,
        string title,
        string subtitle,
        bool selected,
        int badgeCount)
    {
        AutomationProperties.SetAutomationId(row, WorkspaceSidebarAccessibility.ResolveRowAutomationId(key));
        AutomationProperties.SetName(
            row,
            WorkspaceSidebarHelper.IsSelectionMatch(key, WorkspaceSidebarHelper.DashboardSelectionKey)
                ? WorkspaceSidebarAccessibility.ComposeDashboardName(selected)
                : WorkspaceSidebarAccessibility.ComposeInstanceName(title, subtitle, badgeCount, selected));
    }

    private void ApplySelectionVisuals()
    {
        if (_dashboardRow is not null)
        {
            var dashboardSelected = WorkspaceSidebarHelper.IsSelectionMatch(
                _selectedKey,
                WorkspaceSidebarHelper.DashboardSelectionKey);
            ApplyRowSelection(_dashboardRow, dashboardSelected);
            UpdateSelectableRowAccessibility(
                _dashboardRow,
                WorkspaceSidebarHelper.DashboardSelectionKey,
                "Dashboard",
                "Overview",
                dashboardSelected,
                badgeCount: 0);
        }

        foreach (var (instanceId, row) in _instanceRows)
        {
            var selected = WorkspaceSidebarHelper.IsSelectionMatch(_selectedKey, instanceId);
            ApplyRowSelection(row, selected);
            if (_instanceTitleLabels.TryGetValue(instanceId, out var titleLabel) &&
                _instanceStatusLabels.TryGetValue(instanceId, out var statusLabel))
            {
                UpdateSelectableRowAccessibility(
                    row,
                    instanceId,
                    titleLabel.Text,
                    statusLabel.Text,
                    selected,
                    ResolveBadgeCount(instanceId));
            }
        }

        ApplyFooterButtonSelection(
            NotificationsButton,
            WorkspaceSidebarHelper.IsSelectionMatch(
                _selectedKey,
                WorkspaceSidebarHelper.NotificationHubSelectionKey));
        ApplyFooterButtonSelection(
            SettingsButton,
            WorkspaceSidebarHelper.IsSelectionMatch(_selectedKey, WorkspaceSidebarHelper.SettingsSelectionKey));
        UpdateFooterButtonAccessibility(
            NotificationsButton,
            FooterNotificationHubLabel,
            WorkspaceSidebarHelper.IsSelectionMatch(
                _selectedKey,
                WorkspaceSidebarHelper.NotificationHubSelectionKey),
            _viewModel.NotificationHubBadgeCount);
        UpdateFooterButtonAccessibility(
            SettingsButton,
            FooterSettingsLabel,
            WorkspaceSidebarHelper.IsSelectionMatch(
                _selectedKey,
                WorkspaceSidebarHelper.SettingsSelectionKey),
            badgeCount: 0);
    }

    private static void UpdateFooterButtonAccessibility(
        Button button,
        string label,
        bool selected,
        int badgeCount)
    {
        var name = badgeCount > 0
            ? $"{label}, {badgeCount} unread{(selected ? ", selected" : string.Empty)}"
            : selected
                ? $"{label}, selected"
                : label;
        AutomationProperties.SetName(button, name);
    }

    private static void ApplyFooterButtonSelection(Button button, bool selected)
    {
        button.Background = selected
            ? ResolveBrush("CardBackgroundFillColorDefaultBrush")
              ?? ResolveBrush("LayerFillColorDefaultBrush")
            : ResolveTransparentBrush();

        button.BorderThickness = new Thickness(3, 0, 0, 0);
        button.BorderBrush = selected
            ? ResolveBrush("SystemFillColorSuccessBrush")
            : ResolveTransparentBrush();
    }

    private static void ApplyRowSelection(Border row, bool selected)
    {
        row.Background = selected
            ? ResolveBrush("CardBackgroundFillColorDefaultBrush")
              ?? ResolveBrush("LayerFillColorDefaultBrush")
            : ResolveTransparentBrush();

        row.BorderThickness = new Thickness(3, 0, 0, 0);
        row.BorderBrush = selected
            ? ResolveBrush("SystemFillColorSuccessBrush")
            : ResolveTransparentBrush();
    }

    private void AddInstanceButton_Click(object sender, RoutedEventArgs e) =>
        AddInstanceRequested?.Invoke(this, EventArgs.Empty);


    private void NotificationsButton_Click(object sender, RoutedEventArgs e) =>
        NotificationsRequested?.Invoke(this, EventArgs.Empty);

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private static Thickness ResolveThickness(string key, Thickness fallback) =>
        Application.Current.Resources.TryGetValue(key, out var resource) && resource is Thickness thickness
            ? thickness
            : fallback;

    private static CornerRadius ResolveCornerRadius(string key, CornerRadius fallback) =>
        Application.Current.Resources.TryGetValue(key, out var resource) && resource is CornerRadius radius
            ? radius
            : fallback;

    private static Style? ResolveTextStyle(string key) =>
        Application.Current.Resources.TryGetValue(key, out var resource) && resource is Style style
            ? style
            : null;

    private static Brush ResolveTransparentBrush() =>
        ResolveBrush("UmTransparentBrush") ?? new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

    private static Brush? ResolveBrush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush
            ? brush
            : null;

    private static double ResolveDouble(string key, double fallback) =>
        Application.Current.Resources.TryGetValue(key, out var resource) && resource is double value
            ? value
            : fallback;
}


