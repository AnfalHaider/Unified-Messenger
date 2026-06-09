using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Services;

/// <summary>
/// Clears persisted operational telemetry (analytics + thread registry) together.
/// </summary>
public static class OperationalDataService
{
    public static async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await MessageAnalyticsService.Instance.ClearAllDataAsync(cancellationToken).ConfigureAwait(false);
        await RichTriageStoreService.Instance.ClearAsync(cancellationToken).ConfigureAwait(false);
        MessageTriageService.Instance.DrainPendingQueue();
        MessageTriageService.Instance.RestoreItems([]);
        ThreadRegistryService.Instance.RestoreThreads([]);
        ProfessionalWorkspaceService.Instance.ClearOperationalData();
        NotificationHub.Instance.ClearAlerts();
        BackfillDedupeRegistry.Clear();
        UnifiedMessengerDashboardService.Instance.NotifyChanged();
    }
}
