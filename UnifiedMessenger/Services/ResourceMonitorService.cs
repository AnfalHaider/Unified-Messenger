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

    public string MemoryTier { get; init; } = "Low";
}

public sealed class ResourceMonitorService
{
    private static readonly Lazy<ResourceMonitorService> LazyInstance = new(() => new ResourceMonitorService());

    public static ResourceMonitorService Instance => LazyInstance.Value;

    public ResourceSnapshot Capture(
        IEnumerable<MessengerInstance> instances,
        InstanceSessionManager sessionManager,
        NotificationHub notificationHub,
        AdapterHealthMonitor healthMonitor)
    {
        var instanceList = instances.ToList();
        var visibleId = sessionManager.VisibleInstanceId;
        var process = Process.GetCurrentProcess();

        var tiles = instanceList.Select(instance =>
        {
            var isVisible = instance.Id.Equals(visibleId, StringComparison.OrdinalIgnoreCase);
            return new InstanceResourceTile
            {
                InstanceId = instance.Id,
                DisplayName = instance.DisplayName,
                Platform = instance.Platform,
                AccentColor = instance.AccentColor,
                IconGlyph = instance.IconGlyph,
                UnreadCount = notificationHub.GetBadgeCount(instance.Id),
                HealthState = healthMonitor.GetStatus(instance.Id).State,
                IsVisible = isVisible,
                MemoryTier = isVisible ? "Normal" : "Low"
            };
        }).ToList();

        var visibleName = instanceList
            .FirstOrDefault(i => i.Id.Equals(visibleId, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName ?? "None";

        return new ResourceSnapshot
        {
            ActiveAccountCount = instanceList.Count,
            TotalUnreadCount = notificationHub.TotalUnreadCount,
            WorkingSetMegabytes = process.WorkingSet64 / (1024 * 1024),
            VisibleInstanceName = visibleName,
            InstanceTiles = tiles
        };
    }
}
