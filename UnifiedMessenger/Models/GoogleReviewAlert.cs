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

    public string RelativeTimeText => RelativeTimeFormatter.Format(DetectedAt);

    public void Normalize()
    {
        InstanceId = InstanceId?.Trim() ?? string.Empty;
        InstanceDisplayName = string.IsNullOrWhiteSpace(InstanceDisplayName)
            ? "Google Business"
            : InstanceDisplayName.Trim();
        ReviewId = ReviewId?.Trim() ?? string.Empty;
        ReviewerName = string.IsNullOrWhiteSpace(ReviewerName) ? "Customer" : ReviewerName.Trim();
        Snippet = Snippet?.Trim() ?? string.Empty;
        LocationLabel = string.IsNullOrWhiteSpace(LocationLabel)
            ? InstanceDisplayName
            : LocationLabel.Trim();
        Rating = Math.Clamp(Rating, 0, 5);

        if (string.IsNullOrWhiteSpace(Id) &&
            !string.IsNullOrWhiteSpace(InstanceId) &&
            !string.IsNullOrWhiteSpace(ReviewId))
        {
            Id = $"{InstanceId}:{ReviewId}";
        }
    }
}
