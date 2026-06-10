using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Controls.Occ;
using UnifiedMessenger.Controls.Shared;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Windows.ApplicationModel.DataTransfer;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private readonly Dictionary<string, OccPanelChrome> _panelChromes = new(StringComparer.OrdinalIgnoreCase);
    private OccDashboardLayoutPreferences? _layoutUndoSnapshot;
    private string? _layoutDragSourcePanelId;
    private string? _kpiDragSourceMetricId;
    private string? _keyboardMovePanelId;
    private DispatcherQueueTimer? _layoutUndoTimer;
    private bool _suppressPresetSelection;
    private string? _dragOverTargetPanelId;
    private int _dragOverStableTicks;
    private FrameworkElement? _dragPreviewHighlightTarget;
    private Brush? _dragPreviewOriginalBorderBrush;
    private Thickness _dragPreviewOriginalBorderThickness;
    private const int DropPreviewHysteresisTicks = 2;
    private readonly OccLayoutInteractionService _layoutInteraction = new();

    private void LayoutEditToggleButton_Click(object sender, RoutedEventArgs e) =>
        SetLayoutEditMode(!_viewModel.IsLayoutEditMode);

    private async void RestoreLayoutButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureLayoutUndoSnapshot();
        await _services.AppSettings.UpdateAsync(settings => OccLayoutService.ApplyDefaults(settings))
            .ConfigureAwait(true);
        ApplyLayoutPreferences();
        ShowLayoutUndoInfoBar();
    }

    private async void LayoutPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressPresetSelection || !_viewModel.IsLayoutEditMode)
        {
            return;
        }

        if (LayoutPresetComboBox.SelectedItem is not ComboBoxItem item ||
            item.Tag is not string presetId)
        {
            return;
        }

        CaptureLayoutUndoSnapshot();
        await _services.AppSettings.UpdateAsync(settings => OccLayoutService.ApplyPreset(settings, presetId))
            .ConfigureAwait(true);
        ApplyLayoutPreferences();
        AnnounceLayoutChange($"Applied {item.Content} layout preset.");
        ShowLayoutUndoInfoBar();
    }

    private void SetLayoutEditMode(bool enabled)
    {
        _viewModel.IsLayoutEditMode = enabled;
        LayoutEditToggleButton.Content = enabled ? "Done" : "Customize layout";
        RestoreLayoutButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        LayoutPresetComboBox.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        HiddenPanelsTray.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        KanbanBoard.IsReorderEnabled = enabled;
        ImmediateQueueList.CanDragItems = enabled;
        ImmediateQueueList.CanReorderItems = enabled;
        ImmediateQueueList.IsSwipeEnabled = enabled;
        ImmediateLaneDragGrip.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        ApplyLayoutEditChrome(enabled);

        if (enabled)
        {
            PopulateLayoutPresetComboBox();
        }

        if (enabled && !_services.AppSettings.Settings.OccLayoutTeachingDismissed)
        {
            OccTeachingTipHelper.ShowTeachingTip(
                LayoutEditToggleButton,
                "Customize your dashboard",
                "Drag panels on the grid, resize with +/− or Shift+arrow, hide panels, pick a preset, or press Ctrl+Z to undo. Arrow keys move the focused panel header.");
            _ = _services.AppSettings.UpdateAsync(settings => settings.OccLayoutTeachingDismissed = true);
        }
    }

    private void PopulateLayoutPresetComboBox()
    {
        _suppressPresetSelection = true;
        LayoutPresetComboBox.Items.Clear();
        LayoutPresetComboBox.Items.Add(new ComboBoxItem { Content = "Operations focus", Tag = OccLayoutPresets.OperationsFocus });
        LayoutPresetComboBox.Items.Add(new ComboBoxItem { Content = "WhatsApp focus", Tag = OccLayoutPresets.WhatsAppFocus });
        LayoutPresetComboBox.Items.Add(new ComboBoxItem { Content = "Analytics focus", Tag = OccLayoutPresets.AnalyticsFocus });
        LayoutPresetComboBox.Items.Add(new ComboBoxItem { Content = "Compact", Tag = OccLayoutPresets.Compact });

        var currentPreset = OccLayoutService.Resolve(_services.AppSettings.Settings).LayoutPresetId;
        var selectedIndex = 0;
        for (var index = 0; index < LayoutPresetComboBox.Items.Count; index++)
        {
            if (LayoutPresetComboBox.Items[index] is ComboBoxItem item &&
                item.Tag is string presetId &&
                presetId.Equals(currentPreset, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = index;
                break;
            }
        }

        LayoutPresetComboBox.SelectedIndex = selectedIndex;
        _suppressPresetSelection = false;
    }

    private void ApplyLayoutEditChrome(bool enabled)
    {
        foreach (var (panelId, section) in EnumerateLayoutSectionEntries())
        {
            EnsurePanelChrome(section, panelId);
            var surface = ResolveLayoutSectionSurface(panelId, section);
            surface.CanDrag = enabled;
            surface.AllowDrop = enabled;
            if (enabled)
            {
                surface.DragStarting -= OccSection_DragStarting;
                surface.DragStarting += OccSection_DragStarting;
                surface.DragOver -= OccSection_DragOver;
                surface.DragOver += OccSection_DragOver;
                surface.DragLeave -= OccSection_DragLeave;
                surface.DragLeave += OccSection_DragLeave;
                surface.Drop -= OccSection_Drop;
                surface.Drop += OccSection_Drop;
                surface.KeyDown -= OccSection_KeyDown;
                surface.KeyDown += OccSection_KeyDown;
                surface.IsTabStop = true;
            }
            else
            {
                surface.DragStarting -= OccSection_DragStarting;
                surface.DragOver -= OccSection_DragOver;
                surface.DragLeave -= OccSection_DragLeave;
                surface.Drop -= OccSection_Drop;
                surface.KeyDown -= OccSection_KeyDown;
            }
        }

        if (!enabled)
        {
            ClearDropPreviewHighlight();
        }

        foreach (var kpiCard in EnumerateKpiCards())
        {
            kpiCard.CanDrag = enabled;
            if (enabled)
            {
                kpiCard.DragStarting -= KpiCard_DragStarting;
                kpiCard.DragStarting += KpiCard_DragStarting;
            }
            else
            {
                kpiCard.DragStarting -= KpiCard_DragStarting;
            }
        }

        KpiStripSection.AllowDrop = enabled;
        if (enabled)
        {
            KpiStripSection.DragOver += KpiGrid_DragOver;
            KpiStripSection.Drop += KpiGrid_Drop;
        }
        else
        {
            KpiStripSection.DragOver -= KpiGrid_DragOver;
            KpiStripSection.Drop -= KpiGrid_Drop;
        }

        foreach (var chrome in _panelChromes.Values)
        {
            chrome.IsEditMode = enabled;
        }
    }

    private void EnsurePanelChrome(FrameworkElement section, string panelId)
    {
        if (_panelChromes.TryGetValue(panelId, out var existingChrome))
        {
            existingChrome.IsEditMode = _viewModel.IsLayoutEditMode;
            if (section is Expander expander && _viewModel.IsLayoutEditMode)
            {
                expander.IsExpanded = true;
            }

            return;
        }

        if (section.Parent is OccPanelChrome)
        {
            return;
        }

        var chrome = new OccPanelChrome
        {
            PanelId = panelId,
            PanelTitle = ResolvePanelTitle(panelId),
            IsEditMode = _viewModel.IsLayoutEditMode,
            Tag = panelId
        };
        chrome.HideRequested += OnPanelChromeHideRequested;
        chrome.ResizeRequested += OnPanelChromeResizeRequested;
        WrapSectionWithChrome(section, chrome);
        _panelChromes[panelId] = chrome;

        if (section is Expander wrappedExpander && _viewModel.IsLayoutEditMode)
        {
            wrappedExpander.IsExpanded = true;
        }
    }

    private static void WrapSectionWithChrome(FrameworkElement section, OccPanelChrome chrome)
    {
        if (section.Parent is not Panel parent)
        {
            return;
        }

        var index = parent.Children.IndexOf(section);
        if (index < 0)
        {
            return;
        }

        var row = Grid.GetRow(section);
        var column = Grid.GetColumn(section);
        var rowSpan = Grid.GetRowSpan(section);
        var columnSpan = Grid.GetColumnSpan(section);

        chrome.PanelContent = section;
        parent.Children.RemoveAt(index);
        parent.Children.Insert(index, chrome);
        Grid.SetRow(chrome, row);
        Grid.SetColumn(chrome, column);
        Grid.SetRowSpan(chrome, rowSpan);
        Grid.SetColumnSpan(chrome, columnSpan);
    }

    private void OnPanelChromeHideRequested(object? sender, string panelId) =>
        _ = HidePanelAsync(panelId);

    private void OnPanelChromeResizeRequested(object? sender, (string PanelId, int DeltaColumns) e) =>
        _ = ResizePanelAsync(e.PanelId, e.DeltaColumns);

    private void ApplyLayoutPreferences()
    {
        var layout = OccLayoutService.Resolve(_services.AppSettings.Settings);
        ApplyGridLayout(layout);
        ApplyKpiLayout(layout.KpiMetricOrder);
        ApplyCompactPresetChrome(layout.LayoutPresetId);
        UpdateHiddenPanelsTray(layout.PanelPlacements);
    }

    private void ApplyGridLayout(OccDashboardLayoutPreferences layout)
    {
        var width = ResolveOccLayoutWidth();
        var placements = OccLayoutGridEngine.ReflowForWidth(layout.PanelPlacements, width);
        OccLayoutGridApplier.Apply(OccLayoutGrid, placements, BuildPanelMap());
    }

    private double ResolveOccLayoutWidth()
    {
        if (OccLayoutGrid.ActualWidth > 0)
        {
            return OccLayoutGrid.ActualWidth;
        }

        return ActualWidth > 0 ? ActualWidth : OccLayoutGridConstants.BreakpointWide;
    }

    private void OccLayoutGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) < 1)
        {
            return;
        }

        var layout = OccLayoutService.Resolve(_services.AppSettings.Settings);
        ApplyGridLayout(layout);
    }

    private void ApplyCompactPresetChrome(string presetId)
    {
        if (!presetId.Equals(OccLayoutPresets.Compact, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        PlatformIntelligenceExpander.IsExpanded = false;
        AnalyticsTrendsExpander.IsExpanded = false;
    }

    private void ApplyKpiLayout(IReadOnlyList<string> metricOrder)
    {
        var cardMap = BuildKpiCardMap();
        for (var index = 0; index < metricOrder.Count; index++)
        {
            if (!cardMap.TryGetValue(metricOrder[index], out var card))
            {
                continue;
            }

            Grid.SetRow(card, 1);
            Grid.SetColumn(card, index);
            Grid.SetColumnSpan(card, 1);
        }
    }

    private Dictionary<string, FrameworkElement> BuildPanelMap()
    {
        var panelMap = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase)
        {
            [OccLayoutDefaults.KpiStripPanelId] = KpiStripSection,
            [OccLayoutDefaults.ImmediateLanePanelId] = ImmediateLaneSection,
            [OccLayoutDefaults.KanbanPanelId] = KanbanSection,
            [OccLayoutDefaults.BranchMetricsPanelId] = BranchMetricsSection,
            [OccLayoutDefaults.BranchPulsePanelId] = BranchPulseSection,
            [OccLayoutDefaults.HighlightsPanelId] = HighlightsSection,
            [OccLayoutDefaults.AiFeedPanelId] = AiFeedSection,
            [OccLayoutDefaults.PlatformIntelligencePanelId] = PlatformIntelligenceExpander,
            [OccLayoutDefaults.AnalyticsPanelId] = AnalyticsTrendsExpander,
            [OccLayoutDefaults.DataHealthPanelId] = HealthChipsSection
        };

        foreach (var (panelId, chrome) in _panelChromes)
        {
            panelMap[panelId] = chrome;
        }

        return panelMap;
    }

    private Dictionary<string, FrameworkElement> BuildKpiCardMap() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["open"] = OpenKpiCard,
        ["hanging"] = HangingKpiCard,
        ["revenue"] = RevenueKpiCard,
        ["immediate"] = ImmediateActionKpiCard,
        ["sla"] = SlaBreachesKpiCard
    };

    private IEnumerable<FrameworkElement> EnumerateLayoutSections()
    {
        foreach (var (panelId, section) in EnumerateLayoutSectionEntries())
        {
            yield return ResolveLayoutSectionSurface(panelId, section);
        }
    }

    private IEnumerable<(string PanelId, FrameworkElement Section)> EnumerateLayoutSectionEntries()
    {
        yield return (OccLayoutDefaults.KpiStripPanelId, KpiStripSection);
        yield return (OccLayoutDefaults.ImmediateLanePanelId, ImmediateLaneSection);
        yield return (OccLayoutDefaults.KanbanPanelId, KanbanSection);
        yield return (OccLayoutDefaults.BranchMetricsPanelId, BranchMetricsSection);
        yield return (OccLayoutDefaults.BranchPulsePanelId, BranchPulseSection);
        yield return (OccLayoutDefaults.HighlightsPanelId, HighlightsSection);
        yield return (OccLayoutDefaults.AiFeedPanelId, AiFeedSection);
        yield return (OccLayoutDefaults.PlatformIntelligencePanelId, PlatformIntelligenceExpander);
        yield return (OccLayoutDefaults.AnalyticsPanelId, AnalyticsTrendsExpander);
        yield return (OccLayoutDefaults.DataHealthPanelId, HealthChipsSection);
    }

    private FrameworkElement ResolveLayoutSectionSurface(string panelId, FrameworkElement section) =>
        _panelChromes.TryGetValue(panelId, out var chrome) ? chrome : section;

    private static string? ResolvePanelId(FrameworkElement element) =>
        element.Tag as string ?? (element is OccPanelChrome chrome ? chrome.PanelId : null);

    private IEnumerable<FrameworkElement> EnumerateKpiCards()
    {
        yield return OpenKpiCard;
        yield return HangingKpiCard;
        yield return RevenueKpiCard;
        yield return ImmediateActionKpiCard;
        yield return SlaBreachesKpiCard;
    }

    private void UpdateHiddenPanelsTray(IReadOnlyList<OccPanelPlacement> placements)
    {
        HiddenPanelsTray.Children.Clear();
        if (!_viewModel.IsLayoutEditMode)
        {
            return;
        }

        foreach (var placement in placements.Where(candidate => !candidate.IsVisible))
        {
            var restoreButton = new Button
            {
                Content = $"Show {ResolvePanelTitle(placement.PanelId)}",
                Tag = placement.PanelId,
                Style = Application.Current.Resources["SubtleButtonStyle"] as Style
            };
            restoreButton.Click += HiddenPanelRestoreButton_Click;
            HiddenPanelsTray.Children.Add(restoreButton);
        }
    }

    private static string ResolvePanelTitle(string panelId) => panelId switch
    {
        OccLayoutDefaults.KpiStripPanelId => "KPI strip",
        OccLayoutDefaults.ImmediateLanePanelId => "Immediate lane",
        OccLayoutDefaults.KanbanPanelId => "Kanban",
        OccLayoutDefaults.BranchMetricsPanelId => "Branch metrics",
        OccLayoutDefaults.BranchPulsePanelId => "Branch pulse",
        OccLayoutDefaults.HighlightsPanelId => "Highlights",
        OccLayoutDefaults.AiFeedPanelId => "AI feed",
        OccLayoutDefaults.PlatformIntelligencePanelId => "Platform intelligence",
        OccLayoutDefaults.AnalyticsPanelId => "Analytics",
        OccLayoutDefaults.DataHealthPanelId => "Data health",
        _ => panelId
    };

    private async void HiddenPanelRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string panelId)
        {
            return;
        }

        CaptureLayoutUndoSnapshot();
        await _services.AppSettings.UpdateAsync(settings =>
        {
            var placements = OccLayoutService.Resolve(settings).PanelPlacements.ToList();
            var index = placements.FindIndex(placement =>
                placement.PanelId.Equals(panelId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return;
            }

            placements[index].IsVisible = true;
            OccLayoutService.PersistPlacements(settings, placements);
        }).ConfigureAwait(true);

        ApplyLayoutPreferences();
        AnnounceLayoutChange($"Restored {ResolvePanelTitle(panelId)} panel.");
        ShowLayoutUndoInfoBar();
    }

    private void OccSection_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        if (!_viewModel.IsLayoutEditMode ||
            sender is not FrameworkElement element ||
            ResolvePanelId(element) is not { } panelId)
        {
            e.Cancel = true;
            return;
        }

        _layoutDragSourcePanelId = panelId;
        e.Data.SetText(panelId);
        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    private void OccSection_DragOver(object sender, DragEventArgs e)
    {
        if (!_viewModel.IsLayoutEditMode)
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Move;
        e.DragUIOverride.IsGlyphVisible = false;

        if (sender is FrameworkElement target && ResolvePanelId(target) is { } panelId)
        {
            if (!panelId.Equals(_dragOverTargetPanelId, StringComparison.OrdinalIgnoreCase))
            {
                _dragOverTargetPanelId = panelId;
                _dragOverStableTicks = 0;
                ClearDropPreviewHighlight();
            }
            else if (++_dragOverStableTicks >= DropPreviewHysteresisTicks)
            {
                ApplyDropPreviewHighlight(target);
            }
        }
    }

    private void OccSection_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement target &&
            ReferenceEquals(target, _dragPreviewHighlightTarget))
        {
            ClearDropPreviewHighlight();
            _dragOverTargetPanelId = null;
            _dragOverStableTicks = 0;
        }
    }

    private void ApplyDropPreviewHighlight(FrameworkElement target)
    {
        if (ReferenceEquals(target, _dragPreviewHighlightTarget))
        {
            return;
        }

        ClearDropPreviewHighlight();
        if (ResolveDropPreviewBorder(target) is not Border border)
        {
            return;
        }

        _dragPreviewHighlightTarget = target;
        _dragPreviewOriginalBorderBrush = border.BorderBrush;
        _dragPreviewOriginalBorderThickness = border.BorderThickness;
        border.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        border.BorderThickness = new Thickness(2);
    }

    private void ClearDropPreviewHighlight()
    {
        if (_dragPreviewHighlightTarget is not null &&
            ResolveDropPreviewBorder(_dragPreviewHighlightTarget) is Border border)
        {
            border.BorderBrush = _dragPreviewOriginalBorderBrush;
            border.BorderThickness = _dragPreviewOriginalBorderThickness;
        }

        _dragPreviewHighlightTarget = null;
        _dragPreviewOriginalBorderBrush = null;
    }

    private static Border? ResolveDropPreviewBorder(FrameworkElement target) =>
        target switch
        {
            Border border => border,
            SurfaceCard surfaceCard => surfaceCard.FindName("CardBorder") as Border,
            OccPanelChrome chrome when chrome.PanelContent is FrameworkElement content =>
                ResolveDropPreviewBorder(content),
            _ => null
        };

    private async void OccSection_Drop(object sender, DragEventArgs e)
    {
        ClearDropPreviewHighlight();
        _dragOverTargetPanelId = null;
        _dragOverStableTicks = 0;

        if (!_viewModel.IsLayoutEditMode ||
            sender is not FrameworkElement target ||
            ResolvePanelId(target) is not { } targetPanelId)
        {
            return;
        }

        var sourcePanelId = _layoutDragSourcePanelId;
        if (string.IsNullOrWhiteSpace(sourcePanelId) ||
            sourcePanelId.Equals(targetPanelId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!e.DataView.Contains(StandardDataFormats.Text))
        {
            return;
        }

        var draggedId = await e.DataView.GetTextAsync();
        if (string.IsNullOrWhiteSpace(draggedId))
        {
            draggedId = sourcePanelId;
        }

        await PersistGridMoveAsync(draggedId, targetPanelId).ConfigureAwait(true);
        _layoutDragSourcePanelId = null;
    }

    private async Task PersistGridMoveAsync(string sourcePanelId, string targetPanelId)
    {
        CaptureLayoutUndoSnapshot();
        await _services.AppSettings.UpdateAsync(settings =>
        {
            var layout = OccLayoutService.Resolve(settings);
            var target = layout.PanelPlacements.First(placement =>
                placement.PanelId.Equals(targetPanelId, StringComparison.OrdinalIgnoreCase));

            if (!OccLayoutGridEngine.TryMove(
                    layout.PanelPlacements,
                    sourcePanelId,
                    target.Column,
                    target.Row,
                    out var updated))
            {
                return;
            }

            OccLayoutService.PersistPlacements(settings, updated);
        }).ConfigureAwait(true);

        ApplyLayoutPreferences();
        AnnounceLayoutChange($"Moved {ResolvePanelTitle(sourcePanelId)}.");
        ShowLayoutUndoInfoBar();
    }

    private void OccSection_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_viewModel.IsLayoutEditMode ||
            sender is not FrameworkElement element ||
            ResolvePanelId(element) is not { } panelId)
        {
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            _keyboardMovePanelId = null;
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Enter && _keyboardMovePanelId is not null)
        {
            _keyboardMovePanelId = null;
            e.Handled = true;
            return;
        }

        var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(
            Windows.System.VirtualKey.Shift);
        var isResize = (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) ==
                       Windows.UI.Core.CoreVirtualKeyStates.Down;
        var deltaColumns = e.Key switch
        {
            Windows.System.VirtualKey.Left => -1,
            Windows.System.VirtualKey.Right => 1,
            _ => 0
        };

        if (deltaColumns == 0)
        {
            if (e.Key == Windows.System.VirtualKey.Delete)
            {
                _ = HidePanelAsync(panelId);
                e.Handled = true;
            }

            return;
        }

        e.Handled = true;
        if (isResize)
        {
            _ = ResizePanelAsync(panelId, deltaColumns);
        }
        else
        {
            _ = NudgePanelAsync(panelId, deltaColumns);
        }
    }

    private async Task NudgePanelAsync(string panelId, int deltaColumns)
    {
        CaptureLayoutUndoSnapshot();
        await _services.AppSettings.UpdateAsync(settings =>
        {
            var layout = OccLayoutService.Resolve(settings);
            if (!_layoutInteraction.TryNudgePanel(layout.PanelPlacements, panelId, deltaColumns, out var updated))
            {
                return;
            }

            OccLayoutService.PersistPlacements(settings, updated);
        }).ConfigureAwait(true);

        ApplyLayoutPreferences();
        AnnounceLayoutChange($"Moved {ResolvePanelTitle(panelId)}.");
        ShowLayoutUndoInfoBar();
    }

    private async Task ResizePanelAsync(string panelId, int deltaColumns)
    {
        CaptureLayoutUndoSnapshot();
        await _services.AppSettings.UpdateAsync(settings =>
        {
            var layout = OccLayoutService.Resolve(settings);
            if (!_layoutInteraction.TryResizePanel(layout.PanelPlacements, panelId, deltaColumns, out var updated))
            {
                return;
            }

            OccLayoutService.PersistPlacements(settings, updated);
        }).ConfigureAwait(true);

        ApplyLayoutPreferences();
        AnnounceLayoutChange($"Resized {ResolvePanelTitle(panelId)}.");
        ShowLayoutUndoInfoBar();
    }

    private async Task HidePanelAsync(string panelId)
    {
        CaptureLayoutUndoSnapshot();
        await _services.AppSettings.UpdateAsync(settings =>
        {
            var layout = OccLayoutService.Resolve(settings);
            var updated = OccLayoutGridEngine.SetVisibility(layout.PanelPlacements, panelId, false);
            OccLayoutService.PersistPlacements(settings, updated);
        }).ConfigureAwait(true);

        ApplyLayoutPreferences();
        AnnounceLayoutChange($"Hidden {ResolvePanelTitle(panelId)}.");
        ShowLayoutUndoInfoBar();
    }

    private void KpiCard_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        if (!_viewModel.IsLayoutEditMode)
        {
            e.Cancel = true;
            return;
        }

        var metricId = BuildKpiCardMap().FirstOrDefault(pair => ReferenceEquals(pair.Value, sender)).Key;
        if (string.IsNullOrWhiteSpace(metricId))
        {
            e.Cancel = true;
            return;
        }

        _kpiDragSourceMetricId = metricId;
        e.Data.SetText(metricId);
        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    private void KpiGrid_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = _viewModel.IsLayoutEditMode
            ? DataPackageOperation.Move
            : DataPackageOperation.None;
    }

    private async void KpiGrid_Drop(object sender, DragEventArgs e)
    {
        if (!_viewModel.IsLayoutEditMode || string.IsNullOrWhiteSpace(_kpiDragSourceMetricId))
        {
            return;
        }

        var position = e.GetPosition(KpiStripSection);
        var targetIndex = (int)Math.Clamp(
            Math.Round(position.X / Math.Max(KpiStripSection.ActualWidth / 5.0, 1)),
            0,
            4);
        await _services.AppSettings.UpdateAsync(settings =>
        {
            var order = OccLayoutService.Resolve(settings).KpiMetricOrder.ToList();
            order.RemoveAll(metric => metric.Equals(_kpiDragSourceMetricId, StringComparison.OrdinalIgnoreCase));
            targetIndex = Math.Clamp(targetIndex, 0, order.Count);
            order.Insert(targetIndex, _kpiDragSourceMetricId!);
            settings.OccKpiMetricOrder = order;
        }).ConfigureAwait(true);

        _kpiDragSourceMetricId = null;
        CaptureLayoutUndoSnapshot();
        ApplyLayoutPreferences();
        ShowLayoutUndoInfoBar();
    }

    private void CaptureLayoutUndoSnapshot() =>
        _layoutUndoSnapshot = OccLayoutService.Resolve(_services.AppSettings.Settings);

    private void ShowLayoutUndoInfoBar()
    {
        LayoutUndoInfoBar.IsOpen = true;
        _layoutUndoTimer?.Stop();
        _layoutUndoTimer = _dispatcherQueue.CreateTimer();
        _layoutUndoTimer.Interval = TimeSpan.FromSeconds(8);
        _layoutUndoTimer.Tick += (_, _) =>
        {
            _layoutUndoTimer?.Stop();
            LayoutUndoInfoBar.IsOpen = false;
            _layoutUndoSnapshot = null;
        };
        _layoutUndoTimer.Start();

        LayoutUndoInfoBar.ActionButton.Click -= LayoutUndoInfoBar_ActionClick;
        LayoutUndoInfoBar.ActionButton.Click += LayoutUndoInfoBar_ActionClick;
    }

    private async void LayoutUndoInfoBar_ActionClick(object sender, RoutedEventArgs e) =>
        await TryUndoLayoutChangeAsync().ConfigureAwait(true);

    internal async Task<bool> TryUndoLayoutChangeAsync()
    {
        if (_layoutUndoSnapshot is null)
        {
            LayoutUndoInfoBar.IsOpen = false;
            return false;
        }

        var snapshot = _layoutUndoSnapshot;
        await _services.AppSettings.UpdateAsync(settings =>
            OccLayoutCommandHelper.ApplyLayoutSnapshot(settings, snapshot)).ConfigureAwait(true);

        _layoutUndoSnapshot = null;
        LayoutUndoInfoBar.IsOpen = false;
        _layoutUndoTimer?.Stop();
        ApplyLayoutPreferences();
        AnnounceLayoutChange("Layout change undone.");
        return true;
    }

    internal static bool IsLayoutUndoShortcut(Windows.System.VirtualKey key, bool isControlDown) =>
        isControlDown && key == Windows.System.VirtualKey.Z;

    private void AnnounceLayoutChange(string message) =>
        LayoutLiveRegion.Text = message;
}
