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

    public string? ConversationKey { get; init; }

    public string? CustomerName { get; init; }

    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool IsRead { get; set; }

    public bool HasConversationTarget =>
        !string.IsNullOrWhiteSpace(ConversationKey) ||
        !string.IsNullOrWhiteSpace(CustomerName);

    public string RelativeTimeText => RelativeTimeFormatter.Format(ReceivedAt);

    public static NotificationAlert Create(
        string instanceId,
        string instanceDisplayName,
        string platform,
        string title,
        string? body = null,
        string? iconGlyph = null,
        string? id = null,
        string? conversationKey = null,
        string? customerName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var displayName = string.IsNullOrWhiteSpace(instanceDisplayName)
            ? "Account"
            : instanceDisplayName.Trim();

        return new NotificationAlert
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id.Trim(),
            InstanceId = instanceId.Trim(),
            InstanceDisplayName = displayName,
            Platform = PlatformDefinition.NormalizePlatformId(platform),
            IconGlyph = string.IsNullOrWhiteSpace(iconGlyph) ? "\uE8BD" : iconGlyph,
            Title = string.IsNullOrWhiteSpace(title) ? displayName : title.Trim(),
            Body = body?.Trim() ?? string.Empty,
            ConversationKey = string.IsNullOrWhiteSpace(conversationKey) ? null : conversationKey.Trim(),
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim()
        };
    }
}
