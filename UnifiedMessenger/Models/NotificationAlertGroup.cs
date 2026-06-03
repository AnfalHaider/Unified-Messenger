namespace UnifiedMessenger.Models;

public sealed class NotificationAlertGroup : List<NotificationAlert>
{
    public NotificationAlertGroup(string instanceDisplayName, IEnumerable<NotificationAlert> alerts)
        : base(alerts)
    {
        Key = instanceDisplayName;
    }

    public string Key { get; }
}
