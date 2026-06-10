using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Controls.Occ;
using UnifiedMessenger.Controls.Shared;
using UnifiedMessenger.Services;
using Windows.ApplicationModel.DataTransfer;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
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
}
