using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class WorkspaceSidebarHelper
{
    public const int MaxBadgeValue = 99;

    public const string DashboardSelectionKey = "dashboard";

    public const string SettingsSelectionKey = "settings";

    public const string NotificationHubSelectionKey = "notifications";

    public const string WorkQueueSelectionKey = "work-queue";

    public static string ResolveSelectionKey(
        bool dashboardSelected,
        string? instanceId,
        bool settingsSelected = false,
        bool notificationHubSelected = false,
        bool workQueueSelected = false)
    {
        if (notificationHubSelected)
        {
            return NotificationHubSelectionKey;
        }

        if (settingsSelected)
        {
            return SettingsSelectionKey;
        }

        if (workQueueSelected)
        {
            return WorkQueueSelectionKey;
        }

        if (dashboardSelected || string.IsNullOrWhiteSpace(instanceId))
        {
            return DashboardSelectionKey;
        }

        return instanceId.Trim();
    }

    public static ShellViewState ResolveShellViewState(
        bool dashboardSelected,
        string? instanceId,
        bool settingsSelected,
        bool notificationHubOpen,
        bool workQueueSelected = false)
    {
        if (notificationHubOpen)
        {
            return ShellViewState.NotificationHub;
        }

        if (settingsSelected)
        {
            return ShellViewState.Settings;
        }

        if (workQueueSelected)
        {
            return ShellViewState.WorkQueue;
        }

        if (!dashboardSelected && !string.IsNullOrWhiteSpace(instanceId))
        {
            return ShellViewState.Instance;
        }

        return ShellViewState.Dashboard;
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

    public static string FormatMemoryTierLabel(MemoryTierPreference tier) =>
        tier switch
        {
            MemoryTierPreference.Low => "Low",
            MemoryTierPreference.High => "High",
            _ => "Normal"
        };

    public static string AppendMemoryTierHint(string subtitle, MemoryTierPreference tier)
    {
        ArgumentNullException.ThrowIfNull(subtitle);

        if (tier == MemoryTierPreference.Normal)
        {
            return subtitle;
        }

        return $"{subtitle} · Memory: {FormatMemoryTierLabel(tier)}";
    }

    public static string ComposeInstanceTooltip(
        string displayName,
        WorkspaceCategory category,
        string statusSubtitle,
        string adapterDescription,
        MemoryTierPreference memoryTier,
        string? connectionDetail = null)
    {
        var detailLine = string.IsNullOrWhiteSpace(connectionDetail) ? string.Empty : $"\n{connectionDetail}";
        return
            $"{displayName}\nWorkspace: {category}\n{statusSubtitle}{detailLine}\nMemory tier: {FormatMemoryTierLabel(memoryTier)}\nAdapter: {adapterDescription}";
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

        if (connectionStatus == InstanceConnectionStatus.Connected)
        {
            return connectionStatus switch
            {
                _ when adapterState == AdapterHealthState.Healthy => "Connected",
                _ => "Connected · syncing"
            };
        }

        return connectionStatus switch
        {
            InstanceConnectionStatus.LoggedOut => "Signed out",
            InstanceConnectionStatus.Error => "Connection error",
            InstanceConnectionStatus.Initializing => "Connecting…",
            _ => "Connecting…"
        };
    }

    /// <summary>
    /// The row subtitle for the redesigned sidebar: the channel/platform name when the account is healthy
    /// (far more useful at a glance than a repeated "Connected · syncing"), and the problem state only when
    /// there is one to surface (signed out, connection error, muted). Transient connecting/syncing is left to
    /// the status dot's colour.
    /// </summary>
    public static string ComposeRowSubtitle(
        string? platformId,
        InstanceConnectionStatus connectionStatus,
        bool notificationsMuted)
    {
        if (notificationsMuted)
        {
            return "Notifications muted";
        }

        if (connectionStatus == InstanceConnectionStatus.LoggedOut)
        {
            return "Signed out — tap to reconnect";
        }

        if (connectionStatus == InstanceConnectionStatus.Error)
        {
            return "Connection error";
        }

        return PlatformDefinition.FindById(platformId)?.DisplayName ?? "Account";
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
