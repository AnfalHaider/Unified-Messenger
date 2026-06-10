using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public static class WhatsAppDeliveryStatusPresentation
{
    public static bool ShouldShow(string platform, string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return PlatformModuleSettingsHelper.IsPlatformModuleEnabled(platform) &&
               PlatformDefinition.NormalizePlatformId(platform) is "whatsapp" or "whatsappbusiness";
    }

    public static string ResolveLabel(string status) =>
        WhatsAppDeliveryStatusLabel.Normalize(status) switch
        {
            WhatsAppDeliveryStatusLabel.Pending => "Sending…",
            WhatsAppDeliveryStatusLabel.Sent => "Sent ✓",
            WhatsAppDeliveryStatusLabel.Delivered => "Delivered ✓✓",
            WhatsAppDeliveryStatusLabel.Read => "Read ✓✓",
            _ => string.Empty
        };

    public static string ResolveBrushHex(string status) =>
        WhatsAppDeliveryStatusLabel.Normalize(status) switch
        {
            WhatsAppDeliveryStatusLabel.Read => "#2563EB",
            WhatsAppDeliveryStatusLabel.Delivered => "#64748B",
            WhatsAppDeliveryStatusLabel.Sent => "#94A3B8",
            WhatsAppDeliveryStatusLabel.Pending => "#94A3B8",
            _ => "#00000000"
        };
}
