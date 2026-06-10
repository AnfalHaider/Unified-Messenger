using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Adapters;
using UnifiedMessenger.Services.Backfill;

namespace UnifiedMessenger.Services.Adapters.Modules;

public sealed class GoogleBusinessAdapter : BasePlatformAdapter
{
    protected override string ScriptFileName => "google_business_scraper.js";

    protected override bool SupportsInboundAutoDraft => true;

    public override string PlatformId => "googlebusiness";

    protected override IReadOnlyList<string> AdditionalScriptFileNames => ["thread-status-auditor.js"];

    protected override bool HandleCustomMessage(
        string? type,
        JsonElement root,
        NotificationHub hub,
        MessengerInstance instance)
    {
        if (AdapterMessageTypes.DashboardScrapeStatus.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            HandleDashboardScrapeStatus(root, instance);
            return true;
        }

        switch (type)
        {
            case AdapterMessageTypes.GoogleReviewSnapshot:
                var unreplied = WebMessageParser.ReadNonNegativeInt(root, "unrepliedCount");

                ProfessionalWorkspaceService.Instance.HandleGoogleReviewSnapshot(
                    instance.Id,
                    instance.DisplayName,
                    unreplied);

                if (!instance.NotificationsMuted)
                {
                    hub.UpdateBadgeCount(instance.Id, unreplied);
                }

                return true;

            case AdapterMessageTypes.GoogleReviewAlert:
                if (instance.NotificationsMuted)
                {
                    return true;
                }

                var reviewId = root.TryGetProperty("reviewId", out var reviewIdElement)
                    ? reviewIdElement.GetString() ?? Guid.NewGuid().ToString("N")
                    : Guid.NewGuid().ToString("N");
                var reviewer = root.TryGetProperty("reviewerName", out var reviewerElement)
                    ? reviewerElement.GetString() ?? "Customer"
                    : "Customer";
                var snippet = root.TryGetProperty("snippet", out var snippetElement)
                    ? snippetElement.GetString() ?? string.Empty
                    : string.Empty;
                var location = root.TryGetProperty("locationLabel", out var locationElement)
                    ? locationElement.GetString() ?? instance.DisplayName
                    : instance.DisplayName;
                var rating = WebMessageParser.ReadNonNegativeInt(root, "rating");
                var detectedAt = ParseMessageTimestamp(root);

                ProfessionalWorkspaceService.Instance.HandleGoogleReviewAlert(
                    instance.Id,
                    instance.DisplayName,
                    reviewId,
                    reviewer,
                    snippet,
                    location,
                    rating,
                    detectedAt);

                hub.AddAlert(NotificationAlert.Create(
                    instance.Id,
                    instance.DisplayName,
                    instance.Platform,
                    $"{reviewer} · review",
                    snippet,
                    instance.IconGlyph,
                    conversationKey: ConversationKeyResolver.BuildReviewKey(reviewId),
                    customerName: reviewer));

                if (!string.IsNullOrWhiteSpace(snippet))
                {
                    var reviewConversationKey = ConversationKeyResolver.BuildReviewKey(reviewId);
                    if (!BackfillDedupeRegistry.TryAccept(
                            instance.Id,
                            instance.Platform,
                            reviewConversationKey,
                            snippet))
                    {
                        return true;
                    }

                    MessageAnalyticsService.Instance.RecordMessageReceived(
                        instance.Id,
                        reviewConversationKey,
                        detectedAt);

                    var selection = new InboundMessageSelection
                    {
                        InstanceId = instance.Id,
                        Platform = instance.Platform,
                        MessageText = snippet,
                        CustomerName = reviewer,
                        ConversationKey = reviewConversationKey,
                        ConversationHint = reviewConversationKey,
                        TimestampUtc = detectedAt
                    };

                    MessageTriageService.Instance.Enqueue(
                        selection,
                        instance.DisplayName,
                        BranchWorkspaceHelper.ResolveBranchKey(instance));
                    AutoDraftOrchestrator.Instance.HandleInboundMessage(selection);
                }

                return true;

            default:
                return false;
        }
    }
}
