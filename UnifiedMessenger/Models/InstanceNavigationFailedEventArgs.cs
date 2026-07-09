namespace UnifiedMessenger.Models;

public sealed class InstanceNavigationFailedEventArgs : EventArgs
{
    public required string InstanceId { get; init; }

    public string? ConversationKey { get; init; }

    public required string Message { get; init; }
}
