using Microsoft.UI.Xaml;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
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
}
