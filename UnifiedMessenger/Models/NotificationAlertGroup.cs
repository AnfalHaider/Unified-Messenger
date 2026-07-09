namespace UnifiedMessenger.Models;

public sealed class NotificationAlertGroup : List<NotificationAlert>
{
    public NotificationAlertGroup(
        string instanceId,
        string instanceDisplayName,
        IEnumerable<NotificationAlert> alerts)
        : base(alerts)
    {
        InstanceId = instanceId;
        Key = instanceDisplayName;
    }

    /// <summary>Stable grouping key for the account.</summary>
    public string InstanceId { get; }

    /// <summary>Display label used by notification feed headers.</summary>
    public string Key { get; }
}
