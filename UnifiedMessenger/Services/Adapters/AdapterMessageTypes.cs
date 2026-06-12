namespace UnifiedMessenger.Services.Adapters;

/// <summary>
/// Standard WebMessage payload contract shared by all platform adapters.
/// </summary>
public static class AdapterMessageTypes
{
    public const string BadgeCount = "badge-count";

    public const string NotificationPreview = "notification-preview";

    public const string AdapterReady = "adapter-ready";

    public const string Heartbeat = "adapter-heartbeat";

    public const string ConnectionStatus = "connection-status";

    public const string MessageSent = "message-sent";

    public const string MetaInboundMessage = "meta-inbound-message";

    public const string GoogleReviewSnapshot = "google-review-snapshot";

    public const string GoogleReviewAlert = "google-review-alert";

    public const string InboundMessageSelected = "inbound-message-selected";

    public const string DashboardScrapeStatus = "dashboard-scrape-status";

    public const string MetaTelemetrySnapshot = "meta-telemetry-snapshot";

    public const string UpdateThreadStatus = "update-thread-status";

    public const string WhatsAppThreadContext = "whatsapp-thread-context";

    public const string WhatsAppOutgoingStatus = "whatsapp-outgoing-status";

    public const string WhatsAppTelemetry = "whatsapp-telemetry";

    public const string WhatsAppVoicePayload = "whatsapp-voice-payload";

    private static readonly HashSet<string> StandardTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        BadgeCount,
        NotificationPreview,
        AdapterReady,
        Heartbeat,
        ConnectionStatus,
        MessageSent
    };

    public static bool IsStandardType(string? type) =>
        !string.IsNullOrWhiteSpace(type) && StandardTypes.Contains(type);

    public static bool IsKnownType(string? type) =>
        IsStandardType(type) ||
        MetaInboundMessage.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        GoogleReviewSnapshot.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        GoogleReviewAlert.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        InboundMessageSelected.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        DashboardScrapeStatus.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        MetaTelemetrySnapshot.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        UpdateThreadStatus.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        WhatsAppThreadContext.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        WhatsAppOutgoingStatus.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        WhatsAppTelemetry.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        WhatsAppVoicePayload.Equals(type, StringComparison.OrdinalIgnoreCase);
}
