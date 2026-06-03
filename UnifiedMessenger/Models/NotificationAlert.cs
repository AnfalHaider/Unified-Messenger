namespace UnifiedMessenger.Models;

public sealed class NotificationAlert
{
    public required string Id { get; init; }

    public required string InstanceId { get; init; }

    public required string InstanceDisplayName { get; init; }

    public required string Platform { get; init; }

    public string IconGlyph { get; init; } = "\uE8BD";

    public required string Title { get; init; }

    public string Body { get; init; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool IsRead { get; set; }

    public string RelativeTimeText => FormatRelativeTime(ReceivedAt);

    private static string FormatRelativeTime(DateTimeOffset time)
    {
        var delta = DateTimeOffset.Now - time;
        if (delta.TotalSeconds < 45)
        {
            return "Just now";
        }

        if (delta.TotalMinutes < 60)
        {
            return $"{(int)delta.TotalMinutes}m ago";
        }

        if (delta.TotalHours < 24)
        {
            return $"{(int)delta.TotalHours}h ago";
        }

        if (delta.TotalDays < 7)
        {
            return $"{(int)delta.TotalDays}d ago";
        }

        return time.ToLocalTime().ToString("MMM d");
    }
}
