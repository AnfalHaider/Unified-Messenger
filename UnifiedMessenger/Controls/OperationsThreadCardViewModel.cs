using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Windows.UI;

namespace UnifiedMessenger.Controls;

public sealed class OperationsThreadCardViewModel
{
    public OperationsThreadCardViewModel(
        ThreadData thread,
        bool hideBranchName = false,
        TriageInferenceSource? inferenceSourceOverride = null,
        bool compactDensity = false)
    {
        var inferenceSource = inferenceSourceOverride ?? thread.InferenceSource;
        InstanceId = thread.InstanceId;
        ThreadId = thread.ThreadId;
        CustomerName = thread.CustomerName;
        ConversationKey = thread.ConversationKey;
        BranchName = thread.BranchName;
        InstanceDisplayName = thread.InstanceDisplayName;
        MessagePreview = OperationsThreadCardPresentationHelper.BuildMessagePreview(thread);
        OpsHint = OperationsThreadCardPresentationHelper.BuildOpsHint(thread);
        var opsHintDistinct = !string.IsNullOrWhiteSpace(OpsHint) &&
                              !OpsHint.Equals(MessagePreview, StringComparison.OrdinalIgnoreCase) &&
                              !OpsHint.Contains(MessagePreview, StringComparison.OrdinalIgnoreCase);
        OpsHintVisibility = opsHintDistinct ? Visibility.Visible : Visibility.Collapsed;
        RelativeTime = OperationsThreadCardPresentationHelper.BuildRelativeTime(thread.LastMessageTime);
        MetadataRow = BuildMetadataRow(thread, hideBranchName);
        InboxLabel = BuildInboxLabel(thread);
        PlatformGlyph = ResolvePlatformGlyph(thread.Platform);
        IntentLabel = UnifiedMessengerDashboardPresentationHelper.FormatIntentLabel(thread.AiIntentCategory);
        SentimentLabel = thread.ClientSentiment;
        InferenceSourceLabel = TriageInferenceLabelFormatter.Format(inferenceSource);
        ShowInferenceProgress = TriageInferenceLabelFormatter.IsActiveJob(inferenceSource);
        InferenceProgressVisibility = ShowInferenceProgress ? Visibility.Visible : Visibility.Collapsed;
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
        var slaThreshold = OperationalThresholds.GetSlaThresholdMinutes();
        SlaText = thread.IsReplied || thread.IsSpamOrPromo
            ? string.Empty
            : UnifiedMessengerDashboardPresentationHelper.FormatSlaCountdown(
                thread.LatencyMinutes,
                slaThreshold);
        SlaBrush = thread.IsSlaBreached
            ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
            : thread.LatencyMinutes >= slaThreshold * 0.75
                ? new SolidColorBrush(Color.FromArgb(255, 245, 158, 11))
                : new SolidColorBrush(Color.FromArgb(255, 100, 116, 139));
        SlaVisibility = string.IsNullOrWhiteSpace(SlaText) ? Visibility.Collapsed : Visibility.Visible;
        BranchNameVisibility = hideBranchName ? Visibility.Collapsed : Visibility.Visible;
        DeliveryStatusLabel = WhatsAppDeliveryStatusPresentation.ResolveLabel(thread.WhatsAppDeliveryStatus);
        DeliveryStatusBrush = CreateBrush(
            WhatsAppDeliveryStatusPresentation.ResolveBrushHex(thread.WhatsAppDeliveryStatus));
        DeliveryStatusVisibility = WhatsAppDeliveryStatusPresentation.ShouldShow(
            thread.Platform,
            thread.WhatsAppDeliveryStatus)
            ? Visibility.Visible
            : Visibility.Collapsed;
        LastMessageTimeUtc = thread.LastMessageTime;
        LastMessageKind = thread.LastMessageKind;
        CompactDensity = compactDensity;
    }

    public bool CompactDensity { get; }

    public DateTimeOffset LastMessageTimeUtc { get; }

    public string LastMessageKind { get; }

    public string InstanceId { get; }

    public string ThreadId { get; }

    public string CustomerName { get; }

    public string ConversationKey { get; }

    public string BranchName { get; }

    public Visibility BranchNameVisibility { get; }

    public string InstanceDisplayName { get; }

    public string MessagePreview { get; }

    public string OpsHint { get; }

    public Visibility OpsHintVisibility { get; }

    public string RelativeTime { get; }

    public string MetadataRow { get; }

    public string InboxLabel { get; }

    public string PlatformGlyph { get; }

    public string IntentLabel { get; }

    public string SentimentLabel { get; }

    public string InferenceSourceLabel { get; }

    public bool ShowInferenceProgress { get; }

    public Visibility InferenceProgressVisibility { get; }

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

    public string DeliveryStatusLabel { get; }

    public SolidColorBrush DeliveryStatusBrush { get; }

    public Visibility DeliveryStatusVisibility { get; }

    private static string BuildMetadataRow(ThreadData thread, bool hideBranchName)
    {
        var platform = PlatformDefinition.FindById(thread.Platform)?.DisplayName ?? thread.Platform;
        var parts = new List<string> { platform };

        if (!string.IsNullOrWhiteSpace(thread.InstanceDisplayName))
        {
            parts.Add(thread.InstanceDisplayName);
        }

        if (!hideBranchName && !string.IsNullOrWhiteSpace(thread.BranchName))
        {
            parts.Add(thread.BranchName.Trim());
        }

        parts.Add(OperationsThreadCardPresentationHelper.BuildRelativeTime(thread.LastMessageTime));
        return string.Join(" · ", parts);
    }

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
