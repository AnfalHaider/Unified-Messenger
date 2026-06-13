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
        NotifyHeaderStatusChanged();
    }

    public event EventHandler? HeaderStatusChanged;

    public string HeaderLastRefreshedText => _viewModel.LastRefreshedText;

    public bool HeaderShowBackfillStatus => _viewModel.ShowBackfillStatus;

    public string HeaderBackfillStatusText => _viewModel.BackfillStatusText;

    private void NotifyHeaderStatusChanged() => HeaderStatusChanged?.Invoke(this, EventArgs.Empty);
}
