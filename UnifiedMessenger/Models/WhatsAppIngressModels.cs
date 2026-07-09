namespace UnifiedMessenger.Models;

/// <summary>DOM-derived WhatsApp Business metadata attached to inbound messages.</summary>
public sealed class WhatsAppConversationMetadata
{
    public IReadOnlyList<string> BusinessLabels { get; init; } = [];

    public string? VerifiedBusinessName { get; init; }

    public string? ProfilePhoneNumber { get; init; }

    public string? ContactPhoneNumber { get; init; }

    public string? ChatJid { get; init; }
}

/// <summary>Persisted active-thread context from whatsapp-thread-context WebMessages.</summary>
public sealed class WhatsAppThreadContextSnapshot
{
    public required string InstanceId { get; init; }

    public required string ConversationKey { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public IReadOnlyList<string> BusinessLabels { get; init; } = [];

    public string? VerifiedBusinessName { get; init; }

    public string? ProfilePhoneNumber { get; init; }

    public string? ContactPhoneNumber { get; init; }

    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastVoiceNoteAtUtc { get; init; }
}

/// <summary>Structured active-conversation telemetry from whatsapp-telemetry WebMessages.</summary>
public sealed class WhatsAppTelemetryPayload
{
    public required string InstanceId { get; init; }

    public required string ConversationKey { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string? ContactPhoneNumber { get; init; }

    public string? ProfilePhoneNumber { get; init; }

    public DateTimeOffset? LastReceivedAtUtc { get; init; }

    public DateTimeOffset? LastSentAtUtc { get; init; }

    public InboundMessageKind LastReceivedKind { get; init; } = InboundMessageKind.Text;

    public InboundMessageKind LastSentKind { get; init; } = InboundMessageKind.Text;

    public string? ActiveMessagePreview { get; init; }

    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Outgoing manager reply delivery telemetry from WhatsApp Web ticks.</summary>
public sealed class WhatsAppOutgoingStatusEvent
{
    public required string InstanceId { get; init; }

    public required string ConversationKey { get; init; }

    /// <summary>pending | sent | delivered | read</summary>
    public required string DeliveryStatus { get; init; }

    public string? MessagePreview { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}

public static class WhatsAppDeliveryStatusLabel
{
    public const string Pending = "pending";
    public const string Sent = "sent";
    public const string Delivered = "delivered";
    public const string Read = "read";

    public static int Rank(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            Pending => 0,
            Sent => 1,
            Delivered => 2,
            Read => 3,
            _ => -1
        };

    public static bool IsKnown(string? status) => Rank(status) >= 0;

    public static string Normalize(string status) =>
        status.Trim().ToLowerInvariant();
}
