namespace UnifiedMessenger.Models;

/// <summary>
/// Why a professional dashboard card is showing an empty or placeholder state.
/// </summary>
public enum DashboardCardEmptyReason
{
    HasData = 0,
    NoPlatformInstance,
    ConnectedAwaitingScrape,
    ConnectedNoData,
    InboundOnlyAwaitingReply,
    NoTriageItems,
    OnlyLowUrgencyItems
}
