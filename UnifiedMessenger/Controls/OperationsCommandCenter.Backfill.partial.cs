using Microsoft.UI.Xaml;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private bool _backfillEventsAttached;

    private void EnsureBackfillStatusSubscription()
    {
        if (_backfillEventsAttached)
        {
            return;
        }

        _backfillEventsAttached = true;
        BackfillSyncManager.Instance.ProgressChanged += OnBackfillProgressChanged;
    }

    private void OnBackfillProgressChanged(object? sender, BackfillProgressEventArgs e)
    {
        _ = _dispatcherQueue.TryEnqueue(ApplyBackfillStatusUi);
    }

    private void ApplyBackfillStatusUi()
    {
        var status = OccBackfillStatusPresenter.BuildStatus(_professionalInstances);
        _viewModel.ApplyBackfillStatus(status);
        BackfillStatusText.Text = _viewModel.BackfillStatusText;
        BackfillStatusPanel.Visibility = _viewModel.ShowBackfillStatus
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
