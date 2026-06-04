using System.Diagnostics;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class ResourceSnapshot
{
    public int ActiveAccountCount { get; init; }

    public int TotalUnreadCount { get; init; }

    public long WorkingSetMegabytes { get; init; }

    public string VisibleInstanceName { get; init; } = "None";

    public IReadOnlyList<InstanceResourceTile> InstanceTiles { get; init; } = [];
}

public sealed class InstanceResourceTile
{
    public required string InstanceId { get; init; }

    public required string DisplayName { get; init; }

    public required string Platform { get; init; }

    public required string AccentColor { get; init; }

    public required string IconGlyph { get; init; }

    public int UnreadCount { get; init; }

    public AdapterHealthState HealthState { get; init; }

    public bool IsVisible { get; init; }

    public string MemoryTier { get; init; } = MemoryTierPreference.Normal.ToString();
}

public sealed class ResourceMonitorService
{
    private const long MegabyteDivisor = 1024 * 1024;

    private static readonly Lazy<ResourceMonitorService> LazyInstance = new(() => new ResourceMonitorService());

    private Func<long>? _workingSetBytesProvider;

    public static ResourceMonitorService Instance => LazyInstance.Value;

    internal ResourceMonitorService()
    {
    }

    internal static ResourceMonitorService CreateForTests(Func<long>? workingSetBytesProvider = null) =>
        new() { _workingSetBytesProvider = workingSetBytesProvider };

    public ResourceSnapshot Capture(
        IEnumerable<MessengerInstance> instances,
        InstanceSessionManager sessionManager,
        NotificationHub notificationHub,
        AdapterHealthMonitor healthMonitor)
    {
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(notificationHub);
        ArgumentNullException.ThrowIfNull(healthMonitor);

        var instanceList = instances
            .Where(instance => !string.IsNullOrWhiteSpace(instance.Id))
            .OrderBy(instance => instance.SortOrder)
            .ThenBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var visibleId = sessionManager.VisibleInstanceId;
        var tiles = instanceList
            .Select(instance => BuildTile(instance, visibleId, notificationHub, healthMonitor))
            .ToList();

        var workingSetBytes = _workingSetBytesProvider?.Invoke()
            ?? Process.GetCurrentProcess().WorkingSet64;

        return new ResourceSnapshot
        {
            ActiveAccountCount = instanceList.Count,
            TotalUnreadCount = SumInstanceUnreadCounts(instanceList, notificationHub),
            WorkingSetMegabytes = ConvertWorkingSetToMegabytes(workingSetBytes),
            VisibleInstanceName = ResolveVisibleDisplayName(instanceList, visibleId),
            InstanceTiles = tiles
        };
    }

    internal static long ConvertWorkingSetToMegabytes(long workingSetBytes) =>
        workingSetBytes / MegabyteDivisor;

    internal static string ResolveVisibleDisplayName(
        IReadOnlyList<MessengerInstance> instances,
        string? visibleInstanceId)
    {
        if (string.IsNullOrWhiteSpace(visibleInstanceId))
        {
            return "None";
        }

        var normalizedId = visibleInstanceId.Trim();
        return instances
            .FirstOrDefault(instance => instance.Id.Equals(normalizedId, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName ?? "None";
    }

    internal static int SumInstanceUnreadCounts(
        IReadOnlyList<MessengerInstance> instances,
        NotificationHub notificationHub)
    {
        ArgumentNullException.ThrowIfNull(notificationHub);

        return instances.Sum(instance => notificationHub.GetBadgeCount(instance.Id));
    }

    internal static InstanceResourceTile BuildTile(
        MessengerInstance instance,
        string? visibleInstanceId,
        NotificationHub notificationHub,
        AdapterHealthMonitor healthMonitor)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(notificationHub);
        ArgumentNullException.ThrowIfNull(healthMonitor);

        var instanceId = instance.Id.Trim();
        var isVisible = !string.IsNullOrWhiteSpace(visibleInstanceId)
            && instanceId.Equals(visibleInstanceId.Trim(), StringComparison.OrdinalIgnoreCase);

        return new InstanceResourceTile
        {
            InstanceId = instanceId,
            DisplayName = instance.DisplayName,
            Platform = instance.Platform,
            AccentColor = instance.AccentColor,
            IconGlyph = instance.IconGlyph,
            UnreadCount = notificationHub.GetBadgeCount(instanceId),
            HealthState = healthMonitor.GetStatus(instanceId).State,
            IsVisible = isVisible,
            MemoryTier = instance.MemoryTier.ToString()
        };
    }
}
