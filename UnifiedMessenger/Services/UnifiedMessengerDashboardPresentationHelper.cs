using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Pure formatting helpers for the Unified Messenger control-center UI (unit-testable without WinUI).
/// </summary>
public static class UnifiedMessengerDashboardPresentationHelper
{
    public static string FormatRevenue(double amount) =>
        amount <= 0 ? "PKR 0" : $"PKR {amount:N0}";

    public static string FormatLatency(double minutes) =>
        minutes <= 0 ? "—" : $"{minutes:0.#}m";

    public static string FormatIntentLabel(string? intent) =>
        intent?.Trim() switch
        {
            UnifiedMessengerIntentCategory.PriceInquiry => "Price inquiry",
            UnifiedMessengerIntentCategory.Booking => "Booking",
            UnifiedMessengerIntentCategory.Complaint => "Complaint",
            UnifiedMessengerIntentCategory.Lead => "Lead",
            _ => "Inquiry"
        };

    public static string ResolveLatencyHex(string latencyColor) =>
        latencyColor switch
        {
            "Green" => "#22C55E",
            "Amber" => "#F59E0B",
            _ => "#EF4444"
        };

    public static string ResolveSentimentHex(string sentiment) =>
        sentiment switch
        {
            ClientSentimentLabel.Positive => "#22C55E",
            ClientSentimentLabel.Frustrated => "#F59E0B",
            ClientSentimentLabel.Critical => "#EF4444",
            _ => "#94A3B8"
        };

    public static string ResolveUrgencyHex(int urgencyScore) =>
        urgencyScore >= 4 ? "#DC2626" : urgencyScore >= 3 ? "#F59E0B" : "#64748B";

    public static string BuildScopeLabel(string? branchInstanceId, IReadOnlyList<string> branchNames)
    {
        if (string.IsNullOrWhiteSpace(branchInstanceId))
        {
            return branchNames.Count == 0
                ? "Showing: All branches"
                : $"Showing: {branchNames.Count} branch workspace(s)";
        }

        return "Showing: Selected branch filter";
    }

    public static IReadOnlyList<ThreadData> FilterThreadsForBranch(
        IEnumerable<ThreadData> threads,
        string? branchName) =>
        string.IsNullOrWhiteSpace(branchName)
            ? threads.OrderByDescending(thread => thread.LastMessageTime).ToList()
            : threads
                .Where(thread => thread.BranchName.Equals(branchName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(thread => thread.LastMessageTime)
                .ToList();
}
