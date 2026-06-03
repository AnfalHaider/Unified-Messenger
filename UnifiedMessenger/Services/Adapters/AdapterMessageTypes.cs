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

    public const string MessageSent = "message-sent";
}
