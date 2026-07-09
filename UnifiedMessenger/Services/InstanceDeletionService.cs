using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Centralizes instance removal so sidebar and Settings use the same teardown order.
/// </summary>
public static class InstanceDeletionService
{
    public static Task DeleteAsync(
        ApplicationServices services,
        MessengerInstance instance,
        DeleteInstanceChoice choice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(instance);

        // The whole teardown touches WebView2 (COM/STA) and UI-coupled services, so it MUST run on the UI
        // dispatcher thread. A plain ConfigureAwait(false)/(true) is not enough — WinRT awaitables routinely
        // resume on a thread-pool thread regardless, which threw "the application called an interface that
        // was marshalled for a different thread" when removing an instance. UiThreadRunner pins it.
        return UiThreadRunner.RunAsync(() => DeleteCoreAsync(services, instance, choice, cancellationToken));
    }

    private static async Task DeleteCoreAsync(
        ApplicationServices services,
        MessengerInstance instance,
        DeleteInstanceChoice choice,
        CancellationToken cancellationToken)
    {
        await services.SessionManager.CloseSessionAsync(instance.Id).ConfigureAwait(true);

        if (choice == DeleteInstanceChoice.RemoveFromSidebar)
        {
            await services.Registry.RemoveFromSidebarAsync(instance.Id, cancellationToken).ConfigureAwait(true);
        }
        else
        {
            await WebViewProfileManager.Instance
                .PermanentlyDeleteProfileAsync(instance.ProfileName, cancellationToken: cancellationToken)
                .ConfigureAwait(true);
            await services.Registry.RemovePermanentlyAsync(instance.Id, cancellationToken).ConfigureAwait(true);
        }

        services.NotificationHub.RemoveAlertsForInstance(instance.Id);
        WhatsAppBusinessContextService.Instance.RemoveInstance(instance.Id);
        AdapterHealthMonitor.Instance.RemoveInstance(instance.Id);
    }

    public static MessengerInstance? ResolveInstance(IInstanceRegistryService registry, string instanceId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        return registry.FindById(instanceId) ??
               registry.ArchivedInstances.FirstOrDefault(
                   i => i.Id.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
    }
}
