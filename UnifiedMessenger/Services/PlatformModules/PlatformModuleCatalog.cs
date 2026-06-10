using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.PlatformModules;

internal static class PlatformModuleCatalog
{
    private static readonly IReadOnlyList<PlatformModuleDescriptor> Installed =
    [
        Describe("whatsapp", "WhatsApp",
            PlatformCapability.BadgeNotifications |
            PlatformCapability.InboundTriage |
            PlatformCapability.AutoDraft |
            PlatformCapability.ThreadAuditor |
            PlatformCapability.Backfill |
            PlatformCapability.CustomIngress),
        Describe("whatsappbusiness", "WhatsApp Business",
            PlatformCapability.BadgeNotifications |
            PlatformCapability.InboundTriage |
            PlatformCapability.AutoDraft |
            PlatformCapability.ThreadAuditor |
            PlatformCapability.Backfill |
            PlatformCapability.CustomIngress |
            PlatformCapability.OperationalIntelligence),
        Describe("metabusiness", "Meta Business Suite",
            PlatformCapability.BadgeNotifications |
            PlatformCapability.InboundTriage |
            PlatformCapability.AutoDraft |
            PlatformCapability.ThreadAuditor |
            PlatformCapability.Backfill |
            PlatformCapability.DashboardScrape |
            PlatformCapability.OperationalIntelligence),
        Describe("googlebusiness", "Google Business Profile",
            PlatformCapability.BadgeNotifications |
            PlatformCapability.InboundTriage |
            PlatformCapability.AutoDraft |
            PlatformCapability.ThreadAuditor |
            PlatformCapability.Backfill |
            PlatformCapability.DashboardScrape |
            PlatformCapability.OperationalIntelligence),
        Describe("telegram", "Telegram", PlatformCapability.BadgeNotifications),
        Describe("messenger", "Messenger", PlatformCapability.BadgeNotifications),
        Describe("slack", "Slack", PlatformCapability.BadgeNotifications),
        Describe("discord", "Discord", PlatformCapability.BadgeNotifications),
        Describe("signal", "Signal", PlatformCapability.BadgeNotifications),
        Describe("teams", "Microsoft Teams", PlatformCapability.BadgeNotifications),
        Describe("generic", "Custom URL", PlatformCapability.BadgeNotifications, canDisable: false)
    ];

    public static IReadOnlyList<PlatformModuleDescriptor> AllInstalled => Installed;

    public static PlatformModuleDescriptor? Find(string platformId)
    {
        var normalized = PlatformDefinition.NormalizePlatformId(platformId);
        return Installed.FirstOrDefault(module =>
            module.PlatformId.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static PlatformModuleDescriptor Describe(
        string platformId,
        string displayName,
        PlatformCapability capabilities,
        bool canDisable = true) =>
        new()
        {
            PlatformId = platformId,
            DisplayName = displayName,
            Capabilities = capabilities,
            CanDisable = canDisable,
            IsInstalled = true
        };
}
