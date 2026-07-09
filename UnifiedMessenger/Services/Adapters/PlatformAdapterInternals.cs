using UnifiedMessenger.Services.Adapters.Modules;

namespace UnifiedMessenger.Services.Adapters;

internal static class PlatformAdapterInternals
{
    private static readonly WhatsAppAdapter WhatsApp = new();
    private static readonly WhatsAppBusinessAdapter WhatsAppBusiness = new();
    private static readonly NullPlatformAdapter Unsupported = new();

    public static IPlatformAdapter ResolveEnabledAdapter(string normalizedPlatformId) =>
        normalizedPlatformId switch
        {
            "whatsapp" => WhatsApp,
            "whatsappbusiness" => WhatsAppBusiness,
            _ => Unsupported
        };
}
