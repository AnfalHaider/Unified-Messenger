using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Services;

public static class OccChartBackfillHelper
{
    public static bool HasEmptyChartBuckets(IReadOnlyList<DailyActivityPoint>? series) =>
        series is null ||
        series.Count == 0 ||
        series.All(point => point.Sent == 0 && point.Received == 0);

    public static bool IsConnectedWhatsAppInstance(
        MessengerInstance instance,
        InstanceConnectionStatusService? connectionStatus = null)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (string.IsNullOrWhiteSpace(instance.Id))
        {
            return false;
        }

        var provider = new WhatsAppBackfillProvider();
        if (!provider.CanBackfill(instance))
        {
            return false;
        }

        var connection = connectionStatus ?? InstanceConnectionStatusService.Instance;
        return connection.GetStatus(instance.Id) == InstanceConnectionStatus.Connected;
    }

    public static IReadOnlyList<MessengerInstance> ResolveConnectedWhatsAppInstances(
        IEnumerable<MessengerInstance> professionalInstances,
        string? selectedBranchKey,
        InstanceConnectionStatusService? connectionStatus = null)
    {
        ArgumentNullException.ThrowIfNull(professionalInstances);

        var connection = connectionStatus ?? InstanceConnectionStatusService.Instance;
        return DashboardPageHelper
            .FilterProfessionalInstances(professionalInstances, selectedBranchKey)
            .Where(instance => IsConnectedWhatsAppInstance(instance, connection))
            .ToList();
    }

    public static void TryScheduleBackfillForEmptyChart(
        IReadOnlyList<DailyActivityPoint>? series,
        IEnumerable<MessengerInstance> professionalInstances,
        string? selectedBranchKey,
        BackfillSyncManager? backfillSyncManager = null,
        InstanceConnectionStatusService? connectionStatus = null)
    {
        if (!HasEmptyChartBuckets(series))
        {
            return;
        }

        var manager = backfillSyncManager ?? BackfillSyncManager.Instance;
        foreach (var instance in ResolveConnectedWhatsAppInstances(
                     professionalInstances,
                     selectedBranchKey,
                     connectionStatus))
        {
            manager.Schedule(instance, force: true);
        }
    }
}
