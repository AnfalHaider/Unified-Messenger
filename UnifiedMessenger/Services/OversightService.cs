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
        IReadOnlyList<MessengerInstance> professionalInstances,
        OversightWindow window = OversightWindow.Today)
    {
        ArgumentNullException.ThrowIfNull(professionalInstances);

        // Date window in the operator's local day. On-time is measured over conversations active in
        // the window (today by default — including messages that arrived before connecting today).
        var nowLocal = DateTimeOffset.Now;
        DateTimeOffset? windowStart = window switch
        {
            OversightWindow.Today => new DateTimeOffset(nowLocal.Date, nowLocal.Offset),
            OversightWindow.Week => new DateTimeOffset(nowLocal.Date.AddDays(-6), nowLocal.Offset),
            _ => null
        };

        var allowed = professionalInstances
            .Select(instance => instance.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var threads = ThreadRegistryService.Instance.GetAllThreads()
            .Where(thread => allowed.Count == 0 || allowed.Contains(thread.InstanceId))
            .ToList();

        // Canonical, per-account location key (each account → exactly one location). Resolved from the
        // instance's assigned branch, not the per-thread BranchName, so inconsistently-tagged threads
        // don't split an account across buckets.
        var locationByInstance = professionalInstances
            .Where(instance => !string.IsNullOrWhiteSpace(instance.Id))
            .GroupBy(instance => instance.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => BranchWorkspaceHelper.ResolveBranchKey(group.First()),
                StringComparer.OrdinalIgnoreCase);

        return OversightRollupBuilder.Build(
            threads,
            professionalInstances,
            grouping,
            OperationalThresholds.GetSlaThresholdMinutes,
            instanceId => InstanceConnectionStatusService.Instance.GetStatus(instanceId)
                != InstanceConnectionStatus.Connected,
            nowUtc: null,
            locationForInstance: instanceId =>
                locationByInstance.TryGetValue(instanceId, out var location) ? location : string.Empty,
            windowStartUtc: windowStart,
            windowEndUtc: null,
            chatSnapshot: instanceId =>
                OversightChatSnapshotService.Instance.TryGetWindowed(instanceId, windowStart, out var active, out var caughtUp)
                    ? (active, caughtUp)
                    : null);
    }
}
