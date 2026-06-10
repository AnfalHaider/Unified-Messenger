using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public interface IAppNotificationService
{
    event EventHandler<ToastActivationEventArgs>? ActivationRequested;

    void Initialize();

    void Shutdown();

    void ShowAlertToast(NotificationAlert alert, MessengerInstance? instance = null);

    bool TryHandleLaunchActivation();
}
