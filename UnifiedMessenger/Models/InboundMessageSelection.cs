namespace UnifiedMessenger.Models;

public sealed class InboundMessageSelection
{
    public required string InstanceId { get; init; }

    public required string Platform { get; init; }

    public required string MessageText { get; init; }

    public string CustomerName { get; init; } = "Customer";

    public string ConversationHint { get; init; } = string.Empty;

    /// <summary>Canonical thread key (JID, review:id, header title). Preferred over <see cref="ConversationHint"/>.</summary>
    public string ConversationKey { get; init; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
