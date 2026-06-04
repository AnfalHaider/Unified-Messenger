namespace UnifiedMessenger.Models;

public static class RelativeTimeFormatter
{
    public static string Format(DateTimeOffset timestamp, DateTimeOffset? reference = null)
    {
        var delta = (reference ?? DateTimeOffset.Now) - timestamp;
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

        return timestamp.ToLocalTime().ToString("MMM d");
    }
}
