namespace UnifiedMessenger.Models;

public sealed class WhatsAppVoiceNotePayload
{
    public required string InstanceId { get; init; }

    public required string Platform { get; init; }

    public required string ConversationKey { get; init; }

    public string CustomerName { get; init; } = "Customer";

    public double DurationSeconds { get; init; }

    public string MimeType { get; init; } = "audio/ogg";

    public required string AudioBase64 { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<string> BusinessLabels { get; init; } = [];

    public string? VerifiedBusinessName { get; init; }

    public string? ProfilePhoneNumber { get; init; }

    public string? ContactPhoneNumber { get; init; }
}

public sealed class SpeechTranscriptionResult
{
    public required string Text { get; init; }

    public double Confidence { get; init; } = 0.75;

    public string Language { get; init; } = "auto";
}
