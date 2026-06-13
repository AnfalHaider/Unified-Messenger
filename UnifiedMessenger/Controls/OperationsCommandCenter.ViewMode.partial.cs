using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private bool _suppressViewModeEvents;

    private void InitializeViewModeToggle()
    {
        _suppressViewModeEvents = true;
        try
        {
            OccViewModeToggle.IsOn = _services.OccViewMode.IsHistorical;
        }
        finally
        {
            _suppressViewModeEvents = false;
        }

        MessageVolumeChart.IsHistoricalMode = _services.OccViewMode.IsHistorical;
    }

    private void OnOccViewModeChanged(object? sender, EventArgs e) =>
        _dispatcherQueue.TryEnqueue(() =>
        {
            InitializeViewModeToggle();
            ApplyScopeLabel(null, null);
            _ = RefreshAsync(_professionalInstances, _registry);
        });

    private async void OccViewModeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressViewModeEvents)
        {
            return;
        }

        _services.OccViewMode.Mode = OccViewModeToggle.IsOn
            ? OccViewMode.Historical
            : OccViewMode.Live;

        MessageVolumeChart.IsHistoricalMode = _services.OccViewMode.IsHistorical;

        try
        {
            await _services.AppSettings.UpdateAsync(settings =>
                    OccViewModeSettingsHelper.WriteToSettings(settings, _services.OccViewMode))
                .ConfigureAwait(true);
        }
        catch
        {
            // Non-fatal — mode still applies for this session.
        }
    }
}
