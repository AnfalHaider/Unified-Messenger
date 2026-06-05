namespace UnifiedMessenger.Models;

/// <summary>
/// Canonical platform identifiers for Unified Messenger analytics and routing.
/// </summary>
public enum PlatformKind
{
    Meta,
    Google,
    WhatsApp,
    WhatsAppBusiness,
    Generic
}

public static class PlatformKindExtensions
{
    public static string ToPlatformId(this PlatformKind kind) =>
        kind switch
        {
            PlatformKind.Meta => "metabusiness",
            PlatformKind.Google => "googlebusiness",
            PlatformKind.WhatsApp => "whatsapp",
            PlatformKind.WhatsAppBusiness => "whatsappbusiness",
            _ => "generic"
        };

    public static PlatformKind FromPlatformId(string? platformId)
    {
        var normalized = PlatformDefinition.NormalizePlatformId(platformId);
        return normalized switch
        {
            "metabusiness" => PlatformKind.Meta,
            "googlebusiness" => PlatformKind.Google,
            "whatsapp" => PlatformKind.WhatsApp,
            "whatsappbusiness" => PlatformKind.WhatsAppBusiness,
            _ => PlatformKind.Generic
        };
    }
}
