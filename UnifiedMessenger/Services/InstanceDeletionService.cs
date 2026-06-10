using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Centralizes instance removal so sidebar and Settings use the same teardown order.
/// </summary>
public static class InstanceDeletionService
{
    public static async Task DeleteAsync(
        ApplicationServices services,
        MessengerInstance instance,
        DeleteInstanceChoice choice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(instance);

        await services.SessionManager.CloseSessionAsync(instance.Id).ConfigureAwait(false);

        if (choice == DeleteInstanceChoice.RemoveFromSidebar)
        {
            await services.Registry.RemoveFromSidebarAsync(instance.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await WebViewProfileManager.Instance
                .PermanentlyDeleteProfileAsync(instance.ProfileName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await services.Registry.RemovePermanentlyAsync(instance.Id, cancellationToken).ConfigureAwait(false);
        }

        services.NotificationHub.RemoveAlertsForInstance(instance.Id);
        AdapterHealthMonitor.Instance.RemoveInstance(instance.Id);
        ProfessionalWorkspaceService.Instance.RemoveInstance(instance.Id);
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
