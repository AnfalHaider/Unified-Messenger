namespace UnifiedMessenger.Models;

/// <summary>
/// Why the personal overview dashboard is showing an empty or sparse state.
/// </summary>
public enum PersonalDashboardEmptyReason
{
    HasData = 0,
    NoPersonalAccounts,
    AllAccountsMuted,
    NoRecentActivity
}
