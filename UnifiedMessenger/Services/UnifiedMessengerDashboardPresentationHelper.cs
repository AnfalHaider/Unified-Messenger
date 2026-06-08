using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Pure formatting helpers for the Operations Command Center UI (unit-testable without WinUI).
/// </summary>
public static class UnifiedMessengerDashboardPresentationHelper
{
    public static string FormatRevenue(double amount) =>
        amount <= 0 ? "PKR 0" : $"PKR {amount:N0}";

    public static string FormatLatency(double minutes) =>
        minutes <= 0 ? "—" : $"{minutes:0.#}m";

    /// <summary>
    /// Human-readable waiting duration for SLA and thread cards (e.g. 90m, 24h, 3d 2h).
    /// </summary>
    public static string FormatWaitingDuration(double minutes)
    {
        if (minutes <= 0)
        {
            return "—";
        }

        if (minutes < 60)
        {
            return $"{minutes:0.#}m";
        }

        if (minutes < 24 * 60)
        {
            var hours = minutes / 60d;
            return hours < 10 ? $"{hours:0.#}h" : $"{Math.Round(hours)}h";
        }

        var days = (int)(minutes / (24 * 60));
        var remainingHours = (int)Math.Round((minutes % (24 * 60)) / 60d);
        return remainingHours > 0 ? $"{days}d {remainingHours}h" : $"{days}d";
    }

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
}
