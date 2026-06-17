using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Bridges the pure <see cref="OversightRollupBuilder"/> to live data: the thread registry, the
/// caller's professional instances, per-location SLA thresholds, and live connection status (for the
/// freshness/stale signal). This is the snapshot the command-center UI binds to.
/// </summary>
public sealed class OversightService
{
    private static readonly Lazy<OversightService> LazyInstance = new(() => new OversightService());

    public static OversightService Instance => LazyInstance.Value;

    private OversightService()
    {
    }

    public OversightCommandCenterSnapshot BuildSnapshot(
        OversightGrouping grouping,
        IReadOnlyList<MessengerInstance> professionalInstances)
    {
        ArgumentNullException.ThrowIfNull(professionalInstances);

        var allowed = professionalInstances
            .Select(instance => instance.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var threads = ThreadRegistryService.Instance.GetAllThreads()
            .Where(thread => allowed.Count == 0 || allowed.Contains(thread.InstanceId))
            .ToList();

        return OversightRollupBuilder.Build(
            threads,
            professionalInstances,
            grouping,
            OperationalThresholds.GetSlaThresholdMinutes,
            instanceId => InstanceConnectionStatusService.Instance.GetStatus(instanceId)
                != InstanceConnectionStatus.Connected);
    }
}
