using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class WorkspaceSidebar : Grid
{
    private readonly Dictionary<string, Border> _instanceRows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InfoBadge> _instanceBadges = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Ellipse> _instanceStatusDots = new(StringComparer.OrdinalIgnoreCase);

    private Border? _dashboardRow;
    private string? _selectedKey = "dashboard";
    private bool _isCompact;
    private readonly List<FrameworkElement> _compactHiddenElements = [];

    public WorkspaceSidebar()
    {
        InitializeComponent();
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
        bool dashboardSelected)
    {
        _selectedKey = dashboardSelected ? "dashboard" : selectedInstanceId;
        _instanceRows.Clear();
        _instanceBadges.Clear();
        _instanceStatusDots.Clear();
        _compactHiddenElements.Clear();
        MenuStack.Children.Clear();

        AddSectionHeader("Overview");
        _dashboardRow = CreateDashboardRow();
        MenuStack.Children.Add(_dashboardRow);

        var professional = instances
            .Where(i => i.IsProfessional)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var personal = instances
            .Where(i => !i.IsProfessional)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        AddSectionHeader("Pro / Business");
        if (professional.Count == 0)
        {
            MenuStack.Children.Add(CreateEmptyHint("No business accounts yet."));
        }
        else
        {
            foreach (var instance in professional)
            {
                MenuStack.Children.Add(CreateInstanceRow(instance));
            }
        }

        AddSectionHeader("Personal / Life");
        if (personal.Count == 0)
        {
            MenuStack.Children.Add(CreateEmptyHint("No personal accounts yet."));
        }
        else
        {
            foreach (var instance in personal)
            {
                MenuStack.Children.Add(CreateInstanceRow(instance));
            }
        }

        ApplySelectionVisuals();
        ApplyCompactDisplay();
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

    public void SetSelection(bool dashboardSelected, string? instanceId)
    {
        _selectedKey = dashboardSelected ? "dashboard" : instanceId;
        ApplySelectionVisuals();
    }

    public void UpdateInstanceBadge(string instanceId, int count, MessengerInstance? instance = null)
    {
        if (!_instanceBadges.TryGetValue(instanceId, out var badge))
        {
            return;
        }

        if (count > 0)
        {
            badge.Value = count > 99 ? 99 : count;
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
        if (total > 0)
        {
            NotificationHubBadge.Value = total > 99 ? 99 : total;
            NotificationHubBadge.Visibility = Visibility.Visible;
        }
        else
        {
            NotificationHubBadge.Visibility = Visibility.Collapsed;
        }
    }

    public void UpdateInstanceHealth(string instanceId, MessengerInstance instance)
    {
        if (!_instanceStatusDots.TryGetValue(instanceId, out var dot))
        {
            return;
        }

        var status = AdapterHealthMonitor.Instance.GetStatus(instanceId);
        dot.Fill = new SolidColorBrush(status.State switch
        {
            AdapterHealthState.Healthy => Windows.UI.Color.FromArgb(255, 16, 124, 16),
            AdapterHealthState.Ready => Windows.UI.Color.FromArgb(255, 0, 99, 177),
            AdapterHealthState.Stale => Windows.UI.Color.FromArgb(255, 196, 89, 17),
            AdapterHealthState.NoAdapter => Windows.UI.Color.FromArgb(255, 128, 128, 128),
            _ => Windows.UI.Color.FromArgb(255, 160, 160, 160)
        });

        if (_instanceRows.TryGetValue(instanceId, out var row))
        {
            ToolTipService.SetToolTip(
                row,
                $"{instance.DisplayName}\nWorkspace: {instance.Category}\nAdapter: {status.Description}");
        }
    }

    private void AddSectionHeader(string title)
    {
        var header = new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Opacity = 0.55,
            Margin = new Thickness(4, 14, 4, 6),
            CharacterSpacing = 40
        };
        MenuStack.Children.Add(header);
        _compactHiddenElements.Add(header);
    }

    private TextBlock CreateEmptyHint(string text)
    {
        var hint = new TextBlock
        {
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
        var row = CreateSelectableRow("dashboard", null, "Dashboard", "Overview", null);
        row.PointerPressed += (_, _) => DashboardRequested?.Invoke(this, EventArgs.Empty);
        return row;
    }

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
        var subtitle = instance.NotificationsMuted
            ? "Notifications muted"
            : GetStatusSubtitle(instance.Id);
        var row = CreateSelectableRow(
            instance.Id,
            instance,
            instance.DisplayName,
            subtitle,
            PlatformBrandingHelper.GetAccentBrush(instance));

        row.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(row).Properties.IsRightButtonPressed)
            {
                InstanceContextRequested?.Invoke(this, (instance.Id, instance, row));
                return;
            }

            InstanceRequested?.Invoke(this, instance.Id);
        };

        _instanceRows[instance.Id] = row;
        UpdateInstanceHealth(instance.Id, instance);
        return row;
    }

    private Border CreateSelectableRow(
        string key,
        MessengerInstance? instance,
        string title,
        string subtitle,
        SolidColorBrush? accentBrush)
    {
        accentBrush ??= new SolidColorBrush(Windows.UI.Color.FromArgb(255, 107, 114, 128));

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
        textPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        textPanel.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 11,
            Opacity = 0.62,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
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

        if (key != "dashboard")
        {
            _instanceStatusDots[key] = statusDot;
            _instanceBadges[key] = badge;
        }

        return row;
    }

    private static string GetStatusSubtitle(string instanceId)
    {
        var status = AdapterHealthMonitor.Instance.GetStatus(instanceId);
        return status.State switch
        {
            AdapterHealthState.Healthy => "Status: Online",
            AdapterHealthState.Ready => "Status: Ready",
            AdapterHealthState.Stale => "Status: Stale",
            AdapterHealthState.NoAdapter => "Status: Starting",
            _ => "Status: Unknown"
        };
    }

    private void ApplySelectionVisuals()
    {
        if (_dashboardRow is not null)
        {
            ApplyRowSelection(_dashboardRow, _selectedKey == "dashboard");
        }

        foreach (var (instanceId, row) in _instanceRows)
        {
            ApplyRowSelection(row, _selectedKey == instanceId);
        }
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
}
