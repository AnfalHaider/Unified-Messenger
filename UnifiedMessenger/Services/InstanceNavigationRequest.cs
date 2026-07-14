namespace UnifiedMessenger.Services;

public sealed class InstanceNavigationRequest
{
    public required string InstanceId { get; init; }

    public string? ConversationKey { get; init; }

    public string? CustomerName { get; init; }

    /// <summary>Resolved phone digits (from the lid→phone map for @lid privacy chats) — what WhatsApp search
    /// matches and what the row shows, so focus can locate an @lid chat whose JID isn't a real number.</summary>
    public string? ContactPhone { get; init; }

    public bool HasConversationTarget =>
        !string.IsNullOrWhiteSpace(ConversationKey) ||
        !string.IsNullOrWhiteSpace(CustomerName);
}
