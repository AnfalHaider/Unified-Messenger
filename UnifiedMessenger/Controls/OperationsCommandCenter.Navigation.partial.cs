using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
    private DispatcherQueueTimer? _navigationStatusTimer;

    private void OnInstanceNavigationFailed(object? sender, InstanceNavigationFailedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Message))
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(() => ShowNavigationStatus(e.Message));
    }

    private void ShowNavigationStatus(string message)
    {
        NavigationStatusPanel.Visibility = Visibility.Visible;
        NavigationStatusText.Text = message;

        _navigationStatusTimer ??= _dispatcherQueue.CreateTimer();
        _navigationStatusTimer.Interval = TimeSpan.FromSeconds(8);
        _navigationStatusTimer.Tick -= OnNavigationStatusTimerTick;
        _navigationStatusTimer.Tick += OnNavigationStatusTimerTick;
        _navigationStatusTimer.Stop();
        _navigationStatusTimer.Start();
    }

    private void OnNavigationStatusTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= OnNavigationStatusTimerTick;
        sender.Stop();
        NavigationStatusPanel.Visibility = Visibility.Collapsed;
        NavigationStatusText.Text = string.Empty;
    }
}
