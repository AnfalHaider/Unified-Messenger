using System.Collections.Concurrent;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class ThreadRegistryService : IThreadRegistryService
{
    private static readonly Lazy<ThreadRegistryService> LazyInstance =
        new(() => new ThreadRegistryService());

    private readonly ConcurrentDictionary<string, ThreadData> _threads =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _sortedCacheGate = new();
    private IReadOnlyList<ThreadData>? _sortedThreadsCache;

    public static ThreadRegistryService Instance => LazyInstance.Value;

    public event EventHandler? Changed;

    internal static ThreadRegistryService CreateForTests() => new();

    public IReadOnlyList<ThreadData> GetAllThreads()
    {
        lock (_sortedCacheGate)
        {
            if (_sortedThreadsCache is not null)
            {
                return _sortedThreadsCache;
            }

            _sortedThreadsCache = _threads.Values
                .OrderByDescending(thread => thread.LastMessageTime)
                .ToList();

            return _sortedThreadsCache;
        }
    }

    public void RestoreThreads(IEnumerable<ThreadData> threads)
    {
        _threads.Clear();
        InvalidateSortedCache();
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

        var key = ResolveKey(item.Platform, conversationKey, item.ConversationKey, item.CustomerName, item.MessagePreview);
        var threadId = ConversationKeyResolver.BuildThreadId(item.InstanceId, key);
        var now = DateTimeOffset.UtcNow;
        var branch = string.IsNullOrWhiteSpace(branchName)
            ? item.BranchName
            : branchName.Trim();
        var spam = isSpamOrPromo ?? item.IsSpamOrPromo;
        var threadExisted = _threads.ContainsKey(threadId);

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

        var enrichmentOnResolved = thread.IsReplied && item.TimestampUtc <= thread.LastMessageTime;
        var reopenAfterReply = thread.IsReplied && item.TimestampUtc > thread.LastMessageTime;

        thread.Platform = item.Platform;
        thread.InstanceId = item.InstanceId;
        thread.InstanceDisplayName = item.InstanceDisplayName;
        thread.BranchName = string.IsNullOrWhiteSpace(branch) ? thread.BranchName : branch;
        thread.CustomerName = item.CustomerName;
        thread.ConversationKey = key;
        thread.LastMessageTime = item.TimestampUtc;
        thread.LastTriageItemId = item.Id;
        thread.IsSpamOrPromo = spam;
        thread.LastMessageKind = item.MessageKind.ToString();
        if (item.MessageKind == InboundMessageKind.VoiceNote)
        {
            thread.HasUnreadVoiceNote = false;
        }

        if (reopenAfterReply)
        {
            thread.FirstInboundAtUtc = item.TimestampUtc;
            thread.IsReplied = false;
            thread.ReplyLatencyMinutes = 0;
        }
        else if (!threadExisted && thread.FirstInboundAtUtc == default)
        {
            thread.FirstInboundAtUtc = item.TimestampUtc;
        }
        else if (!enrichmentOnResolved && thread.FirstInboundAtUtc == default)
        {
            thread.FirstInboundAtUtc = item.TimestampUtc;
        }

        if (!enrichmentOnResolved)
        {
            thread.IsReplied = false;
            thread.ReplyLatencyMinutes = 0;
            thread.LatencyMinutes = spam
                ? 0
                : Math.Max(0, (now - thread.FirstInboundAtUtc).TotalMinutes);
        }

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
            : isRevenueLeakageRisk ?? (item.IsRevenueLeakageRisk || EvaluateRevenueLeakage(thread));

        NotifyChanged();
    }

    public void MarkThreadResolved(
        string instanceId,
        string? conversationKey,
        string? customerName,
        DateTimeOffset? resolvedAtUtc = null,
        string? platform = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var resolvedAt = resolvedAtUtc ?? DateTimeOffset.UtcNow;
        var key = ResolveKey(platform ?? string.Empty, conversationKey, null, customerName, null);
        var threadId = ConversationKeyResolver.BuildThreadId(instanceId, key);
        if (!_threads.TryGetValue(threadId, out var thread))
        {
            thread = new ThreadData
            {
                ThreadId = threadId,
                Platform = string.IsNullOrWhiteSpace(platform) ? "unknown" : platform.Trim(),
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
        MessageAnalyticsService.Instance.RecordThreadReply(
            instanceId,
            key,
            inboundAt,
            resolvedAt,
            customerName);
        NotifyChanged();
    }

    public void UpdateLastMessageKind(
        string instanceId,
        string conversationKey,
        InboundMessageKind messageKind,
        DateTimeOffset messageAtUtc)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(conversationKey))
        {
            return;
        }

        var threadId = ConversationKeyResolver.BuildThreadId(instanceId, conversationKey.Trim());
        if (!_threads.TryGetValue(threadId, out var thread))
        {
            thread = _threads.GetOrAdd(threadId, _ => new ThreadData
            {
                ThreadId = threadId,
                Platform = "whatsapp",
                InstanceId = instanceId,
                ConversationKey = conversationKey.Trim(),
                CustomerName = conversationKey.Trim(),
                LastMessageTime = messageAtUtc,
                FirstInboundAtUtc = messageAtUtc
            });
        }

        thread.LastMessageKind = messageKind.ToString();
        if (messageAtUtc > thread.LastMessageTime)
        {
            thread.LastMessageTime = messageAtUtc;
        }

        NotifyChanged();
    }

    public void UpdateWhatsAppDeliveryStatus(
        string instanceId,
        string conversationKey,
        string status,
        DateTimeOffset? updatedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId) ||
            string.IsNullOrWhiteSpace(conversationKey) ||
            string.IsNullOrWhiteSpace(status))
        {
            return;
        }

        var normalized = WhatsAppDeliveryStatusLabel.Normalize(status);
        if (!WhatsAppDeliveryStatusLabel.IsKnown(normalized))
        {
            return;
        }

        var threadId = ConversationKeyResolver.BuildThreadId(instanceId, conversationKey.Trim());
        if (!_threads.TryGetValue(threadId, out var thread))
        {
            var createdAt = updatedAtUtc ?? DateTimeOffset.UtcNow;
            thread = _threads.GetOrAdd(threadId, _ => new ThreadData
            {
                ThreadId = threadId,
                Platform = "whatsapp",
                InstanceId = instanceId,
                ConversationKey = conversationKey.Trim(),
                CustomerName = conversationKey.Trim(),
                LastMessageTime = createdAt,
                FirstInboundAtUtc = createdAt
            });
        }

        var incomingRank = WhatsAppDeliveryStatusLabel.Rank(normalized);
        var currentRank = WhatsAppDeliveryStatusLabel.Rank(thread.WhatsAppDeliveryStatus);
        if (incomingRank < currentRank)
        {
            return;
        }

        thread.WhatsAppDeliveryStatus = normalized;
        thread.WhatsAppDeliveryUpdatedUtc = updatedAtUtc ?? DateTimeOffset.UtcNow;
        NotifyChanged();
    }

    public void MarkVoiceNoteReceived(
        string instanceId,
        string conversationKey,
        double durationSeconds,
        bool hasUnreadVoiceNote)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(conversationKey))
        {
            return;
        }

        var threadId = ConversationKeyResolver.BuildThreadId(instanceId, conversationKey.Trim());
        var now = DateTimeOffset.UtcNow;
        var thread = _threads.GetOrAdd(threadId, _ => new ThreadData
        {
            ThreadId = threadId,
            Platform = "whatsapp",
            InstanceId = instanceId,
            ConversationKey = conversationKey.Trim(),
            CustomerName = "Customer",
            LastMessageTime = now,
            FirstInboundAtUtc = now
        });

        thread.LastMessageKind = nameof(InboundMessageKind.VoiceNote);
        thread.HasUnreadVoiceNote = hasUnreadVoiceNote;
        thread.LastMessageTime = now;
        thread.LatencyMinutes = thread.IsReplied
            ? thread.LatencyMinutes
            : Math.Max(0, (now - thread.FirstInboundAtUtc).TotalMinutes);

        if (durationSeconds > 0 && string.IsNullOrWhiteSpace(thread.NextActionSummary))
        {
            thread.NextActionSummary = $"Review {Math.Max(1, (int)Math.Round(durationSeconds))}s voice note";
        }

        NotifyChanged();
    }

    public void RefreshOperationalFlags(bool raiseChanged = true) =>
        RefreshOperationalFlagsCore(raiseChanged);

    private void RefreshOperationalFlagsCore(bool raiseChanged)
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

            var leakage = thread.IsRevenueLeakageRisk || EvaluateRevenueLeakage(thread);
            if (thread.IsRevenueLeakageRisk != leakage)
            {
                thread.IsRevenueLeakageRisk = leakage;
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        InvalidateSortedCache();
        if (raiseChanged)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

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

        return isCommercialIntent && thread.LatencyMinutes >= OperationalThresholds.GetRevenueLeakageMinutes();
    }

    private static string ResolveKey(
        string platform,
        string? conversationKey,
        string? itemConversationKey,
        string? customerName,
        string? messagePreview)
    {
        if (!string.IsNullOrWhiteSpace(conversationKey))
        {
            return ConversationKeyResolver.Resolve(
                platform,
                conversationKey,
                itemConversationKey,
                customerName,
                messagePreview);
        }

        if (!string.IsNullOrWhiteSpace(itemConversationKey))
        {
            return ConversationKeyResolver.Resolve(
                platform,
                itemConversationKey,
                conversationHint: itemConversationKey,
                customerName,
                messagePreview);
        }

        return ConversationKeyResolver.Resolve(
            platform,
            customerName: customerName,
            messagePreview: messagePreview);
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
            var sanitized = ConversationNoiseFilter.SanitizeSummary(nextActionSummary);
            if (!string.IsNullOrWhiteSpace(sanitized))
            {
                return sanitized;
            }
        }

        if (!string.IsNullOrWhiteSpace(item.NextActionSummary))
        {
            var sanitized = ConversationNoiseFilter.SanitizeSummary(item.NextActionSummary);
            if (!string.IsNullOrWhiteSpace(sanitized))
            {
                return sanitized;
            }
        }

        return ConversationNoiseFilter.SanitizeSummary(item.CoreSummary);
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

    private void InvalidateSortedCache()
    {
        lock (_sortedCacheGate)
        {
            _sortedThreadsCache = null;
        }
    }

    private void NotifyChanged()
    {
        InvalidateSortedCache();
        Changed?.Invoke(this, EventArgs.Empty);
    }
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
        thread.WhatsAppDeliveryStatus = string.IsNullOrWhiteSpace(thread.WhatsAppDeliveryStatus)
            ? string.Empty
            : WhatsAppDeliveryStatusLabel.Normalize(thread.WhatsAppDeliveryStatus);

        if (thread.FirstInboundAtUtc == default)
        {
            thread.FirstInboundAtUtc = thread.LastMessageTime;
        }

        if (string.IsNullOrWhiteSpace(thread.ConversationKey) && !string.IsNullOrWhiteSpace(thread.ThreadId))
        {
            var separator = thread.ThreadId.IndexOf('|', StringComparison.Ordinal);
            if (separator >= 0 && separator < thread.ThreadId.Length - 1)
            {
                thread.ConversationKey = thread.ThreadId[(separator + 1)..];
            }
        }
    }
}
