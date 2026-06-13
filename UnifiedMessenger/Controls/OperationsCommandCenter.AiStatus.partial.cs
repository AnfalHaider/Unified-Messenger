using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using UnifiedMessenger.Services;
using UnifiedMessenger.Services.Ai;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private bool _aiStatusEventsAttached;

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
