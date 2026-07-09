using UnifiedMessenger.Services;

namespace UnifiedMessenger.Models;

/// <summary>
/// Operational thread rollup for the Unified Messenger control-center dashboard.
/// Persisted in <c>triage_v2.json</c> alongside message-level triage items.
/// </summary>
public sealed class ThreadData
{
    public required string ThreadId { get; set; }

    public required string Platform { get; set; }

    public required string InstanceId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public string CustomerName { get; set; } = "Customer";

    public bool IsReplied { get; set; }

    public DateTimeOffset LastMessageTime { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset FirstInboundAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public double LatencyMinutes { get; set; }

    public double ReplyLatencyMinutes { get; set; }

    public string AiIntentCategory { get; set; } = UnifiedMessengerIntentCategory.Inquiry;

    public string ClientSentiment { get; set; } = ClientSentimentLabel.Neutral;

    /// <summary>Operational urgency on a 1–5 scale for manager queues.</summary>
    public int UrgencyScore { get; set; } = 1;

    public double EstimatedValue { get; set; }

    public string NextActionSummary { get; set; } = string.Empty;

    public string LastMessagePreview { get; set; } = string.Empty;

    public TriageInferenceSource InferenceSource { get; set; } = TriageInferenceSource.Heuristic;

    public string SuggestedAction { get; set; } = string.Empty;

    public bool IsSpamOrPromo { get; set; }

    public bool IsRevenueLeakageRisk { get; set; }

    public string ConversationKey { get; set; } = string.Empty;

    public string? LastTriageItemId { get; set; }

    public string InstanceDisplayName { get; set; } = string.Empty;

    /// <summary>Latest outgoing WhatsApp delivery tick: pending | sent | delivered | read.</summary>
    public string WhatsAppDeliveryStatus { get; set; } = string.Empty;

    public DateTimeOffset WhatsAppDeliveryUpdatedUtc { get; set; }

    public string LastMessageKind { get; set; } = nameof(InboundMessageKind.Text);

    public bool HasUnreadVoiceNote { get; set; }

    /// <summary>
    /// True when this thread was reconstructed from historical message backfill rather than
    /// observed live after connect. Backfilled threads are excluded from SLA timing because their
    /// original inbound timestamps predate this session and may already have been answered on the
    /// phone — counting them as breaches saturates the SLA metric (see UI/UX audit, finding F1/Q4).
    /// </summary>
    public bool IsBackfilled { get; set; }

    public bool IsSlaBreached =>
        !IsSpamOrPromo &&
        !IsReplied &&
        !IsBackfilled &&
        LatencyMinutes > OperationalThresholds.GetSlaThresholdMinutes(BranchName);

    /// <summary>
    /// Approaching the SLA threshold (≥50% of the budget) but not yet breached. Gives the operator a
    /// warning window before a technical breach, per SLA-dashboard best practice.
    /// </summary>
    public bool IsSlaAtRisk =>
        !IsSpamOrPromo &&
        !IsReplied &&
        !IsBackfilled &&
        !IsSlaBreached &&
        LatencyMinutes >= OperationalThresholds.GetSlaThresholdMinutes(BranchName) * 0.5;

    /// <summary>Open thread carried over from history (not spam, not replied, backfilled).</summary>
    public bool IsHistoricalOpen =>
        !IsSpamOrPromo &&
        !IsReplied &&
        IsBackfilled;

    /// <summary>
    /// High-urgency threads (urgency or critical sentiment) excluding SLA-only breaches.
    /// Used for the Urgent KPI and filter chip.
    /// </summary>
    public bool IsUrgent =>
        !IsSpamOrPromo &&
        !IsReplied &&
        (UrgencyScore >= 4 ||
         ClientSentiment.Equals(ClientSentimentLabel.Critical, StringComparison.OrdinalIgnoreCase));

    public bool IsImmediateAction =>
        !IsSpamOrPromo &&
        !IsReplied &&
        (IsSlaBreached || IsUrgent);

    public UnifiedMessengerKanbanColumn KanbanColumn =>
        IsReplied || IsSpamOrPromo
            ? UnifiedMessengerKanbanColumn.Resolved
            : IsRevenueLeakageRisk
                ? UnifiedMessengerKanbanColumn.HangingLeads
                : UnifiedMessengerKanbanColumn.NewInquiries;
}

public enum UnifiedMessengerKanbanColumn
{
    NewInquiries,
    HangingLeads,
    Resolved
}

public static class UnifiedMessengerIntentCategory
{
    public const string Booking = "Booking";
    public const string Complaint = "Complaint";
    public const string PriceInquiry = "Price_Inquiry";
    public const string Lead = "Lead";
    public const string Inquiry = "Inquiry";
    public const string Spam = "Spam";
}

public static class ClientSentimentLabel
{
    public const string Positive = "Positive";
    public const string Neutral = "Neutral";
    public const string Frustrated = "Frustrated";
    public const string Critical = "Critical";
}
