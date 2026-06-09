using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class PersonalActivityItem
{
    public required NotificationAlert Alert { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public required string InstanceDisplayName { get; init; }

    public required string RelativeTimeText { get; init; }

    public required string IconGlyph { get; init; }

    public required string AccentColorHex { get; init; }

    public bool IsUnread { get; init; }
}

public sealed class PersonalInstanceTileDisplay
{
    public required string InstanceId { get; init; }

    public required string DisplayName { get; init; }

    public required string PlatformLabel { get; init; }

    public required string DetailLine { get; init; }

    public required string ConnectionStatusLabel { get; init; }

    public required string ConnectionColorHex { get; init; }

    public required string IconGlyph { get; init; }

    public required string AccentColorHex { get; init; }

    public int UnreadCount { get; init; }

    public bool IsMuted { get; init; }
}

public sealed class PersonalDashboardSnapshot
{
    public int PersonalAccountCount { get; init; }

    public int TotalUnreadCount { get; init; }

    public long AppWorkingSetMegabytes { get; init; }

    public string VisibleInstanceName { get; init; } = "None";

    public string? MostUnreadInstanceId { get; init; }

    public int MostUnreadCount { get; init; }

    public IReadOnlyList<PersonalActivityItem> RecentActivity { get; init; } = [];

    public IReadOnlyList<PersonalInstanceTileDisplay> InstanceTiles { get; init; } = [];

    public PersonalDashboardEmptyReason EmptyReason { get; init; }

    public DateTimeOffset CapturedAtUtc { get; init; }
}

public sealed class PersonalDashboardService
{
    public const int MaxDisplayedActivityItems = 50;

    private static readonly Lazy<PersonalDashboardService> LazyInstance =
        new(() => new PersonalDashboardService());

    public static PersonalDashboardService Instance => LazyInstance.Value;

    internal static PersonalDashboardService CreateForTests() => new();

    public PersonalDashboardSnapshot BuildSnapshot(
        IEnumerable<MessengerInstance> personalInstances,
        INotificationHubService notificationHub,
        IInstanceSessionManager sessionManager,
        ResourceMonitorService resourceMonitor,
        AdapterHealthMonitor healthMonitor,
        InstanceConnectionStatusService? connectionStatusService = null)
    {
        ArgumentNullException.ThrowIfNull(personalInstances);
        ArgumentNullException.ThrowIfNull(notificationHub);
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(resourceMonitor);
        ArgumentNullException.ThrowIfNull(healthMonitor);

        var connectionService = connectionStatusService ?? InstanceConnectionStatusService.Instance;

        var instanceList = personalInstances
            .Where(instance => !string.IsNullOrWhiteSpace(instance.Id))
            .ToList();

        var instanceLookup = instanceList.ToDictionary(
            instance => instance.Id.Trim(),
            instance => instance,
            StringComparer.OrdinalIgnoreCase);

        var personalIds = instanceLookup.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var emptyReason = ResolveEmptyReason(instanceList, personalIds, notificationHub);
        var resourceSnapshot = resourceMonitor.Capture(
            instanceList,
            sessionManager,
            notificationHub,
            healthMonitor);

        var mostUnread = resourceSnapshot.InstanceTiles
            .OrderByDescending(tile => tile.UnreadCount)
            .ThenBy(tile => tile.DisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return new PersonalDashboardSnapshot
        {
            PersonalAccountCount = resourceSnapshot.ActiveAccountCount,
            TotalUnreadCount = resourceSnapshot.TotalUnreadCount,
            AppWorkingSetMegabytes = resourceSnapshot.WorkingSetMegabytes,
            VisibleInstanceName = resourceSnapshot.VisibleInstanceName,
            MostUnreadInstanceId = mostUnread is { UnreadCount: > 0 } ? mostUnread.InstanceId : null,
            MostUnreadCount = mostUnread?.UnreadCount ?? 0,
            RecentActivity = BuildRecentActivity(instanceLookup, notificationHub, personalIds),
            InstanceTiles = resourceSnapshot.InstanceTiles
                .Select(tile => BuildInstanceTileDisplay(
                    tile,
                    instanceLookup.GetValueOrDefault(tile.InstanceId),
                    connectionService))
                .ToList(),
            EmptyReason = emptyReason,
            CapturedAtUtc = DateTimeOffset.UtcNow
        };
    }

    internal static IReadOnlyList<NotificationAlert> GetPersonalAlertsSortedByRecency(
        INotificationHubService notificationHub,
        IReadOnlySet<string> personalInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(notificationHub);
        ArgumentNullException.ThrowIfNull(personalInstanceIds);

        return notificationHub.Alerts
            .Where(alert => personalInstanceIds.Contains(alert.InstanceId))
            .OrderByDescending(alert => alert.ReceivedAt)
            .ToList();
    }

    internal static PersonalDashboardEmptyReason ResolveEmptyReason(
        IReadOnlyList<MessengerInstance> personalInstances,
        IReadOnlySet<string> personalInstanceIds,
        INotificationHubService notificationHub)
    {
        ArgumentNullException.ThrowIfNull(notificationHub);

        if (personalInstances.Count == 0)
        {
            return PersonalDashboardEmptyReason.NoPersonalAccounts;
        }

        if (personalInstances.All(instance => instance.NotificationsMuted))
        {
            return PersonalDashboardEmptyReason.AllAccountsMuted;
        }

        var hasActivity = notificationHub.Alerts
            .Any(alert => personalInstanceIds.Contains(alert.InstanceId));

        return hasActivity
            ? PersonalDashboardEmptyReason.HasData
            : PersonalDashboardEmptyReason.NoRecentActivity;
    }

    private static IReadOnlyList<PersonalActivityItem> BuildRecentActivity(
        IReadOnlyDictionary<string, MessengerInstance> instanceLookup,
        INotificationHubService notificationHub,
        IReadOnlySet<string> personalInstanceIds)
    {
        var activity = new List<PersonalActivityItem>();
        foreach (var alert in GetPersonalAlertsSortedByRecency(notificationHub, personalInstanceIds))
        {
            instanceLookup.TryGetValue(alert.InstanceId, out var instance);
            activity.Add(new PersonalActivityItem
            {
                Alert = alert,
                Title = alert.Title,
                Body = alert.Body,
                InstanceDisplayName = alert.InstanceDisplayName,
                RelativeTimeText = alert.RelativeTimeText,
                IconGlyph = instance?.IconGlyph ?? alert.IconGlyph,
                AccentColorHex = instance?.AccentColor ?? PlatformBrandingHelper.DefaultAccentHex,
                IsUnread = !alert.IsRead
            });

            if (activity.Count >= MaxDisplayedActivityItems)
            {
                break;
            }
        }

        return activity;
    }

    private static PersonalInstanceTileDisplay BuildInstanceTileDisplay(
        InstanceResourceTile tile,
        MessengerInstance? instance,
        InstanceConnectionStatusService connectionService)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(connectionService);

        var connectionStatus = connectionService.GetStatus(tile.InstanceId);
        var connectionDetail = connectionService.GetDetail(tile.InstanceId);
        var platform = PlatformDefinition.FindById(tile.Platform);
        var isMuted = instance?.NotificationsMuted ?? false;

        return new PersonalInstanceTileDisplay
        {
            InstanceId = tile.InstanceId,
            DisplayName = tile.DisplayName,
            PlatformLabel = platform?.DisplayName ?? tile.Platform,
            DetailLine = DashboardPageHelper.BuildPersonalTileDetailLine(
                tile,
                connectionStatus,
                isMuted,
                connectionDetail),
            ConnectionStatusLabel = DashboardPageHelper.FormatConnectionPillLabel(connectionStatus),
            ConnectionColorHex = DashboardPageHelper.FormatConnectionColorHex(
                connectionStatus,
                tile.HealthState),
            IconGlyph = tile.IconGlyph,
            AccentColorHex = tile.AccentColor,
            UnreadCount = tile.UnreadCount,
            IsMuted = isMuted
        };
    }
}
