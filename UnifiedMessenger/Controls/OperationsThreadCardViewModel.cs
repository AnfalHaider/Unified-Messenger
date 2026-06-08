using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Windows.UI;

namespace UnifiedMessenger.Controls;

public sealed class OperationsThreadCardViewModel
{
    public OperationsThreadCardViewModel(ThreadData thread, bool hideBranchName = false)
    {
        InstanceId = thread.InstanceId;
        ThreadId = thread.ThreadId;
        CustomerName = thread.CustomerName;
        ConversationKey = thread.ConversationKey;
        BranchName = thread.BranchName;
        InstanceDisplayName = thread.InstanceDisplayName;
        InboxLabel = BuildInboxLabel(thread);
        PlatformGlyph = ResolvePlatformGlyph(thread.Platform);
        IntentLabel = UnifiedMessengerDashboardPresentationHelper.FormatIntentLabel(thread.AiIntentCategory);
        SentimentLabel = thread.ClientSentiment;
        NextActionSummary = string.IsNullOrWhiteSpace(thread.NextActionSummary)
            ? "Awaiting AI summary"
            : thread.NextActionSummary;
        UrgencyLabel = $"U{thread.UrgencyScore}";
        RevenueDisplay = thread.IsRevenueLeakageRisk && thread.EstimatedValue > 0
            ? $"{UnifiedMessengerDashboardPresentationHelper.FormatRevenue(thread.EstimatedValue)} at risk"
            : string.Empty;
        RevenueVisibility = thread.IsRevenueLeakageRisk && thread.EstimatedValue > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        UrgencyBrush = CreateBrush(UnifiedMessengerDashboardPresentationHelper.ResolveUrgencyHex(thread.UrgencyScore));
        SentimentBrush = CreateBrush(UnifiedMessengerDashboardPresentationHelper.ResolveSentimentHex(thread.ClientSentiment));
        CardBorderBrush = thread.IsSlaBreached || thread.IsRevenueLeakageRisk
            ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
            : new SolidColorBrush(Colors.Transparent);
        CardBorderThickness = thread.IsSlaBreached || thread.IsRevenueLeakageRisk
            ? new Thickness(thread.IsSlaBreached ? 4 : 2, 1, 1, 1)
            : new Thickness(1);
        SlaText = thread.IsSlaBreached
            ? $"Waiting {thread.LatencyMinutes:0.#}m · SLA breach"
            : string.Empty;
        SlaBrush = new SolidColorBrush(Color.FromArgb(255, 220, 38, 38));
        SlaVisibility = thread.IsSlaBreached ? Visibility.Visible : Visibility.Collapsed;
        BranchNameVisibility = hideBranchName ? Visibility.Collapsed : Visibility.Visible;
    }

    public string InstanceId { get; }

    public string ThreadId { get; }

    public string CustomerName { get; }

    public string ConversationKey { get; }

    public string BranchName { get; }

    public Visibility BranchNameVisibility { get; }

    public string InstanceDisplayName { get; }

    public string InboxLabel { get; }

    public string PlatformGlyph { get; }

    public string IntentLabel { get; }

    public string SentimentLabel { get; }

    public string NextActionSummary { get; }

    public string UrgencyLabel { get; }

    public string RevenueDisplay { get; }

    public Visibility RevenueVisibility { get; }

    public SolidColorBrush UrgencyBrush { get; }

    public SolidColorBrush SentimentBrush { get; }

    public SolidColorBrush CardBorderBrush { get; }

    public Thickness CardBorderThickness { get; }

    public string SlaText { get; }

    public SolidColorBrush SlaBrush { get; }

    public Visibility SlaVisibility { get; }

    private static string BuildInboxLabel(ThreadData thread)
    {
        var platform = PlatformDefinition.FindById(thread.Platform)?.DisplayName ?? thread.Platform;
        if (string.IsNullOrWhiteSpace(thread.InstanceDisplayName))
        {
            return platform;
        }

        return $"{platform} · {thread.InstanceDisplayName}";
    }

    private static string ResolvePlatformGlyph(string platformId) =>
        PlatformDefinition.NormalizePlatformId(platformId) switch
        {
            "whatsapp" or "whatsappbusiness" => "\uE8BD",
            "metabusiness" => "\uE717",
            "googlebusiness" => "\uE774",
            _ => "\uE774"
        };

    private static SolidColorBrush CreateBrush(string hex) =>
        new(ColorFromHex(hex));

    private static Color ColorFromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6)
        {
            return Colors.Gray;
        }

        return Color.FromArgb(
            255,
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }
}
