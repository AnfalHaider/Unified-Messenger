using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Windows.UI;

namespace UnifiedMessenger.Controls.Occ;

public sealed class OperationsInsightFeedViewModel
{
    public OperationsInsightFeedViewModel(OperationsInsightFeedItem item)
    {
        CustomerName = item.CustomerName;
        BranchName = item.BranchName;
        Summary = item.Summary;
        SourceLabel = item.SourceLabel;
        IntentLabel = item.IntentLabel;
        UrgencyLabel = item.UrgencyLabel;
        InstanceId = item.InstanceId;
        ConversationKey = string.IsNullOrWhiteSpace(item.Thread?.ConversationKey)
            ? null
            : item.Thread!.ConversationKey;
        IntentLabelVisibility = string.IsNullOrWhiteSpace(item.IntentLabel)
            ? Visibility.Collapsed
            : Visibility.Visible;
        UrgencyLabelVisibility = string.IsNullOrWhiteSpace(item.UrgencyLabel)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public string CustomerName { get; }

    public string BranchName { get; }

    public string Summary { get; }

    public string SourceLabel { get; }

    public string IntentLabel { get; }

    public string UrgencyLabel { get; }

    public string? InstanceId { get; }

    public string? ConversationKey { get; }

    public Visibility IntentLabelVisibility { get; }

    public Visibility UrgencyLabelVisibility { get; }
}

public sealed class BranchMetricViewModel
{
    private const double DimmedCardOpacity = 0.55;

    public BranchMetricViewModel(UnifiedMessengerBranchMetrics metric, bool isSelected, bool isWorkspaceScoped)
    {
        Source = metric;
        BranchName = metric.BranchName;
        LatencyDisplay = UnifiedMessengerDashboardPresentationHelper.FormatLatency(metric.AverageLatencyMinutes);
        UnresolvedDisplay = metric.UnresolvedCount == 1
            ? "1 open"
            : $"{metric.UnresolvedCount} open";
        InboxDisplay = metric.InboxCount <= 1
            ? "1 inbox"
            : $"{metric.InboxCount} inboxes";
        PlatformBreakdown = metric.PlatformBreakdown;
        DetailDisplay = BuildDetailDisplay(metric);
        IsSelected = isSelected;
        IsWorkspaceScoped = isWorkspaceScoped;
        LatencyBrush = OccBrushHelper.CreateBrush(
            UnifiedMessengerDashboardPresentationHelper.ResolveLatencyHex(metric.LatencyColor));
        LatencyBorderBrush = OccBrushHelper.CreateBrush(
            UnifiedMessengerDashboardPresentationHelper.ResolveLatencyHex(metric.LatencyColor));
        CardBackgroundBrush = isSelected
            ? OccBrushHelper.ResolveThemeBrush("LayerFillColorAltBrush", Color.FromArgb(255, 239, 246, 255))
            : OccBrushHelper.ResolveThemeBrush("CardBackgroundFillColorDefaultBrush", Colors.Transparent);
        CardOpacity = isWorkspaceScoped && !isSelected ? DimmedCardOpacity : 1.0;
        CardBorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
        ToolTipText = isSelected ? "Currently scoped to this branch" : "Select branch tab";
        ScopeHintText = isSelected ? "Scoped workspace" : "Select branch tab";
        ScopeHintVisibility = Visibility.Visible;
    }

    public UnifiedMessengerBranchMetrics Source { get; }

    public string BranchName { get; }

    public string LatencyDisplay { get; }

    public string UnresolvedDisplay { get; }

    public string InboxDisplay { get; }

    public string PlatformBreakdown { get; }

    public string DetailDisplay { get; }

    public bool IsSelected { get; }

    public bool IsWorkspaceScoped { get; }

    public SolidColorBrush LatencyBrush { get; }

    public SolidColorBrush LatencyBorderBrush { get; }

    public SolidColorBrush CardBackgroundBrush { get; }

    public double CardOpacity { get; }

    public Thickness CardBorderThickness { get; }

    public string ToolTipText { get; }

    public string ScopeHintText { get; }

    public Visibility ScopeHintVisibility { get; }

    public Visibility PlatformBreakdownVisibility =>
        string.IsNullOrWhiteSpace(PlatformBreakdown) ? Visibility.Collapsed : Visibility.Visible;

    private static string BuildDetailDisplay(UnifiedMessengerBranchMetrics metric)
    {
        var parts = new List<string> { metric.UnresolvedCount == 1 ? "1 open" : $"{metric.UnresolvedCount} open" };
        if (metric.InboxCount > 0)
        {
            parts.Add(metric.InboxCount == 1 ? "1 inbox" : $"{metric.InboxCount} inboxes");
        }

        if (metric.SlaBreachCount > 0)
        {
            parts.Add($"{metric.SlaBreachCount} SLA");
        }

        if (metric.RevenueAtRisk > 0)
        {
            parts.Add(UnifiedMessengerDashboardPresentationHelper.FormatRevenue(metric.RevenueAtRisk));
        }

        return string.Join(" · ", parts);
    }
}

public sealed class PlatformHealthViewModel
{
    public PlatformHealthViewModel(UnifiedMessengerPlatformHealthIndicator indicator)
    {
        Label = $"{indicator.DisplayName}: {indicator.StatusText}";
        StatusBrush = new SolidColorBrush(indicator.IsSynced
            ? Color.FromArgb(255, 34, 197, 94)
            : Color.FromArgb(255, 239, 68, 68));
    }

    public string Label { get; }

    public SolidColorBrush StatusBrush { get; }
}

public sealed class HealthChipViewModel
{
    public HealthChipViewModel(DashboardInstanceHealthChip chip)
    {
        Summary = string.IsNullOrWhiteSpace(chip.BackfillSummary)
            ? $"{chip.DisplayName}: backfill {chip.BackfillState}, {chip.AdapterHealth}, {chip.TriageItemCount} triage"
            : $"{chip.DisplayName}: backfill {chip.BackfillState}, {chip.AdapterHealth}, {chip.TriageItemCount} triage · {chip.BackfillSummary}";
    }

    public string Summary { get; }
}

public sealed class OperationalHighlightViewModel
{
    public OperationalHighlightViewModel(OperationalHighlightItem item)
    {
        Title = item.Title;
        Subtitle = item.Subtitle;
        InstanceDisplayName = item.InstanceDisplayName;
        InstanceId = item.InstanceId;
        ConversationKey = string.IsNullOrWhiteSpace(item.ConversationKey)
            ? null
            : item.ConversationKey;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string InstanceDisplayName { get; }

    public string? InstanceId { get; }

    public string? ConversationKey { get; }
}

public sealed class GoogleReviewAlertView
{
    public GoogleReviewAlertView(GoogleReviewAlert alert)
    {
        AlertId = alert.Id;
        InstanceId = alert.InstanceId;
        ReviewId = alert.ReviewId;
        ReviewerName = alert.ReviewerName;
        Snippet = alert.Snippet;
    }

    public string AlertId { get; }

    public string InstanceId { get; }

    public string ReviewId { get; }

    public string ReviewerName { get; }

    public string Snippet { get; }
}
