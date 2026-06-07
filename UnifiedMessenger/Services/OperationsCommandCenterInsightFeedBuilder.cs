using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Builds a deduped AI insight feed for the Operations Command Center.
/// Thread-level next actions take precedence over message-level executive insight cards.
/// </summary>
public static class OperationsCommandCenterInsightFeedBuilder
{
    public const int MaxFeedItems = 24;

    public static IReadOnlyList<OperationsInsightFeedItem> Build(
        UnifiedMessengerDashboardSnapshot threadOperations,
        IEnumerable<MessageTriageItem> triageItems,
        bool includeHeuristicInsights = true)
    {
        ArgumentNullException.ThrowIfNull(threadOperations);
        ArgumentNullException.ThrowIfNull(triageItems);

        var items = new List<OperationsInsightFeedItem>();
        var coveredConversationKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var thread in threadOperations.ImmediateActionQueue)
        {
            items.Add(CreateThreadItem(thread, basePriority: 1000));
            coveredConversationKeys.Add(BuildConversationKey(thread.InstanceId, thread.ConversationKey, thread.CustomerName));
        }

        foreach (var thread in threadOperations.AllThreads)
        {
            if (thread.IsReplied ||
                thread.IsSpamOrPromo ||
                thread.KanbanColumn != UnifiedMessengerKanbanColumn.HangingLeads)
            {
                continue;
            }

            var conversationKey = BuildConversationKey(thread.InstanceId, thread.ConversationKey, thread.CustomerName);
            if (coveredConversationKeys.Contains(conversationKey))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(thread.NextActionSummary))
            {
                continue;
            }

            items.Add(CreateThreadItem(thread, basePriority: 500));
            coveredConversationKeys.Add(conversationKey);
        }

        var orderedTriage = triageItems
            .OrderByDescending(item => item.UrgencyScore)
            .ThenByDescending(item => item.TimestampUtc)
            .ToList();

        foreach (var triageItem in orderedTriage.Where(DashboardPageHelper.HasExecutiveInsightContent))
        {
            if (triageItem.IsSpamOrPromo ||
                ShouldSkipExecutiveInsight(triageItem, threadOperations, coveredConversationKeys))
            {
                continue;
            }

            var card = DashboardPageHelper.BuildExecutiveInsightCard(
                triageItem,
                triageItem.InferenceSource == TriageInferenceSource.LocalAi ? "Local AI" : "Rich");

            items.Add(new OperationsInsightFeedItem
            {
                Kind = OperationsInsightFeedKind.ExecutiveInsight,
                DedupeKey = $"triage:{triageItem.Id}",
                CustomerName = card.CustomerName,
                BranchName = card.BranchName,
                Summary = card.CoreSummary,
                InstanceId = triageItem.InstanceId,
                TriageItemId = triageItem.Id,
                IntentLabel = card.IntentLabel,
                UrgencyLabel = card.UrgencyLabel,
                SourceLabel = card.SourceLabel,
                PriorityScore = 200 + triageItem.UrgencyScore,
                ExecutiveCard = card
            });

            if (items.Count >= MaxFeedItems)
            {
                return SortAndTrim(items);
            }
        }

        if (includeHeuristicInsights)
        {
            foreach (var triageItem in orderedTriage.Where(item => !DashboardPageHelper.HasExecutiveInsightContent(item)))
            {
                if (triageItem.IsSpamOrPromo ||
                    ShouldSkipExecutiveInsight(triageItem, threadOperations, coveredConversationKeys))
                {
                    continue;
                }

                var card = DashboardPageHelper.BuildHeuristicInsightCard(triageItem);
                items.Add(new OperationsInsightFeedItem
                {
                    Kind = OperationsInsightFeedKind.ExecutiveInsight,
                    DedupeKey = $"triage:{triageItem.Id}",
                    CustomerName = card.CustomerName,
                    BranchName = card.BranchName,
                    Summary = card.CoreSummary,
                    InstanceId = triageItem.InstanceId,
                    TriageItemId = triageItem.Id,
                    IntentLabel = card.IntentLabel,
                    UrgencyLabel = card.UrgencyLabel,
                    SourceLabel = card.SourceLabel,
                    PriorityScore = 100 + triageItem.UrgencyScore,
                    ExecutiveCard = card
                });

                if (items.Count >= MaxFeedItems)
                {
                    break;
                }
            }
        }

        return SortAndTrim(items);
    }

    internal static string BuildConversationKey(
        string instanceId,
        string? conversationKey,
        string? customerName = null) =>
        ConversationKeyResolver.BuildThreadId(
            instanceId.Trim(),
            ConversationKeyResolver.Resolve(
                platform: string.Empty,
                conversationKey,
                conversationHint: conversationKey,
                customerName));

    private static bool ShouldSkipExecutiveInsight(
        MessageTriageItem triageItem,
        UnifiedMessengerDashboardSnapshot threadOperations,
        IReadOnlySet<string> coveredConversationKeys)
    {
        var conversationKey = BuildConversationKey(
            triageItem.InstanceId,
            triageItem.ConversationKey,
            ResolveCustomerName(triageItem));
        if (!coveredConversationKeys.Contains(conversationKey))
        {
            return false;
        }

        var thread = threadOperations.AllThreads.FirstOrDefault(candidate =>
            candidate.InstanceId.Equals(triageItem.InstanceId, StringComparison.OrdinalIgnoreCase) &&
            candidate.CustomerName.Equals(ResolveCustomerName(triageItem), StringComparison.OrdinalIgnoreCase));

        return thread is not null && !string.IsNullOrWhiteSpace(thread.NextActionSummary);
    }

    private static OperationsInsightFeedItem CreateThreadItem(ThreadData thread, int basePriority)
    {
        var summary = string.IsNullOrWhiteSpace(thread.NextActionSummary)
            ? $"Follow up with {thread.CustomerName}"
            : thread.NextActionSummary;

        return new OperationsInsightFeedItem
        {
            Kind = OperationsInsightFeedKind.ThreadAction,
            DedupeKey = $"thread:{thread.ThreadId}",
            CustomerName = thread.CustomerName,
            BranchName = string.IsNullOrWhiteSpace(thread.BranchName)
                ? thread.InstanceDisplayName
                : thread.BranchName,
            Summary = summary,
            InstanceId = thread.InstanceId,
            ThreadId = thread.ThreadId,
            TriageItemId = thread.LastTriageItemId,
            IntentLabel = UnifiedMessengerDashboardPresentationHelper.FormatIntentLabel(thread.AiIntentCategory),
            UrgencyLabel = $"U{thread.UrgencyScore}",
            SourceLabel = "Thread registry",
            PriorityScore = basePriority + thread.UrgencyScore,
            Thread = thread
        };
    }

    private static string ResolveCustomerName(MessageTriageItem item) =>
        string.IsNullOrWhiteSpace(item.CustomerName) ? "Customer" : item.CustomerName.Trim();

    private static IReadOnlyList<OperationsInsightFeedItem> SortAndTrim(List<OperationsInsightFeedItem> items) =>
        items
            .OrderByDescending(item => item.PriorityScore)
            .ThenBy(item => item.CustomerName, StringComparer.OrdinalIgnoreCase)
            .Take(MaxFeedItems)
            .ToList();
}
