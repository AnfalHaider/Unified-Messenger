using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Presenters;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ai;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private bool _backfillEventsAttached;
    private bool _aiStatusEventsAttached;

    private void OnInstanceNavigationFailed(object? sender, InstanceNavigationFailedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Message))
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(() => ShowNavigationStatus(e.Message, isError: true));
    }

    private void ShowNavigationStatus(string message, bool isError = true)
    {
        NavigationStatusInfoBar.Message = message;
        NavigationStatusInfoBar.Severity = isError
            ? Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error
            : Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;
        NavigationStatusInfoBar.IsOpen = true;
    }

    private void NavigationStatusInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        NavigationStatusInfoBar.IsOpen = false;
        NavigationStatusInfoBar.Message = string.Empty;
    }

    private async void NavigateToThreadCard(OperationsThreadCardViewModel card)
    {
        if (string.IsNullOrWhiteSpace(card.InstanceId))
        {
            return;
        }

        SetWorkspaceLoadingVisible(true);
        try
        {
            var result = await ConversationNavigationCoordinator.NavigateToThreadAsync(
                _services.SessionManager,
                _services.Registry,
                _services.ThreadRegistry,
                _services.Navigation,
                card.InstanceId,
                card.ConversationKey,
                card.CustomerName,
                card.ThreadId).ConfigureAwait(true);

            if (!string.IsNullOrWhiteSpace(result.StatusMessage) && result.IsFailure)
            {
                ShowNavigationStatus(result.StatusMessage, isError: true);
            }
            else if (!string.IsNullOrWhiteSpace(result.StatusMessage))
            {
                ShowNavigationStatus(result.StatusMessage, isError: false);
            }
        }
        finally
        {
            SetWorkspaceLoadingVisible(false);
        }
    }

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

    private void EnsureAiStatusSubscription()
    {
        if (_aiStatusEventsAttached)
        {
            return;
        }

        _services.OllamaRuntime.ConnectionStateChanged += OnOllamaConnectionStateChanged;
        _services.AiInferenceQueue.Changed += OnAiInferenceQueueChanged;
        _services.AppSettings.Changed += OnAppSettingsChangedForAiChip;
        _aiStatusEventsAttached = true;
        ApplyAiStatusChip();
    }

    private void ReleaseAiStatusSubscription()
    {
        if (!_aiStatusEventsAttached)
        {
            return;
        }

        _services.OllamaRuntime.ConnectionStateChanged -= OnOllamaConnectionStateChanged;
        _services.AiInferenceQueue.Changed -= OnAiInferenceQueueChanged;
        _services.AppSettings.Changed -= OnAppSettingsChangedForAiChip;
        _aiStatusEventsAttached = false;
    }

    private void OnOllamaConnectionStateChanged(object? sender, OllamaConnectionState e) =>
        _dispatcherQueue.TryEnqueue(ApplyAiStatusChip);

    private void OnAiInferenceQueueChanged(object? sender, EventArgs e) =>
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_snapshot.ThreadOperations is not null)
            {
                ApplyImmediateQueue(_snapshot.ThreadOperations);
                ApplyKanban(_snapshot.ThreadOperations);
            }
        });

    private void OnAppSettingsChangedForAiChip(object? sender, EventArgs e) =>
        _dispatcherQueue.TryEnqueue(ApplyAiStatusChip);

    private void ApplyAiStatusChip()
    {
        if (AiStatusChipText is null)
        {
            return;
        }

        var settings = _services.AppSettings.Settings;
        AiStatusChipText.Text = AiSettingsSectionHelper.DescribeOccAiChip(
            settings,
            _services.OllamaRuntime.ConnectionState);
        AiStatusChipPanel.Visibility = Visibility.Visible;
    }
}
