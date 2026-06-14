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

    public const string InboundMessageSelected = "inbound-message-selected";

    public const string DashboardScrapeStatus = "dashboard-scrape-status";

    public const string UpdateThreadStatus = "update-thread-status";

    public const string WhatsAppThreadContext = "whatsapp-thread-context";

    public const string WhatsAppOutgoingStatus = "whatsapp-outgoing-status";

    public const string WhatsAppTelemetry = "whatsapp-telemetry";

    public const string WhatsAppSidebarSnapshot = "whatsapp-sidebar-snapshot";

    public const string WhatsAppHistoryChunk = "whatsapp-history-chunk";

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
        InboundMessageSelected.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        DashboardScrapeStatus.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        UpdateThreadStatus.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        WhatsAppThreadContext.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        WhatsAppOutgoingStatus.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        WhatsAppTelemetry.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        WhatsAppSidebarSnapshot.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        WhatsAppHistoryChunk.Equals(type, StringComparison.OrdinalIgnoreCase) ||
        WhatsAppVoicePayload.Equals(type, StringComparison.OrdinalIgnoreCase);
}
