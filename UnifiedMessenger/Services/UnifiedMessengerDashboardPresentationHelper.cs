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

    public static string FormatSlaCountdown(double latencyMinutes, double slaThresholdMinutes)
    {
        if (latencyMinutes <= 0 || slaThresholdMinutes <= 0)
        {
            return string.Empty;
        }

        var remaining = slaThresholdMinutes - latencyMinutes;
        if (remaining <= 0)
        {
            return $"Waiting {FormatWaitingDuration(latencyMinutes)} · SLA breach";
        }

        return $"Waiting {FormatWaitingDuration(latencyMinutes)} · {FormatWaitingDuration(remaining)} to SLA";
    }

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
            "Green" => UmSemanticColors.StatusSuccess,
            "Amber" => UmSemanticColors.StatusWarning,
            _ => UmSemanticColors.StatusDanger
        };

    public static string ResolveSentimentHex(string sentiment) =>
        sentiment switch
        {
            ClientSentimentLabel.Positive => UmSemanticColors.StatusSuccess,
            ClientSentimentLabel.Frustrated => UmSemanticColors.StatusWarning,
            ClientSentimentLabel.Critical => UmSemanticColors.StatusDanger,
            _ => UmSemanticColors.StatusMuted
        };

    public static string ResolveUrgencyHex(int urgencyScore) =>
        urgencyScore >= 4
            ? UmSemanticColors.StatusDanger
            : urgencyScore >= 3
                ? UmSemanticColors.StatusWarning
                : UmSemanticColors.StatusNeutral;

    public static string ResolveUrgencyBrushKey(int urgencyScore) =>
        urgencyScore >= 4
            ? UmSemanticBrushes.StatusDangerBrushKey
            : urgencyScore >= 3
                ? UmSemanticBrushes.StatusWarningBrushKey
                : UmSemanticBrushes.StatusNeutralBrushKey;

    public static string ResolveSentimentBrushKey(string sentiment) =>
        sentiment switch
        {
            ClientSentimentLabel.Positive => UmSemanticBrushes.StatusSuccessBrushKey,
            ClientSentimentLabel.Frustrated => UmSemanticBrushes.StatusWarningBrushKey,
            ClientSentimentLabel.Critical => UmSemanticBrushes.StatusDangerBrushKey,
            _ => UmSemanticBrushes.StatusMutedBrushKey
        };
}
