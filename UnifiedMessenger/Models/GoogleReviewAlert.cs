namespace UnifiedMessenger.Models;

public sealed class GoogleReviewAlert
{
    public string Id { get; set; } = string.Empty;

    public string InstanceId { get; set; } = string.Empty;

    public string InstanceDisplayName { get; set; } = string.Empty;

    public string ReviewId { get; set; } = string.Empty;

    public string ReviewerName { get; set; } = string.Empty;

    public string Snippet { get; set; } = string.Empty;

    public string LocationLabel { get; set; } = string.Empty;

    public int Rating { get; set; }

    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsReplied { get; set; }

    public string RelativeTimeText
    {
        get
        {
            var delta = DateTimeOffset.UtcNow - DetectedAt;
            if (delta.TotalMinutes < 1)
            {
                return "Just now";
            }

            if (delta.TotalHours < 1)
            {
                return $"{(int)delta.TotalMinutes}m ago";
            }

            if (delta.TotalDays < 1)
            {
                return $"{(int)delta.TotalHours}h ago";
            }

            return $"{(int)delta.TotalDays}d ago";
        }
    }
}
