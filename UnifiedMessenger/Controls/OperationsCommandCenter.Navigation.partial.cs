using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Controls;

public sealed partial class OperationsCommandCenter
{
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
}
