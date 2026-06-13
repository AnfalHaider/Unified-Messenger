namespace UnifiedMessenger.Services;

/// <summary>
/// Clears persisted operational telemetry (analytics + thread registry) together.
/// </summary>
public static class OperationalDataService
{
    public static async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await MessageAnalyticsService.Instance.ClearAllDataAsync(cancellationToken).ConfigureAwait(false);
        await TriagePersistenceService.Instance.ClearAsync(cancellationToken).ConfigureAwait(false);
        NotificationHub.Instance.ClearAlerts();
        UnifiedMessengerDashboardService.Instance.NotifyChanged();
    }
}
