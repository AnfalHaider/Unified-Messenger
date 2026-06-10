using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
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
}
