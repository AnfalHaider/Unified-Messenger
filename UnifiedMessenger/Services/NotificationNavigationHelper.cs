using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class NotificationNavigationHelper
{
    public static void OpenAlert(INavigationService navigation, NotificationAlert alert)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(alert);

        if (alert.HasConversationTarget)
        {
            navigation.OpenInstance(alert.InstanceId, alert.ConversationKey, alert.CustomerName);
            return;
        }

        navigation.OpenInstance(alert.InstanceId);
    }

    public static void OpenToastActivation(
        INavigationService navigation,
        ToastActivationEventArgs activation,
        NotificationAlert? alert = null)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(activation);

        var conversationKey = alert?.ConversationKey;
        var customerName = alert?.CustomerName ?? alert?.Title;

        if (!string.IsNullOrWhiteSpace(activation.ConversationKey))
        {
            conversationKey = activation.ConversationKey;
        }

        if (!string.IsNullOrWhiteSpace(activation.CustomerName))
        {
            customerName = activation.CustomerName;
        }

        if (!string.IsNullOrWhiteSpace(conversationKey) || !string.IsNullOrWhiteSpace(customerName))
        {
            navigation.OpenInstance(activation.InstanceId, conversationKey, customerName);
            return;
        }

        navigation.OpenInstance(activation.InstanceId);
    }
}
