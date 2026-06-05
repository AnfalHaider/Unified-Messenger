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
        bool? isRevenueLeakageRisk = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        var key = NormalizeConversationKey(conversationKey, item.CustomerName, item.MessagePreview);
        var threadId = BuildThreadId(item.InstanceId, key);
        var now = DateTimeOffset.UtcNow;
        var branch = string.IsNullOrWhiteSpace(branchName)
            ? item.BranchName
            : branchName.Trim();

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
            LastTriageItemId = item.Id
        });

        thread.Platform = item.Platform;
        thread.InstanceId = item.InstanceId;
        thread.InstanceDisplayName = item.InstanceDisplayName;
        thread.BranchName = string.IsNullOrWhiteSpace(branch) ? thread.BranchName : branch;
        thread.CustomerName = item.CustomerName;
        thread.ConversationKey = key;
        thread.LastMessageTime = item.TimestampUtc;
        thread.LastTriageItemId = item.Id;
        thread.IsReplied = false;
        thread.LatencyMinutes = Math.Max(0, (now - item.TimestampUtc).TotalMinutes);

        thread.AiIntentCategory = aiIntentCategory
                                  ?? item.AiIntentCategory
                                  ?? MapIntent(item.CustomerIntent);
        thread.ClientSentiment = clientSentiment
                                 ?? item.ClientSentiment
                                 ?? MapSentiment(item.Sentiment);
        thread.UrgencyScore = operationalUrgency
                              ?? (item.OperationalUrgency > 0
                                  ? item.OperationalUrgency
                                  : MapOperationalUrgency(item.UrgencyScore));
        thread.NextActionSummary = string.IsNullOrWhiteSpace(nextActionSummary)
            ? string.IsNullOrWhiteSpace(item.NextActionSummary)
                ? item.CoreSummary
                : item.NextActionSummary
            : nextActionSummary.Trim();
        thread.EstimatedValue = estimatedValue ?? item.EstimatedValue;
        thread.IsRevenueLeakageRisk = isRevenueLeakageRisk ?? EvaluateRevenueLeakage(thread);

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
                LastMessageTime = resolvedAtUtc ?? DateTimeOffset.UtcNow
            };
            _threads[threadId] = thread;
        }

        thread.IsReplied = true;
        thread.IsRevenueLeakageRisk = false;
        thread.LatencyMinutes = 0;
        thread.LastMessageTime = resolvedAtUtc ?? DateTimeOffset.UtcNow;
        NotifyChanged();
    }

    public void RefreshOperationalFlags()
    {
        var now = DateTimeOffset.UtcNow;
        var changed = false;

        foreach (var thread in _threads.Values)
        {
            if (thread.IsReplied)
            {
                continue;
            }

            var latency = Math.Max(0, (now - thread.LastMessageTime).TotalMinutes);
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
        if (thread.IsReplied)
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

    private static string MapIntent(CustomerIntent intent) =>
        intent switch
        {
            CustomerIntent.Booking => UnifiedMessengerIntentCategory.Booking,
            CustomerIntent.Complaint => UnifiedMessengerIntentCategory.Complaint,
            CustomerIntent.Spam => UnifiedMessengerIntentCategory.Inquiry,
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
        thread.UrgencyScore = Math.Clamp(thread.UrgencyScore, 1, 5);
        thread.LatencyMinutes = Math.Max(0, thread.LatencyMinutes);
        thread.EstimatedValue = Math.Max(0, thread.EstimatedValue);
    }
}
