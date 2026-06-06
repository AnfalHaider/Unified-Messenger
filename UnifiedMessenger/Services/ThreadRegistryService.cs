using System.Collections.Concurrent;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class ThreadRegistryService
{
    private static readonly Lazy<ThreadRegistryService> LazyInstance =
        new(() => new ThreadRegistryService());

    private readonly ConcurrentDictionary<string, ThreadData> _threads =
        new(StringComparer.OrdinalIgnoreCase);

    public static ThreadRegistryService Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    internal static ThreadRegistryService CreateForTests() => new();

    public IReadOnlyList<ThreadData> GetAllThreads() =>
        _threads.Values
            .OrderByDescending(thread => thread.LastMessageTime)
            .ToList();

    public void RestoreThreads(IEnumerable<ThreadData> threads)
    {
        _threads.Clear();
        foreach (var thread in threads)
        {
            if (string.IsNullOrWhiteSpace(thread.ThreadId))
            {
                continue;
            }

            thread.Normalize();
            _threads[thread.ThreadId] = thread;
        }
    }

    public void UpsertFromTriageItem(
        MessageTriageItem item,
        string? conversationKey,
        string? branchName,
        string? nextActionSummary = null,
        string? aiIntentCategory = null,
        string? clientSentiment = null,
        int? operationalUrgency = null,
        double? estimatedValue = null,
        bool? isRevenueLeakageRisk = null,
        bool? isSpamOrPromo = null,
        string? suggestedAction = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        var key = NormalizeConversationKey(conversationKey, item.CustomerName, item.MessagePreview);
        var threadId = BuildThreadId(item.InstanceId, key);
        var now = DateTimeOffset.UtcNow;
        var branch = string.IsNullOrWhiteSpace(branchName)
            ? item.BranchName
            : branchName.Trim();
        var spam = isSpamOrPromo ?? item.IsSpamOrPromo;

        var thread = _threads.GetOrAdd(threadId, _ => new ThreadData
        {
            ThreadId = threadId,
            Platform = item.Platform,
            InstanceId = item.InstanceId,
            InstanceDisplayName = item.InstanceDisplayName,
            BranchName = branch,
            CustomerName = item.CustomerName,
            ConversationKey = key,
            LastMessageTime = item.TimestampUtc,
            FirstInboundAtUtc = item.TimestampUtc,
            LastTriageItemId = item.Id
        });

        thread.Platform = item.Platform;
        thread.InstanceId = item.InstanceId;
        thread.InstanceDisplayName = item.InstanceDisplayName;
        thread.BranchName = string.IsNullOrWhiteSpace(branch) ? thread.BranchName : branch;
        thread.CustomerName = item.CustomerName;
        thread.ConversationKey = key;
        thread.LastMessageTime = item.TimestampUtc;
        thread.FirstInboundAtUtc = item.TimestampUtc;
        thread.LastTriageItemId = item.Id;
        thread.IsReplied = false;
        thread.IsSpamOrPromo = spam;
        thread.ReplyLatencyMinutes = 0;
        thread.LatencyMinutes = spam
            ? 0
            : Math.Max(0, (now - thread.FirstInboundAtUtc).TotalMinutes);

        thread.AiIntentCategory = aiIntentCategory
                                  ?? item.AiIntentCategory
                                  ?? MapIntent(item.CustomerIntent);
        thread.ClientSentiment = clientSentiment
                                 ?? item.ClientSentiment
                                 ?? MapSentiment(item.Sentiment);
        thread.UrgencyScore = spam
            ? 1
            : operationalUrgency
              ?? (item.OperationalUrgency > 0
                  ? item.OperationalUrgency
                  : MapOperationalUrgency(item.UrgencyScore));
        thread.NextActionSummary = ResolveNextActionSummary(nextActionSummary, item, spam);
        thread.SuggestedAction = string.IsNullOrWhiteSpace(suggestedAction)
            ? item.SuggestedAction
            : suggestedAction.Trim();
        thread.EstimatedValue = spam ? 0 : estimatedValue ?? item.EstimatedValue;
        thread.IsRevenueLeakageRisk = spam
            ? false
            : isRevenueLeakageRisk ?? EvaluateRevenueLeakage(thread);

        NotifyChanged();
    }

    public void MarkThreadResolved(
        string instanceId,
        string? conversationKey,
        string? customerName,
        DateTimeOffset? resolvedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var resolvedAt = resolvedAtUtc ?? DateTimeOffset.UtcNow;
        var key = NormalizeConversationKey(conversationKey, customerName, null);
        var threadId = BuildThreadId(instanceId, key);
        if (!_threads.TryGetValue(threadId, out var thread))
        {
            thread = new ThreadData
            {
                ThreadId = threadId,
                Platform = "whatsapp",
                InstanceId = instanceId,
                ConversationKey = key,
                CustomerName = string.IsNullOrWhiteSpace(customerName) ? "Customer" : customerName.Trim(),
                BranchName = string.Empty,
                LastMessageTime = resolvedAt,
                FirstInboundAtUtc = resolvedAt
            };
            _threads[threadId] = thread;
        }

        var inboundAt = thread.FirstInboundAtUtc == default
            ? thread.LastMessageTime
            : thread.FirstInboundAtUtc;
        var replyLatency = Math.Max(0, (resolvedAt - inboundAt).TotalMinutes);

        thread.IsReplied = true;
        thread.IsRevenueLeakageRisk = false;
        thread.ReplyLatencyMinutes = replyLatency;
        thread.LatencyMinutes = replyLatency;
        thread.LastMessageTime = resolvedAt;
        NotifyChanged();
    }

    public void RefreshOperationalFlags()
    {
        var now = DateTimeOffset.UtcNow;
        var changed = false;

        foreach (var thread in _threads.Values)
        {
            if (thread.IsReplied || thread.IsSpamOrPromo)
            {
                continue;
            }

            var inboundAt = thread.FirstInboundAtUtc == default
                ? thread.LastMessageTime
                : thread.FirstInboundAtUtc;
            var latency = Math.Max(0, (now - inboundAt).TotalMinutes);
            if (Math.Abs(thread.LatencyMinutes - latency) > 0.5)
            {
                thread.LatencyMinutes = latency;
                changed = true;
            }

            var leakage = EvaluateRevenueLeakage(thread);
            if (thread.IsRevenueLeakageRisk != leakage)
            {
                thread.IsRevenueLeakageRisk = leakage;
                changed = true;
            }
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    internal static string BuildThreadId(string instanceId, string conversationKey) =>
        $"{instanceId}|{conversationKey}";

    internal static int MapOperationalUrgency(int urgencyScore) =>
        urgencyScore switch
        {
            >= 80 => 5,
            >= 55 => 4,
            >= 30 => 3,
            >= 15 => 2,
            _ => 1
        };

    internal static bool EvaluateRevenueLeakage(ThreadData thread)
    {
        if (thread.IsReplied || thread.IsSpamOrPromo)
        {
            return false;
        }

        var intent = thread.AiIntentCategory ?? string.Empty;
        var isCommercialIntent =
            intent.Equals(UnifiedMessengerIntentCategory.PriceInquiry, StringComparison.OrdinalIgnoreCase) ||
            intent.Equals(UnifiedMessengerIntentCategory.Booking, StringComparison.OrdinalIgnoreCase) ||
            intent.Equals(UnifiedMessengerIntentCategory.Lead, StringComparison.OrdinalIgnoreCase);

        return isCommercialIntent && thread.LatencyMinutes >= 30;
    }

    private static string ResolveNextActionSummary(
        string? nextActionSummary,
        MessageTriageItem item,
        bool isSpam)
    {
        if (isSpam)
        {
            return "Promotional message — no action required";
        }

        if (!string.IsNullOrWhiteSpace(nextActionSummary))
        {
            return nextActionSummary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(item.NextActionSummary))
        {
            return item.NextActionSummary.Trim();
        }

        return string.IsNullOrWhiteSpace(item.CoreSummary) ? string.Empty : item.CoreSummary.Trim();
    }

    private static string MapIntent(CustomerIntent intent) =>
        intent switch
        {
            CustomerIntent.Booking => UnifiedMessengerIntentCategory.Booking,
            CustomerIntent.Complaint => UnifiedMessengerIntentCategory.Complaint,
            CustomerIntent.Spam => UnifiedMessengerIntentCategory.Spam,
            _ => UnifiedMessengerIntentCategory.Inquiry
        };

    private static string MapSentiment(MessageSentiment sentiment) =>
        sentiment switch
        {
            MessageSentiment.Positive => ClientSentimentLabel.Positive,
            MessageSentiment.Negative => ClientSentimentLabel.Frustrated,
            _ => ClientSentimentLabel.Neutral
        };

    private static string NormalizeConversationKey(
        string? conversationKey,
        string? customerName,
        string? messagePreview)
    {
        if (!string.IsNullOrWhiteSpace(conversationKey))
        {
            return conversationKey.Trim();
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            return customerName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(messagePreview))
        {
            return messagePreview.Length <= 48
                ? messagePreview.Trim()
                : messagePreview[..48].Trim();
        }

        return "unknown";
    }

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

internal static class ThreadDataExtensions
{
    public static void Normalize(this ThreadData thread)
    {
        thread.BranchName = string.IsNullOrWhiteSpace(thread.BranchName) ? "General" : thread.BranchName.Trim();
        thread.CustomerName = string.IsNullOrWhiteSpace(thread.CustomerName) ? "Customer" : thread.CustomerName.Trim();
        thread.AiIntentCategory = string.IsNullOrWhiteSpace(thread.AiIntentCategory)
            ? UnifiedMessengerIntentCategory.Inquiry
            : thread.AiIntentCategory.Trim();
        thread.ClientSentiment = string.IsNullOrWhiteSpace(thread.ClientSentiment)
            ? ClientSentimentLabel.Neutral
            : thread.ClientSentiment.Trim();
        thread.NextActionSummary = thread.NextActionSummary?.Trim() ?? string.Empty;
        thread.SuggestedAction = thread.SuggestedAction?.Trim() ?? string.Empty;
        thread.UrgencyScore = Math.Clamp(thread.UrgencyScore, 1, 5);
        thread.LatencyMinutes = Math.Max(0, thread.LatencyMinutes);
        thread.ReplyLatencyMinutes = Math.Max(0, thread.ReplyLatencyMinutes);
        thread.EstimatedValue = Math.Max(0, thread.EstimatedValue);

        if (thread.FirstInboundAtUtc == default)
        {
            thread.FirstInboundAtUtc = thread.LastMessageTime;
        }
    }
}
