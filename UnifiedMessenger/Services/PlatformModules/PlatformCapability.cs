namespace UnifiedMessenger.Services.PlatformModules;

[Flags]
public enum PlatformCapability
{
    None = 0,

    BadgeNotifications = 1 << 0,

    InboundTriage = 1 << 1,

    AutoDraft = 1 << 2,

    ThreadAuditor = 1 << 3,

    Backfill = 1 << 4,

    DashboardScrape = 1 << 5,

    OperationalIntelligence = 1 << 6,

    CustomIngress = 1 << 7
}
