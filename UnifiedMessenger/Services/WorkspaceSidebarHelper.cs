using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class WorkspaceSidebarHelper
{
    public const int MaxBadgeValue = 99;

    public const string DashboardSelectionKey = "dashboard";

    public static string ResolveSelectionKey(bool dashboardSelected, string? instanceId)
    {
        if (dashboardSelected || string.IsNullOrWhiteSpace(instanceId))
        {
            return DashboardSelectionKey;
        }

        return instanceId.Trim();
    }

    public static bool IsSelectionMatch(string? selectedKey, string rowKey) =>
        string.Equals(selectedKey, rowKey, StringComparison.OrdinalIgnoreCase);

    public static int ClampBadgeCount(int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        return count > MaxBadgeValue ? MaxBadgeValue : count;
    }

    public static (IReadOnlyList<MessengerInstance> Professional, IReadOnlyList<MessengerInstance> Personal)
        PartitionInstances(IEnumerable<MessengerInstance> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);

        var validInstances = instances
            .Where(instance => !string.IsNullOrWhiteSpace(instance.Id))
            .GroupBy(instance => instance.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var professional = validInstances
            .Where(instance => instance.IsProfessional)
            .OrderBy(instance => instance.SortOrder)
            .ThenBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var personal = validInstances
            .Where(instance => !instance.IsProfessional)
            .OrderBy(instance => instance.SortOrder)
            .ThenBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (professional, personal);
    }

    public static string ResolveStatusSubtitle(AdapterHealthState state, bool notificationsMuted)
    {
        if (notificationsMuted)
        {
            return "Notifications muted";
        }

        return state switch
        {
            AdapterHealthState.Healthy => "Status: Online",
            AdapterHealthState.Ready => "Status: Ready",
            AdapterHealthState.Stale => "Status: Stale",
            AdapterHealthState.NoAdapter => "Status: Starting",
            _ => "Status: Unknown"
        };
    }

    public static Windows.UI.Color ResolveHealthIndicatorColor(AdapterHealthState state) =>
        state switch
        {
            AdapterHealthState.Healthy => Windows.UI.Color.FromArgb(255, 16, 124, 16),
            AdapterHealthState.Ready => Windows.UI.Color.FromArgb(255, 0, 99, 177),
            AdapterHealthState.Stale => Windows.UI.Color.FromArgb(255, 196, 89, 17),
            AdapterHealthState.NoAdapter => Windows.UI.Color.FromArgb(255, 128, 128, 128),
            _ => Windows.UI.Color.FromArgb(255, 160, 160, 160)
        };

    public static bool ShouldAcceptReorder(string? sourceInstanceId, string? targetInstanceId)
    {
        if (!ShellNavigationService.IsValidInstanceId(sourceInstanceId) ||
            !ShellNavigationService.IsValidInstanceId(targetInstanceId))
        {
            return false;
        }

        return !sourceInstanceId!.Trim().Equals(targetInstanceId!.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    internal static string? ResolveDropTargetInstanceId(
        double dropY,
        IReadOnlyList<SidebarRowBounds> rowBounds)
    {
        ArgumentNullException.ThrowIfNull(rowBounds);

        foreach (var bounds in rowBounds)
        {
            if (dropY >= bounds.Top && dropY <= bounds.Bottom)
            {
                return bounds.InstanceId;
            }
        }

        return null;
    }
}

internal readonly record struct SidebarRowBounds(string InstanceId, double Top, double Bottom);
