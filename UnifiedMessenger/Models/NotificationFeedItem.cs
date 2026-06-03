namespace UnifiedMessenger.Models;

public sealed class NotificationFeedItem
{
    public bool IsGroupHeader { get; init; }

    public string? GroupTitle { get; init; }

    public int GroupUnreadCount { get; init; }

    public string GroupUnreadLabel => GroupUnreadCount > 0 ? $"{GroupUnreadCount} unread" : string.Empty;

    public NotificationAlert? Alert { get; init; }

    public string IconGlyph { get; init; } = "\uE8BD";

    public string AccentColor { get; init; } = "#6B7280";

    public bool IsRead => Alert?.IsRead ?? true;

    public static NotificationFeedItem Header(string title, int unread) =>
        new() { IsGroupHeader = true, GroupTitle = title, GroupUnreadCount = unread };

    public static NotificationFeedItem FromAlert(NotificationAlert alert, MessengerInstance? instance) =>
        new()
        {
            Alert = alert,
            IconGlyph = instance?.IconGlyph ?? alert.IconGlyph,
            AccentColor = instance?.AccentColor ?? "#6B7280"
        };
}
