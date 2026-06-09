using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UnifiedMessenger.Controls.Occ;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Windows.ApplicationModel.DataTransfer;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private OccDashboardLayoutPreferences? _layoutUndoSnapshot;
    private string? _layoutDragSourcePanelId;
    private string? _kpiDragSourceMetricId;
    private DispatcherQueueTimer? _layoutUndoTimer;

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

    private void SetLayoutEditMode(bool enabled)
    {
        _viewModel.IsLayoutEditMode = enabled;
        LayoutEditToggleButton.Content = enabled ? "Done" : "Customize layout";
        RestoreLayoutButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        KanbanBoard.IsReorderEnabled = enabled;
        ImmediateQueueList.CanDragItems = enabled;
        ImmediateQueueList.CanReorderItems = enabled;
        ImmediateQueueList.IsSwipeEnabled = enabled;
        ImmediateLaneDragGrip.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        ApplyLayoutEditChrome(enabled);

        if (enabled && !_services.AppSettings.Settings.OccLayoutTeachingDismissed)
        {
            OccTeachingTipHelper.ShowTeachingTip(
                LayoutEditToggleButton,
                "Customize your dashboard",
                "Drag sections and KPI cards to reorder. Kanban card order is visual only and does not change thread status.");
            _ = _services.AppSettings.UpdateAsync(settings => settings.OccLayoutTeachingDismissed = true);
        }
    }

    private void ApplyLayoutEditChrome(bool enabled)
    {
        foreach (var section in EnumerateLayoutSections())
        {
            section.CanDrag = enabled;
            if (enabled)
            {
                section.DragStarting -= OccSection_DragStarting;
                section.DragStarting += OccSection_DragStarting;
            }
            else
            {
                section.DragStarting -= OccSection_DragStarting;
            }
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

        OperationsKpiGrid.AllowDrop = enabled;
        if (enabled)
        {
            OperationsKpiGrid.DragOver += KpiGrid_DragOver;
            OperationsKpiGrid.Drop += KpiGrid_Drop;
        }
        else
        {
            OperationsKpiGrid.DragOver -= KpiGrid_DragOver;
            OperationsKpiGrid.Drop -= KpiGrid_Drop;
        }
    }

    private void ApplyLayoutPreferences()
    {
        var layout = OccLayoutService.Resolve(_services.AppSettings.Settings);
        ReorderPanelChildren(ActionPanel, layout.ActionPanelOrder, BuildActionPanelMap());
        ReorderPanelChildren(ContextPanel, layout.ContextPanelOrder, BuildContextPanelMap());
        ApplyKpiLayout(layout.KpiMetricOrder);
        ApplyHiddenPanels(layout.HiddenPanels);
    }

    private static void ReorderPanelChildren(
        Panel panel,
        IReadOnlyList<string> desiredOrder,
        IReadOnlyDictionary<string, FrameworkElement> panelMap)
    {
        var orderedElements = desiredOrder
            .Where(panelMap.ContainsKey)
            .Select(panelId => panelMap[panelId])
            .ToList();

        foreach (var element in panel.Children.OfType<FrameworkElement>().ToList())
        {
            if (element.Tag is string panelId && panelMap.ContainsKey(panelId) && !orderedElements.Contains(element))
            {
                orderedElements.Add(element);
            }
        }

        for (var index = 0; index < orderedElements.Count; index++)
        {
            var element = orderedElements[index];
            if (panel.Children.Contains(element))
            {
                panel.Children.Remove(element);
                panel.Children.Insert(Math.Min(index, panel.Children.Count), element);
            }
        }
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

    private void ApplyHiddenPanels(IReadOnlyList<string> hiddenPanels)
    {
        var hidden = hiddenPanels.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (panelId, element) in BuildActionPanelMap())
        {
            element.Visibility = hidden.Contains(panelId) ? Visibility.Collapsed : Visibility.Visible;
        }

        foreach (var (panelId, element) in BuildContextPanelMap())
        {
            element.Visibility = hidden.Contains(panelId) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private Dictionary<string, FrameworkElement> BuildActionPanelMap() => new(StringComparer.OrdinalIgnoreCase)
    {
        [OccLayoutDefaults.ImmediateLanePanelId] = ImmediateLaneSection,
        [OccLayoutDefaults.KanbanPanelId] = KanbanSection
    };

    private Dictionary<string, FrameworkElement> BuildContextPanelMap() => new(StringComparer.OrdinalIgnoreCase)
    {
        [OccLayoutDefaults.BranchMetricsPanelId] = BranchMetricsSection,
        [OccLayoutDefaults.HighlightsPanelId] = HighlightsSection,
        [OccLayoutDefaults.AiFeedPanelId] = AiFeedSection,
        [OccLayoutDefaults.PlatformIntelligencePanelId] = PlatformIntelligenceExpander,
        [OccLayoutDefaults.AnalyticsPanelId] = AnalyticsTrendsExpander,
        [OccLayoutDefaults.DataHealthPanelId] = HealthChipsSection
    };

    private Dictionary<string, Border> BuildKpiCardMap() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["open"] = OpenKpiCard,
        ["hanging"] = HangingKpiCard,
        ["revenue"] = RevenueKpiCard,
        ["immediate"] = ImmediateActionKpiCard,
        ["sla"] = SlaBreachesKpiCard
    };

    private IEnumerable<FrameworkElement> EnumerateLayoutSections()
    {
        yield return ImmediateLaneSection;
        yield return KanbanSection;
        yield return BranchMetricsSection;
        yield return HighlightsSection;
        yield return AiFeedSection;
        yield return PlatformIntelligenceExpander;
        yield return AnalyticsTrendsExpander;
        yield return HealthChipsSection;
    }

    private IEnumerable<Border> EnumerateKpiCards()
    {
        yield return OpenKpiCard;
        yield return HangingKpiCard;
        yield return RevenueKpiCard;
        yield return ImmediateActionKpiCard;
        yield return SlaBreachesKpiCard;
    }

    private void OccSection_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        if (!_viewModel.IsLayoutEditMode || sender is not FrameworkElement element || element.Tag is not string panelId)
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
    }

    private async void OccSection_Drop(object sender, DragEventArgs e)
    {
        if (!_viewModel.IsLayoutEditMode || sender is not FrameworkElement target || target.Tag is not string targetPanelId)
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

        var actionMap = BuildActionPanelMap();
        var contextMap = BuildContextPanelMap();
        if (actionMap.ContainsKey(draggedId) && actionMap.ContainsKey(targetPanelId))
        {
            await PersistPanelReorderAsync(
                settings => settings.OccActionPanelOrder,
                (settings, order) => settings.OccActionPanelOrder = order,
                draggedId,
                targetPanelId,
                OccLayoutService.DefaultActionPanelOrder).ConfigureAwait(true);
        }
        else if (contextMap.ContainsKey(draggedId) && contextMap.ContainsKey(targetPanelId))
        {
            await PersistPanelReorderAsync(
                settings => settings.OccContextPanelOrder,
                (settings, order) => settings.OccContextPanelOrder = order,
                draggedId,
                targetPanelId,
                OccLayoutService.DefaultContextPanelOrder).ConfigureAwait(true);
        }

        _layoutDragSourcePanelId = null;
        ApplyLayoutPreferences();
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

        var position = e.GetPosition(OperationsKpiGrid);
        var targetIndex = (int)Math.Clamp(
            Math.Round(position.X / Math.Max(OperationsKpiGrid.ActualWidth / 5.0, 1)),
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

    private async Task PersistPanelReorderAsync(
        Func<AppSettings, List<string>> readOrder,
        Action<AppSettings, List<string>> writeOrder,
        string sourcePanelId,
        string targetPanelId,
        IReadOnlyList<string> defaults)
    {
        CaptureLayoutUndoSnapshot();
        await _services.AppSettings.UpdateAsync(settings =>
        {
            var working = readOrder(settings).Count > 0
                ? readOrder(settings).ToList()
                : defaults.ToList();
            working.RemoveAll(panel => panel.Equals(sourcePanelId, StringComparison.OrdinalIgnoreCase));
            var targetIndex = working.FindIndex(panel => panel.Equals(targetPanelId, StringComparison.OrdinalIgnoreCase));
            if (targetIndex < 0)
            {
                working.Add(sourcePanelId);
            }
            else
            {
                working.Insert(targetIndex, sourcePanelId);
            }

            writeOrder(settings, working);
        }).ConfigureAwait(true);

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

    private async void LayoutUndoInfoBar_ActionClick(object sender, RoutedEventArgs e)
    {
        if (_layoutUndoSnapshot is null)
        {
            LayoutUndoInfoBar.IsOpen = false;
            return;
        }

        var snapshot = _layoutUndoSnapshot;
        await _services.AppSettings.UpdateAsync(settings =>
            OccLayoutCommandHelper.ApplyLayoutSnapshot(settings, snapshot)).ConfigureAwait(true);

        _layoutUndoSnapshot = null;
        LayoutUndoInfoBar.IsOpen = false;
        ApplyLayoutPreferences();
    }
}
