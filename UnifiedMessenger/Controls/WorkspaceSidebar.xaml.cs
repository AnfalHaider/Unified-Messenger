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
    private readonly Dictionary<string, UIElement> _menuElementCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FrameworkElement> _compactHiddenElements = [];

    private Border? _dashboardRow;
    private SidebarMenuPlan? _currentPlan;
    private string? _selectedKey = WorkspaceSidebarHelper.DashboardSelectionKey;
    private bool _isCompact;

    // Scope switch (Shell IA): remembers the last Refresh args so a scope change can re-render.
    private SidebarScope _scope = SidebarScope.All;
    private bool _scopeInitialized;
    private bool _suppressScopeChange;
    private IReadOnlyList<MessengerInstance> _lastInstances = [];
    private string? _lastSelectedInstanceId;
    private bool _lastDashboardSelected;
    private bool _lastSettingsSelected;
    private bool _lastNotificationHubSelected;
    private bool _lastWorkQueueSelected;
    private int _nextSidebarTabIndex = AccessibilityTabOrderHelper.SidebarMenuBase;

    private static readonly Thickness s_compactRowPadding = new(6, 8, 4, 8);
    private static readonly Thickness s_normalRowPadding = new(10, 10, 8, 10);

    private const string FooterWorkQueueLabel = "Work Queue";
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
        // Drag-to-reorder disabled (froze the app) — reorder is via the right-click Move up/down menu.
        MenuStack.AllowDrop = false;
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

    // Drag reorder is disabled (WinUI drag-in-ScrollViewer freeze bug); this event is subscribed externally
    // and will be re-wired when drag is re-enabled.
#pragma warning disable CS0067
    public event EventHandler<(string SourceInstanceId, string TargetInstanceId)>? InstanceReorderRequested;
#pragma warning restore CS0067

    public event EventHandler? DashboardRequested;

    public event EventHandler? WorkQueueRequested;

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
        // hide the user's only scope.
        var hasMixed = WorkspaceSidebarMenuPlanner.HasMixedScopes(instanceList);
        ScopeFilterCombo.Visibility = hasMixed ? Visibility.Visible : Visibility.Collapsed;
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

        _suppressScopeChange = true;
        ScopeFilterCombo.SelectedIndex = _scope switch
        {
            SidebarScope.Professional => 1,
            SidebarScope.Personal => 2,
            _ => 0
        };
        _suppressScopeChange = false;
    }

    private static SidebarScope ParseScope(string? value) => value switch
    {
        "Professional" => SidebarScope.Professional,
        "Personal" => SidebarScope.Personal,
        _ => SidebarScope.All
    };

    private void ScopeFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Ignore selection events until the sidebar has had its first real Refresh (the combo can fire
        // during construction/initialization before _services and cached args are ready).
        if (_suppressScopeChange || !_scopeInitialized)
        {
            return;
        }

        _scope = ((ScopeFilterCombo.SelectedItem as ComboBoxItem)?.Tag as string) switch
        {
            "Professional" => SidebarScope.Professional,
            "Personal" => SidebarScope.Personal,
            _ => SidebarScope.All
        };

        _ = AppSettingsService.Instance.UpdateAsync(s => s.SidebarScopeFilter = _scope.ToString());

        // Re-render with the cached args so the filter takes effect immediately.
        Refresh(
            _lastInstances,
            _lastSelectedInstanceId,
            _lastDashboardSelected,
            _lastSettingsSelected,
            _lastNotificationHubSelected,
            _lastWorkQueueSelected);
    }

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
        _dashboardRow = null;

        var desiredElements = new List<UIElement>(plan.Entries.Count);
        var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in plan.Entries)
        {
            activeKeys.Add(entry.Key);
            var element = GetOrCreateMenuElement(entry);
            desiredElements.Add(element);
        }

        for (var index = 0; index < desiredElements.Count; index++)
        {
            if (index < MenuStack.Children.Count &&
                ReferenceEquals(MenuStack.Children[index], desiredElements[index]))
            {
                continue;
            }

            if (index < MenuStack.Children.Count)
            {
                MenuStack.Children.RemoveAt(index);
            }

            MenuStack.Children.Insert(index, desiredElements[index]);
        }

        while (MenuStack.Children.Count > desiredElements.Count)
        {
            MenuStack.Children.RemoveAt(MenuStack.Children.Count - 1);
        }

        foreach (var staleKey in _menuElementCache.Keys.Where(key => !activeKeys.Contains(key)).ToList())
        {
            _menuElementCache.Remove(staleKey);
        }
    }

    private UIElement GetOrCreateMenuElement(SidebarMenuEntry entry)
    {
        if (_menuElementCache.TryGetValue(entry.Key, out var cached))
        {
            if (entry.Kind == SidebarMenuEntryKind.Dashboard && cached is Border dashboardRow)
            {
                _dashboardRow = dashboardRow;
            }

            if (entry.Kind == SidebarMenuEntryKind.Instance && entry.Instance is not null && cached is Border cachedRow)
            {
                UpdateInstanceRowContent(cachedRow, entry.Instance);
                RegisterInstanceRow(entry.Instance, cachedRow);
            }

            return cached;
        }

        UIElement created = entry.Kind switch
        {
            SidebarMenuEntryKind.SectionHeader => CreateSectionHeader(entry.SectionTitle ?? string.Empty, entry.Key),
            SidebarMenuEntryKind.Dashboard => CreateDashboardRow(),
            SidebarMenuEntryKind.EmptyHint => CreateEmptyHint(entry.HintText ?? string.Empty, entry.Key),
            SidebarMenuEntryKind.Instance when entry.Instance is not null => CreateInstanceRow(entry.Instance),
            _ => throw new InvalidOperationException($"Unsupported sidebar entry: {entry.Key}")
        };

        if (created is FrameworkElement frameworkElement)
        {
            frameworkElement.Tag = entry.Key;
        }

        _menuElementCache[entry.Key] = created;
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

    private void ApplyCompactDisplay()
    {
        var labelVisibility = _isCompact ? Visibility.Collapsed : Visibility.Visible;
        BrandWordmarkImage.Visibility = labelVisibility;
        AddInstanceLabel.Visibility = labelVisibility;
        ScopeFilterCombo.Visibility = _isCompact ? Visibility.Collapsed
            : WorkspaceSidebarMenuPlanner.HasMixedScopes(_lastInstances) ? Visibility.Visible : Visibility.Collapsed;
        WorkQueueLabel.Visibility = labelVisibility;
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
        var displaySubtitle = WorkspaceSidebarHelper.AppendMemoryTierHint(statusSubtitle, instance.MemoryTier);

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
        var subtitle = WorkspaceSidebarHelper.AppendMemoryTierHint(statusSubtitle, instance.MemoryTier);
        var row = CreateSelectableRow(
            instanceId,
            instance,
            instance.DisplayName,
            subtitle,
            PlatformBrandingHelper.GetAccentBrush(instance));

        row.PointerPressed += (sender, e) => InstanceRow_PointerPressed(sender, e, instanceId, instance, row);
        row.Tapped += (_, _) => InstanceRequested?.Invoke(this, instanceId);
        row.KeyDown += (sender, e) => InstanceRow_KeyDown(sender, e, instanceId, instance, row);
        // Drag reorder disabled — WinUI drag-in-ScrollViewer freeze bug. Re-enable with MenuStack_DragOver/Drop handlers when resolved.
        row.CanDrag = false;
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
            WorkQueueButton,
            WorkspaceSidebarHelper.IsSelectionMatch(
                _selectedKey,
                WorkspaceSidebarHelper.WorkQueueSelectionKey));
        ApplyFooterButtonSelection(
            NotificationsButton,
            WorkspaceSidebarHelper.IsSelectionMatch(
                _selectedKey,
                WorkspaceSidebarHelper.NotificationHubSelectionKey));
        ApplyFooterButtonSelection(
            SettingsButton,
            WorkspaceSidebarHelper.IsSelectionMatch(_selectedKey, WorkspaceSidebarHelper.SettingsSelectionKey));
        UpdateFooterButtonAccessibility(
            WorkQueueButton,
            FooterWorkQueueLabel,
            WorkspaceSidebarHelper.IsSelectionMatch(
                _selectedKey,
                WorkspaceSidebarHelper.WorkQueueSelectionKey),
            badgeCount: 0);
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

    private void WorkQueueButton_Click(object sender, RoutedEventArgs e) =>
        WorkQueueRequested?.Invoke(this, EventArgs.Empty);

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


