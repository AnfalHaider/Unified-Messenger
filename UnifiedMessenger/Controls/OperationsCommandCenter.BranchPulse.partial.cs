using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private bool _branchPulseSubscribed;

    private void EnsureBranchPulseSubscription()
    {
        if (_branchPulseSubscribed)
        {
            return;
        }

        BranchPulseService.Instance.Changed += OnBranchPulseChanged;
        _branchPulseSubscribed = true;
    }

    private void OnBranchPulseChanged(object? sender, BranchPulseChangedEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (!BranchPulseMatchesScope(e.Snapshot.BranchKey))
            {
                return;
            }

            ApplyBranchPulse(e.Snapshot);
        });
    }

    private bool BranchPulseMatchesScope(string? snapshotBranchKey)
    {
        if (string.IsNullOrWhiteSpace(_workspaceBranchKey))
        {
            return string.IsNullOrWhiteSpace(snapshotBranchKey);
        }

        return snapshotBranchKey?.Equals(_workspaceBranchKey, StringComparison.OrdinalIgnoreCase) == true;
    }

    private void ApplyBranchPulse(BranchPulseSnapshot snapshot)
    {
        BranchPulseScopeText.Text = snapshot.ScopeLabel;
        BranchPulseStatusText.Text = snapshot.StatusMessage;
        BranchPulseSummaryText.Text = snapshot.Summary;
        BranchPulseSummaryText.Visibility = string.IsNullOrWhiteSpace(snapshot.Summary)
            ? Visibility.Collapsed
            : Visibility.Visible;

        BranchPulseThemesList.ItemsSource = snapshot.Themes;
        BranchPulseThemesList.Visibility = snapshot.Themes.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        var isGenerating = snapshot.State == BranchPulseState.Generating;
        BranchPulseProgressRing.IsActive = isGenerating;
        BranchPulseProgressRing.Visibility = isGenerating ? Visibility.Visible : Visibility.Collapsed;
        BranchPulseRefreshButton.IsEnabled = !isGenerating;
    }

    private async Task RefreshBranchPulseAsync()
    {
        EnsureBranchPulseSubscription();

        var instanceList = _professionalInstances.ToList();
        var allowedIds = instanceList
            .Select(instance => instance.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scopedThreads = _services.ThreadRegistry.GetAllThreads()
            .Where(thread => allowedIds.Contains(thread.InstanceId))
            .ToList();

        var snapshot = await BranchPulseService.Instance.GenerateAsync(
            _workspaceBranchKey,
            scopedThreads,
            instanceList).ConfigureAwait(true);
        ApplyBranchPulse(snapshot);
    }

    private async void BranchPulseRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureBranchPulseSubscription();
        BranchPulseService.Instance.Invalidate(_workspaceBranchKey);

        var instanceList = _professionalInstances.ToList();
        var allowedIds = instanceList
            .Select(instance => instance.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scopedThreads = _services.ThreadRegistry.GetAllThreads()
            .Where(thread => allowedIds.Contains(thread.InstanceId))
            .ToList();

        var snapshot = await BranchPulseService.Instance.GenerateAsync(
            _workspaceBranchKey,
            scopedThreads,
            instanceList,
            force: true).ConfigureAwait(true);
        ApplyBranchPulse(snapshot);
    }
}
