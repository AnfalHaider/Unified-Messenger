using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class WhatsAppOperationalContextBuilder
{
    public static bool IsWhatsAppPlatform(string? platform) =>
        PlatformDefinition.NormalizePlatformId(platform) is "whatsapp" or "whatsappbusiness";
}
