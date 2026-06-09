using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using UnifiedMessenger.ViewModels;
using Windows.Foundation;

namespace UnifiedMessenger.Controls;

public sealed partial class WorkspaceSidebar : Grid
{
    private readonly WorkspaceSidebarViewModel _viewModel = new();
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

    public WorkspaceSidebarViewModel ViewModel => _viewModel;

    public WorkspaceSidebar()
    {
        InitializeComponent();
        MenuStack.AllowDrop = true;
        MenuStack.DragOver += MenuStack_DragOver;
        MenuStack.Drop += MenuStack_Drop;
        Unloaded += OnUnloaded;
    }

    public event EventHandler<(string SourceInstanceId, string TargetInstanceId)>? InstanceReorderRequested;

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
        bool dashboardSelected)
    {
        ArgumentNullException.ThrowIfNull(instances);

        _selectedKey = WorkspaceSidebarHelper.ResolveSelectionKey(
            dashboardSelected,
            selectedInstanceId,
            settingsSelected: false);
        _viewModel.ApplySelection(dashboardSelected, selectedInstanceId, settingsSelected: false);

        var plan = WorkspaceSidebarMenuPlanner.BuildPlan(instances);
        if (_currentPlan is not null &&
            WorkspaceSidebarMenuPlanner.HasSameStructure(_currentPlan, plan))
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
    }

    public void SetCompactDisplay(bool compact)
    {
        _isCompact = compact;
        ApplyCompactDisplay();
    }

    private void ApplyCompactDisplay()
    {
        var labelVisibility = _isCompact ? Visibility.Collapsed : Visibility.Visible;
        BrandTitleText.Visibility = labelVisibility;
        AddInstanceLabel.Visibility = labelVisibility;
        NotificationsLabel.Visibility = labelVisibility;
        SettingsLabel.Visibility = labelVisibility;

        MenuRoot.Padding = _isCompact ? new Thickness(6, 12, 6, 8) : new Thickness(8, 12, 8, 8);
        FooterPanel.Padding = _isCompact ? new Thickness(8, 10, 8, 12) : new Thickness(12, 10, 12, 12);

        foreach (var element in _compactHiddenElements)
        {
            element.Visibility = labelVisibility;
        }

        foreach (var row in _instanceRows.Values)
        {
            row.Padding = _isCompact ? new Thickness(6, 8, 4, 8) : new Thickness(10, 10, 8, 10);
        }

        if (_dashboardRow is not null)
        {
            _dashboardRow.Padding = _isCompact ? new Thickness(6, 8, 4, 8) : new Thickness(10, 10, 8, 10);
        }
    }

    public void SetSelection(bool dashboardSelected, string? instanceId, bool settingsSelected = false)
    {
        _selectedKey = WorkspaceSidebarHelper.ResolveSelectionKey(
            dashboardSelected,
            instanceId,
            settingsSelected);
        _viewModel.ApplySelection(dashboardSelected, instanceId, settingsSelected);
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
    }

    public void UpdateInstanceHealth(string instanceId, MessengerInstance instance)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var normalizedId = instanceId.Trim();
        var connectionStatus = InstanceConnectionStatusService.Instance.GetStatus(normalizedId);
        var adapterStatus = AdapterHealthMonitor.Instance.GetStatus(normalizedId);
        var detail = InstanceConnectionStatusService.Instance.GetDetail(normalizedId);

        if (_instanceStatusDots.TryGetValue(normalizedId, out var dot))
        {
            dot.Fill = new SolidColorBrush(
                WorkspaceSidebarHelper.ResolveConnectionIndicatorColor(connectionStatus, adapterStatus.State));
        }

        if (_instanceStatusLabels.TryGetValue(normalizedId, out var statusLabel))
        {
            statusLabel.Text = WorkspaceSidebarHelper.ResolveStatusSubtitle(
                connectionStatus,
                adapterStatus.State,
                instance.NotificationsMuted,
                detail);
        }

        if (_instanceRows.TryGetValue(normalizedId, out var row))
        {
            var detailLine = string.IsNullOrWhiteSpace(detail) ? string.Empty : $"\n{detail}";
            ToolTipService.SetToolTip(
                row,
                $"{instance.DisplayName}\nWorkspace: {instance.Category}\n{statusLabel?.Text ?? connectionStatus.ToString()}{detailLine}\nAdapter: {adapterStatus.Description}");
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        MenuStack.DragOver -= MenuStack_DragOver;
        MenuStack.Drop -= MenuStack_Drop;
        Unloaded -= OnUnloaded;
    }

    private UIElement CreateSectionHeader(string title, string key)
    {
        var header = new TextBlock
        {
            Tag = key,
            Text = title.ToUpperInvariant(),
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Opacity = 0.55,
            Margin = new Thickness(4, 14, 4, 6),
            CharacterSpacing = 40
        };
        _compactHiddenElements.Add(header);
        return header;
    }

    private TextBlock CreateEmptyHint(string text, string key)
    {
        var hint = new TextBlock
        {
            Tag = key,
            Text = text,
            FontSize = 12,
            Opacity = 0.5,
            Margin = new Thickness(8, 0, 8, 4),
            TextWrapping = TextWrapping.WrapWholeWords
        };
        _compactHiddenElements.Add(hint);
        return hint;
    }

    private Border CreateDashboardRow()
    {
        var row = CreateSelectableRow(WorkspaceSidebarHelper.DashboardSelectionKey, null, "Dashboard", "Overview", null);
        AutomationProperties.SetName(row, "Sidebar Dashboard");
        row.PointerPressed += DashboardRow_PointerPressed;
        _dashboardRow = row;
        return row;
    }

    private void DashboardRow_PointerPressed(object sender, PointerRoutedEventArgs e) =>
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
        var connectionStatus = InstanceConnectionStatusService.Instance.GetStatus(instanceId);
        var adapterState = AdapterHealthMonitor.Instance.GetStatus(instanceId).State;
        var connectionDetail = InstanceConnectionStatusService.Instance.GetDetail(instanceId);
        var subtitle = WorkspaceSidebarHelper.ResolveStatusSubtitle(
            connectionStatus,
            adapterState,
            instance.NotificationsMuted,
            connectionDetail);
        var row = CreateSelectableRow(
            instanceId,
            instance,
            instance.DisplayName,
            subtitle,
            PlatformBrandingHelper.GetAccentBrush(instance));

        row.PointerPressed += (sender, e) => InstanceRow_PointerPressed(sender, e, instanceId, instance, row);
        row.CanDrag = true;
        row.DragStarting += (_, e) => InstanceRow_DragStarting(e, instanceId, instance.DisplayName);

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
        if (e.GetCurrentPoint(row).Properties.IsRightButtonPressed)
        {
            InstanceContextRequested?.Invoke(this, (instanceId, instance, row));
            return;
        }

        InstanceRequested?.Invoke(this, instanceId);
    }

    private static void InstanceRow_DragStarting(DragStartingEventArgs e, string instanceId, string displayName)
    {
        e.Data.SetText(instanceId);
        e.Data.Properties.Title = displayName;
        e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
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
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush = accentBrush
        };

        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconHost = instance is not null
            ? ProfileAvatarService.CreateAvatar(instance, 28)
            : (FrameworkElement)CreateFallbackIcon("\uE9D9", accentBrush, 28);

        var textPanel = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var titleLabel = new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        textPanel.Children.Add(titleLabel);
        if (!WorkspaceSidebarHelper.IsSelectionMatch(key, WorkspaceSidebarHelper.DashboardSelectionKey))
        {
            _instanceTitleLabels[key] = titleLabel;
        }
        var statusLabel = new TextBlock
        {
            Text = subtitle,
            FontSize = 11,
            Opacity = 0.62,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
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
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128))
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

        return row;
    }

    private void ApplySelectionVisuals()
    {
        if (_dashboardRow is not null)
        {
            ApplyRowSelection(
                _dashboardRow,
                WorkspaceSidebarHelper.IsSelectionMatch(_selectedKey, WorkspaceSidebarHelper.DashboardSelectionKey));
        }

        foreach (var (instanceId, row) in _instanceRows)
        {
            ApplyRowSelection(row, WorkspaceSidebarHelper.IsSelectionMatch(_selectedKey, instanceId));
        }

        ApplyFooterButtonSelection(
            SettingsButton,
            WorkspaceSidebarHelper.IsSelectionMatch(_selectedKey, WorkspaceSidebarHelper.SettingsSelectionKey));
    }

    private static void ApplyFooterButtonSelection(Button button, bool selected)
    {
        button.Background = selected
            ? Application.Current.Resources["LayerFillColorDefaultBrush"] as Brush
              ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        button.BorderThickness = selected
            ? new Thickness(3, 0, 0, 0)
            : new Thickness(0);
    }

    private static void ApplyRowSelection(Border row, bool selected)
    {
        row.Background = selected
            ? Application.Current.Resources["LayerFillColorDefaultBrush"] as Brush
              ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        row.BorderThickness = selected
            ? new Thickness(3, 0, 0, 0)
            : new Thickness(0);
    }

    private void AddInstanceButton_Click(object sender, RoutedEventArgs e) =>
        AddInstanceRequested?.Invoke(this, EventArgs.Empty);

    private void NotificationsButton_Click(object sender, RoutedEventArgs e) =>
        NotificationsRequested?.Invoke(this, EventArgs.Empty);

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void MenuStack_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        e.DragUIOverride.Caption = "Reorder account";
    }

    private async void MenuStack_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
        {
            return;
        }

        var sourceId = (await e.DataView.GetTextAsync()).Trim();
        var position = e.GetPosition(MenuStack);
        var targetId = ResolveDropTargetInstanceId(position);
        if (!WorkspaceSidebarHelper.ShouldAcceptReorder(sourceId, targetId))
        {
            return;
        }

        InstanceReorderRequested?.Invoke(this, (sourceId, targetId!));
    }

    private string? ResolveDropTargetInstanceId(Point position)
    {
        var bounds = new List<SidebarRowBounds>(_instanceRows.Count);
        foreach (var (instanceId, row) in _instanceRows)
        {
            var transform = row.TransformToVisual(MenuStack);
            var rowBounds = transform.TransformBounds(new Rect(0, 0, row.ActualWidth, row.ActualHeight));
            bounds.Add(new SidebarRowBounds(instanceId, rowBounds.Top, rowBounds.Bottom));
        }

        return WorkspaceSidebarHelper.ResolveDropTargetInstanceId(position.Y, bounds);
    }
}
