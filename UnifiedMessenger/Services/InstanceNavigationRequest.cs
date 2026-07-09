namespace UnifiedMessenger.Services;

public sealed class InstanceNavigationRequest
{
    public required string InstanceId { get; init; }

    public string? ConversationKey { get; init; }

    public string? CustomerName { get; init; }

    public bool HasConversationTarget =>
        !string.IsNullOrWhiteSpace(ConversationKey) ||
        !string.IsNullOrWhiteSpace(CustomerName);
}
