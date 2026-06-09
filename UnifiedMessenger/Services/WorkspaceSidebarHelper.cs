using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class WorkspaceSidebarHelper
{
    public const int MaxBadgeValue = 99;

    public const string DashboardSelectionKey = "dashboard";

    public const string SettingsSelectionKey = "settings";

    public static string ResolveSelectionKey(
        bool dashboardSelected,
        string? instanceId,
        bool settingsSelected = false)
    {
        if (settingsSelected)
        {
            return SettingsSelectionKey;
        }

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

    public static string ResolveStatusSubtitle(
        InstanceConnectionStatus connectionStatus,
        AdapterHealthState adapterState,
        bool notificationsMuted,
        string? connectionDetail = null)
    {
        if (notificationsMuted)
        {
            return "Notifications muted";
        }

        if (connectionStatus == InstanceConnectionStatus.Connected &&
            !string.IsNullOrWhiteSpace(connectionDetail))
        {
            return FormatConnectedDetailSubtitle(connectionDetail);
        }

        return connectionStatus switch
        {
            InstanceConnectionStatus.Connected when adapterState == AdapterHealthState.Healthy =>
                "Status: Connected",
            InstanceConnectionStatus.Connected => "Status: Connected · syncing",
            InstanceConnectionStatus.LoggedOut => "Status: Logged out",
            InstanceConnectionStatus.Error => "Status: Error",
            InstanceConnectionStatus.Initializing => "Status: Connecting…",
            _ => "Status: Connecting…"
        };
    }

    internal static string FormatConnectedDetailSubtitle(string connectionDetail)
    {
        var detail = connectionDetail.Trim();
        if (detail.StartsWith("Status:", StringComparison.OrdinalIgnoreCase))
        {
            return detail;
        }

        return detail.StartsWith("Connected", StringComparison.OrdinalIgnoreCase)
            ? $"Status: {detail}"
            : $"Status: Connected · {detail}";
    }

    public static Windows.UI.Color ResolveConnectionIndicatorColor(
        InstanceConnectionStatus connectionStatus,
        AdapterHealthState adapterState) =>
        connectionStatus switch
        {
            InstanceConnectionStatus.Connected => Windows.UI.Color.FromArgb(255, 16, 124, 16),
            InstanceConnectionStatus.LoggedOut => Windows.UI.Color.FromArgb(255, 196, 89, 17),
            InstanceConnectionStatus.Error => Windows.UI.Color.FromArgb(255, 196, 43, 28),
            InstanceConnectionStatus.Initializing => Windows.UI.Color.FromArgb(255, 0, 99, 177),
            _ => adapterState switch
            {
                AdapterHealthState.Healthy => Windows.UI.Color.FromArgb(255, 16, 124, 16),
                AdapterHealthState.Ready => Windows.UI.Color.FromArgb(255, 0, 99, 177),
                AdapterHealthState.Stale => Windows.UI.Color.FromArgb(255, 196, 89, 17),
                AdapterHealthState.NoAdapter => Windows.UI.Color.FromArgb(255, 128, 128, 128),
                _ => Windows.UI.Color.FromArgb(255, 160, 160, 160)
            }
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
