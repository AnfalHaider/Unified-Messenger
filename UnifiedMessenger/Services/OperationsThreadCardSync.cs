using UnifiedMessenger.Controls;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

internal static class OperationsThreadCardSync
{
    public static string BuildSyncKey(ThreadData thread) =>
        string.Join(
            '|',
            thread.ThreadId,
            thread.LastMessageTime.UtcTicks,
            thread.IsReplied,
            thread.UrgencyScore,
            thread.IsSlaBreached,
            thread.IsRevenueLeakageRisk,
            thread.EstimatedValue,
            thread.NextActionSummary ?? string.Empty);

    public static bool ContentEquals(OperationsThreadCardViewModel existing, OperationsThreadCardViewModel incoming) =>
        existing.ThreadId.Equals(incoming.ThreadId, StringComparison.Ordinal) &&
        existing.CustomerName == incoming.CustomerName &&
        existing.NextActionSummary == incoming.NextActionSummary &&
        existing.UrgencyLabel == incoming.UrgencyLabel &&
        existing.SlaText == incoming.SlaText &&
        existing.RevenueDisplay == incoming.RevenueDisplay &&
        existing.SentimentLabel == incoming.SentimentLabel &&
        existing.IntentLabel == incoming.IntentLabel &&
        existing.BranchNameVisibility == incoming.BranchNameVisibility;
}
