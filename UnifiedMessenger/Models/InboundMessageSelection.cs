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

    /// <summary>WhatsApp Business sidebar labels (e.g. VIP, Booking Pending).</summary>
    public IReadOnlyList<string> BusinessLabels { get; init; } = [];

    public string? VerifiedBusinessName { get; init; }

    public string? ProfilePhoneNumber { get; init; }

    public string? ContactPhoneNumber { get; init; }

    public InboundMessageKind MessageKind { get; init; } = InboundMessageKind.Text;

    public double VoiceDurationSeconds { get; init; }

    public double TranscriptConfidence { get; init; }

    public WhatsAppConversationMetadata ToWhatsAppMetadata() =>
        new()
        {
            BusinessLabels = BusinessLabels,
            VerifiedBusinessName = VerifiedBusinessName,
            ProfilePhoneNumber = ProfilePhoneNumber,
            ContactPhoneNumber = ContactPhoneNumber,
            ChatJid = string.IsNullOrWhiteSpace(ConversationKey) ? null : ConversationKey
        };
}
