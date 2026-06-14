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
            WhatsAppDeliveryStatusLabel.Read => UmSemanticColors.StatusRead,
            WhatsAppDeliveryStatusLabel.Delivered => UmSemanticColors.StatusDelivered,
            WhatsAppDeliveryStatusLabel.Sent => UmSemanticColors.StatusSent,
            WhatsAppDeliveryStatusLabel.Pending => UmSemanticColors.StatusPending,
            _ => UmSemanticColors.Transparent
        };

    public static string ResolveBrushKey(string status) =>
        WhatsAppDeliveryStatusLabel.Normalize(status) switch
        {
            WhatsAppDeliveryStatusLabel.Read => "UmStatusReadBrush",
            WhatsAppDeliveryStatusLabel.Delivered => "UmStatusDeliveredBrush",
            WhatsAppDeliveryStatusLabel.Sent => "UmStatusSentBrush",
            WhatsAppDeliveryStatusLabel.Pending => "UmStatusPendingBrush",
            _ => UmSemanticBrushes.TransparentBrushKey
        };
}
