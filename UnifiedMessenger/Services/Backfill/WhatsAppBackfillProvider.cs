using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Services.Backfill;

public sealed class WhatsAppBackfillProvider : IBackfillSyncProvider
{
    public const int DefaultMaxChats = 20;

    public const int DeepBackfillMaxChats = 3;

    public string PlatformId => "whatsapp";

    public bool CanBackfill(MessengerInstance instance)
    {
        if (string.IsNullOrWhiteSpace(instance.Platform))
        {
            return false;
        }

        var platform = instance.Platform.Trim();
        return platform.Equals("whatsapp", StringComparison.OrdinalIgnoreCase) ||
               platform.Equals("whatsappbusiness", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<BackfillResult> RunAsync(BackfillContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var instance = context.Instance;
        var result = new BackfillAccumulator();

        await BroadcastBackfillOptionsAsync(instance.Id, context, cancellationToken).ConfigureAwait(false);

        await InstanceSessionManager.Instance
            .TryExecuteScriptOnInstanceAsync(
                instance.Id,
                "window.__unifiedMessengerPublishBadge && window.__unifiedMessengerPublishBadge();")
            .ConfigureAwait(false);

        await Task.Delay(400, cancellationToken).ConfigureAwait(false);

        result.DailyAggregateDaysMerged = await MergeDailyAggregatesAsync(instance.Id, cancellationToken)
            .ConfigureAwait(false);
        result.SidebarRowsCaptured = await CaptureSidebarSnapshotAsync(instance, cancellationToken)
            .ConfigureAwait(false);

        var raw = await InstanceSessionManager.Instance
            .TryExecuteScriptOnInstanceAsync(
                instance.Id,
                $"window.__umCollectBackfillCandidates({context.BackfillMaxChats})")
            .ConfigureAwait(false);

        var payload = ParseCollectResponse(raw);
        var urgentLlmCount = 0;
        if (payload?.Candidates is null || payload.Candidates.Count == 0)
        {
            result.HistoryChunksProcessed = await ProcessOpenChatHistoryAsync(instance, result, context, cancellationToken)
                .ConfigureAwait(false);

            if (context.EnableDeepBackfill)
            {
                result.HistoryChunksProcessed += await RunDeepBackfillMvpAsync(instance, result, context, cancellationToken)
                    .ConfigureAwait(false);
            }

            await CommitBaselineAsync(instance.Id, cancellationToken).ConfigureAwait(false);
            return result.ToResult(payload?.Ok == false ? "WhatsApp backfill returned no candidates." : null);
        }

        foreach (var candidate in payload.Candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(candidate.LastMessageBody) ||
                candidate.LastMessageBody.Trim().Length < 8)
            {
                continue;
            }

            var body = candidate.LastMessageBody.Trim();
            var conversationKey = ConversationKeyResolver.Resolve(
                instance.Platform,
                candidate.ChatKey,
                candidate.ChatKey,
                candidate.Title,
                body);

            var timestamp = ParseTimestamp(candidate.LastMessageTimestamp);
            if (!await BackfillDedupeStore.Instance
                    .TryAcceptForDayAsync(instance.Id, instance.Platform, conversationKey, timestamp, cancellationToken)
                    .ConfigureAwait(false))
            {
                result.TriageSkippedDuplicate++;
                continue;
            }

            if (!BackfillDedupeRegistry.TryAccept(instance.Id, instance.Platform, conversationKey, body))
            {
                result.TriageSkippedDuplicate++;
                continue;
            }

            MessageAnalyticsService.Instance.RecordBackfillInbound(
                instance.Id,
                timestamp,
                context.SlaThresholdMinutes);
            result.AnalyticsInboundRecorded++;
            result.SlaCandidatesRecorded += CountSlaFromBackfill(timestamp, context.SlaThresholdMinutes);

            var allowLlm = context.EnableUrgentLlmInference &&
                           urgentLlmCount < context.MaxUrgentLlmPerInstance &&
                           IsUrgentBackfillCandidate(body);

            MessageTriageService.Instance.Enqueue(
                new InboundMessageSelection
                {
                    InstanceId = instance.Id,
                    Platform = instance.Platform,
                    MessageText = body,
                    CustomerName = string.IsNullOrWhiteSpace(candidate.Title) ? "Customer" : candidate.Title.Trim(),
                    ConversationKey = conversationKey,
                    ConversationHint = conversationKey,
                    TimestampUtc = timestamp
                },
                instance.DisplayName,
                BranchWorkspaceHelper.ResolveBranchKey(instance),
                allowLlmInference: allowLlm,
                isBackfilled: true);

            if (allowLlm)
            {
                urgentLlmCount++;
            }

            ThreadRegistryService.Instance.UpdateLastMessageKind(
                instance.Id,
                conversationKey,
                InboundMessageKind.Text,
                timestamp);

            result.TriageEnqueued++;
        }

        // Primary, robust source: read every conversation's history from local IndexedDB (stable keys).
        result.HistoryChunksProcessed = await ProcessIndexedDbConversationsAsync(instance, result, context, cancellationToken)
            .ConfigureAwait(false);

        // DOM fallback: only the currently-open chat (covers builds where the DB read returns nothing).
        result.HistoryChunksProcessed += await ProcessOpenChatHistoryAsync(instance, result, context, cancellationToken)
            .ConfigureAwait(false);

        if (context.EnableDeepBackfill)
        {
            result.HistoryChunksProcessed += await RunDeepBackfillMvpAsync(instance, result, context, cancellationToken)
                .ConfigureAwait(false);
        }

        await CommitBaselineAsync(instance.Id, cancellationToken).ConfigureAwait(false);
        return result.ToResult();
    }

    private static bool IsUrgentBackfillCandidate(string messageBody)
    {
        if (string.IsNullOrWhiteSpace(messageBody))
        {
            return false;
        }

        var lower = messageBody.ToLowerInvariant();
        return lower.Contains("urgent", StringComparison.Ordinal) ||
               lower.Contains("asap", StringComparison.Ordinal) ||
               lower.Contains("emergency", StringComparison.Ordinal) ||
               lower.Contains('?') && lower.Length >= 12;
    }

    private static async Task BroadcastBackfillOptionsAsync(
        string instanceId,
        BackfillContext context,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var mode = context.BackfillMode switch
        {
            WhatsAppBackfillMode.Recent => "recent",
            WhatsAppBackfillMode.All => "all",
            _ => "unread"
        };

        var script =
            $"window.__umSetBackfillOptions && window.__umSetBackfillOptions({{ mode: '{mode}', recentDays: {context.BackfillRecentDays}, maxChats: {context.BackfillMaxChats} }});";

        await InstanceSessionManager.Instance.TryExecuteScriptOnInstanceAsync(instanceId, script).ConfigureAwait(false);
    }

    private static async Task<int> MergeDailyAggregatesAsync(string instanceId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var raw = await InstanceSessionManager.Instance
            .TryExecuteScriptOnInstanceAsync(instanceId, "window.__umCollectMessageDailyAggregates && window.__umCollectMessageDailyAggregates()")
            .ConfigureAwait(false);

        var payload = ParseJsonRoot(raw);
        if (payload is null ||
            !payload.Value.TryGetProperty("received", out var receivedElement) ||
            !payload.Value.TryGetProperty("sent", out var sentElement))
        {
            return 0;
        }

        var received = ReadDayBuckets(receivedElement);
        var sent = ReadDayBuckets(sentElement);
        if (received.Count == 0 && sent.Count == 0)
        {
            return 0;
        }

        MessageAnalyticsService.Instance.RecordBackfillDailyAggregate(instanceId, received, sent);
        return received.Count + sent.Count;
    }

    private static async Task<int> CaptureSidebarSnapshotAsync(
        MessengerInstance instance,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var raw = await InstanceSessionManager.Instance
            .TryExecuteScriptOnInstanceAsync(
                instance.Id,
                "window.__umCollectSidebarSnapshot && window.__umCollectSidebarSnapshot()")
            .ConfigureAwait(false);

        var payload = ParseJsonRoot(raw);
        if (payload is null || !payload.Value.TryGetProperty("rows", out var rowsElement) ||
            rowsElement.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var count = 0;
        foreach (var row in rowsElement.EnumerateArray())
        {
            var title = ReadString(row, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var conversationKey = ReadString(row, "conversationKey") ?? title;
            var preview = ReadString(row, "preview") ?? string.Empty;
            var relativeTime = ReadString(row, "relativeTime") ?? string.Empty;
            var capturedAt = ParseTimestamp(ReadString(row, "timestampUtc"));

            WhatsAppBusinessContextService.Instance.UpsertThreadContext(new WhatsAppThreadContextSnapshot
            {
                InstanceId = instance.Id,
                ConversationKey = conversationKey,
                CustomerName = title,
                CapturedAtUtc = capturedAt
            });

            if (!string.IsNullOrWhiteSpace(preview))
            {
                ThreadRegistryService.Instance.UpdateLastMessageKind(
                    instance.Id,
                    conversationKey,
                    InboundMessageKind.Text,
                    capturedAt);
            }

            _ = relativeTime;
            count++;
        }

        return count;
    }

    /// <summary>
    /// Robust history source: reads conversation history straight from WhatsApp Web's local
    /// 'model-storage' IndexedDB (stable chat JIDs for every chat) instead of scrolling/clicking the
    /// DOM. Each conversation yields its last inbound message with a stable conversation key, so the
    /// command-center drill-down can focus the right chat and history survives restarts.
    /// </summary>
    private static async Task<int> ProcessIndexedDbConversationsAsync(
        MessengerInstance instance,
        BackfillAccumulator result,
        BackfillContext context,
        CancellationToken cancellationToken)
    {
        // Start the async IndexedDB scan, then poll the synchronous getter — ExecuteScriptAsync does not
        // await JS promises, so a pending promise would serialize to "{}".
        await InstanceSessionManager.Instance
            .TryExecuteScriptOnInstanceAsync(
                instance.Id,
                $"window.__umStartDbConversationScan && window.__umStartDbConversationScan({context.BackfillMaxChats})")
            .ConfigureAwait(false);

        JsonElement? payload = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            var raw = await InstanceSessionManager.Instance
                .TryExecuteScriptOnInstanceAsync(
                    instance.Id,
                    "window.__umGetDbConversationResult ? window.__umGetDbConversationResult() : ''")
                .ConfigureAwait(false);

            // Empty string (or its JSON-encoded form) means "not ready yet" — keep polling.
            if (string.IsNullOrWhiteSpace(raw) || raw == "\"\"" || raw == "null")
            {
                continue;
            }

            payload = ParseJsonRoot(raw);
            if (payload is not null)
            {
                break;
            }
        }

        if (payload is null || !payload.Value.TryGetProperty("conversations", out var conversations) ||
            conversations.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var processed = 0;
        foreach (var conversation in conversations.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.DbConversationsFound++;

            var conversationKey = ReadString(conversation, "conversationKey");
            if (string.IsNullOrWhiteSpace(conversationKey))
            {
                continue;
            }

            var customerName = ReadString(conversation, "customerName") ?? "Customer";
            var lastMessageFromMe = ReadBool(conversation, "lastMessageFromMe");

            // Reconciliation runs regardless of whether we extracted a body: migrate any legacy
            // title-keyed thread for this customer to the stable JID so we update it in place (no
            // duplicate) and drill-down gets a real key.
            if (ThreadRegistryService.Instance.ReconcileConversationKey(instance.Id, conversationKey, customerName))
            {
                result.KeysMigrated++;
            }

            // If the conversation's last message is from us, the customer was answered (often on the
            // phone, before we started watching). Mark it replied so it stops counting as a breach.
            // This does NOT need an inbound body, so it happens before the body check below.
            if (lastMessageFromMe)
            {
                var resolvedAt = ParseTimestamp(ReadString(conversation, "lastActivityTimestampUtc"));
                ThreadRegistryService.Instance.MarkThreadResolved(
                    instance.Id,
                    conversationKey,
                    customerName,
                    resolvedAt,
                    instance.Platform);
                result.AnsweredReconciled++;
                continue;
            }

            // Awaiting-reply path needs an actual inbound body to triage.
            var body = ReadString(conversation, "lastInboundBody");
            if (string.IsNullOrWhiteSpace(body) || body.Trim().Length < 8)
            {
                continue;
            }

            var timestamp = ParseTimestamp(ReadString(conversation, "lastInboundTimestampUtc"));
            if (!await BackfillDedupeStore.Instance
                    .TryAcceptForDayAsync(instance.Id, instance.Platform, conversationKey, timestamp, cancellationToken)
                    .ConfigureAwait(false))
            {
                result.TriageSkippedDuplicate++;
                continue;
            }

            if (!BackfillDedupeRegistry.TryAccept(instance.Id, instance.Platform, conversationKey, body))
            {
                result.TriageSkippedDuplicate++;
                continue;
            }

            MessageAnalyticsService.Instance.RecordBackfillInbound(instance.Id, timestamp, context.SlaThresholdMinutes);
            result.AnalyticsInboundRecorded++;

            MessageTriageService.Instance.Enqueue(
                new InboundMessageSelection
                {
                    InstanceId = instance.Id,
                    Platform = instance.Platform,
                    MessageText = body.Trim(),
                    CustomerName = customerName,
                    ConversationKey = conversationKey,
                    ConversationHint = conversationKey,
                    TimestampUtc = timestamp
                },
                instance.DisplayName,
                BranchWorkspaceHelper.ResolveBranchKey(instance),
                allowLlmInference: false,
                isBackfilled: true);

            ThreadRegistryService.Instance.UpdateLastMessageKind(
                instance.Id,
                conversationKey,
                InboundMessageKind.Text,
                timestamp);

            result.TriageEnqueued++;
            processed++;
        }

        return processed;
    }

    private static async Task<int> ProcessOpenChatHistoryAsync(
        MessengerInstance instance,
        BackfillAccumulator result,
        BackfillContext context,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var raw = await InstanceSessionManager.Instance
            .TryExecuteScriptOnInstanceAsync(
                instance.Id,
                "window.__umScrollBackOpenChatHistory && window.__umScrollBackOpenChatHistory(4)")
            .ConfigureAwait(false);

        var payload = ParseJsonRoot(raw);
        if (payload is null || !payload.Value.TryGetProperty("messages", out var messagesElement) ||
            messagesElement.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var processed = 0;
        foreach (var message in messagesElement.EnumerateArray())
        {
            var body = ReadString(message, "body");
            if (string.IsNullOrWhiteSpace(body) || body.Trim().Length < 8)
            {
                continue;
            }

            var conversationKey = ReadString(message, "conversationKey") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(conversationKey))
            {
                continue;
            }

            var timestamp = ParseTimestamp(ReadString(message, "timestampUtc"));
            if (ReadBool(message, "isOutgoing"))
            {
                continue;
            }

            if (!await BackfillDedupeStore.Instance
                    .TryAcceptForDayAsync(instance.Id, instance.Platform, conversationKey, timestamp, cancellationToken)
                    .ConfigureAwait(false))
            {
                result.TriageSkippedDuplicate++;
                continue;
            }

            if (!BackfillDedupeRegistry.TryAccept(instance.Id, instance.Platform, conversationKey, body))
            {
                continue;
            }

            MessageAnalyticsService.Instance.RecordBackfillInbound(instance.Id, timestamp, context.SlaThresholdMinutes);
            result.AnalyticsInboundRecorded++;

            MessageTriageService.Instance.Enqueue(
                new InboundMessageSelection
                {
                    InstanceId = instance.Id,
                    Platform = instance.Platform,
                    MessageText = body.Trim(),
                    CustomerName = ReadString(message, "customerName") ?? "Customer",
                    ConversationKey = conversationKey,
                    ConversationHint = conversationKey,
                    TimestampUtc = timestamp
                },
                instance.DisplayName,
                BranchWorkspaceHelper.ResolveBranchKey(instance),
                allowLlmInference: false,
                isBackfilled: true);

            processed++;
        }

        return processed;
    }

    /// <summary>
    /// MVP deep backfill: bounded sidebar walk (max 3 chats). Full automation deferred — see README v3.4.0 notes.
    /// </summary>
    private static async Task<int> RunDeepBackfillMvpAsync(
        MessengerInstance instance,
        BackfillAccumulator result,
        BackfillContext context,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var raw = await InstanceSessionManager.Instance
            .TryExecuteScriptOnInstanceAsync(
                instance.Id,
                $"window.__umRunDeepBackfillWalk && window.__umRunDeepBackfillWalk({DeepBackfillMaxChats})")
            .ConfigureAwait(false);

        var payload = ParseJsonRoot(raw);
        if (payload is null || !payload.Value.TryGetProperty("messages", out var messagesElement) ||
            messagesElement.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var processed = 0;
        foreach (var message in messagesElement.EnumerateArray())
        {
            var body = ReadString(message, "body");
            var conversationKey = ReadString(message, "conversationKey");
            if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(conversationKey) || body.Trim().Length < 8)
            {
                continue;
            }

            var timestamp = ParseTimestamp(ReadString(message, "timestampUtc"));
            if (!await BackfillDedupeStore.Instance
                    .TryAcceptForDayAsync(instance.Id, instance.Platform, conversationKey, timestamp, cancellationToken)
                    .ConfigureAwait(false))
            {
                continue;
            }

            if (!BackfillDedupeRegistry.TryAccept(instance.Id, instance.Platform, conversationKey, body))
            {
                continue;
            }

            MessageAnalyticsService.Instance.RecordBackfillInbound(instance.Id, timestamp, context.SlaThresholdMinutes);
            MessageTriageService.Instance.Enqueue(
                new InboundMessageSelection
                {
                    InstanceId = instance.Id,
                    Platform = instance.Platform,
                    MessageText = body.Trim(),
                    CustomerName = ReadString(message, "customerName") ?? "Customer",
                    ConversationKey = conversationKey,
                    ConversationHint = conversationKey,
                    TimestampUtc = timestamp
                },
                instance.DisplayName,
                BranchWorkspaceHelper.ResolveBranchKey(instance),
                allowLlmInference: false,
                isBackfilled: true);

            result.TriageEnqueued++;
            processed++;
        }

        _ = context;
        return processed;
    }

    private static async Task CommitBaselineAsync(string instanceId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        await InstanceSessionManager.Instance
            .TryExecuteScriptOnInstanceAsync(
                instanceId,
                "window.__umCommitInboundBaseline && window.__umCommitInboundBaseline();")
            .ConfigureAwait(false);
    }

    private static int CountSlaFromBackfill(DateTimeOffset receivedAt, int slaThresholdMinutes)
    {
        var ageMinutes = (DateTimeOffset.UtcNow - receivedAt).TotalMinutes;
        return ageMinutes > slaThresholdMinutes ? 1 : 0;
    }

    private static DateTimeOffset ParseTimestamp(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DateTimeOffset.UtcNow;
        }

        return DateTimeOffset.TryParse(raw, out var parsed) ? parsed.ToUniversalTime() : DateTimeOffset.UtcNow;
    }

    private static Dictionary<string, int> ReadDayBuckets(JsonElement element)
    {
        var buckets = new Dictionary<string, int>(StringComparer.Ordinal);
        if (element.ValueKind != JsonValueKind.Object)
        {
            return buckets;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.TryGetInt32(out var count) && count > 0)
            {
                buckets[property.Name] = count;
            }
        }

        return buckets;
    }

    private static JsonElement? ParseJsonRoot(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                var inner = root.GetString();
                if (string.IsNullOrWhiteSpace(inner))
                {
                    return null;
                }

                using var innerDocument = JsonDocument.Parse(inner);
                return innerDocument.RootElement.Clone();
            }

            return root.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static WhatsAppBackfillPayload? ParseCollectResponse(string? raw)
    {
        var root = ParseJsonRoot(raw);
        return root is null ? null : ParseCollectRoot(root.Value);
    }

    private static WhatsAppBackfillPayload? ParseCollectRoot(JsonElement root)
    {
        try
        {
            var ok = root.TryGetProperty("ok", out var okElement) &&
                     okElement.ValueKind == JsonValueKind.True;

            var candidates = new List<WhatsAppBackfillCandidate>();
            if (root.TryGetProperty("candidates", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in array.EnumerateArray())
                {
                    candidates.Add(new WhatsAppBackfillCandidate
                    {
                        ChatKey = ReadString(entry, "chatKey") ?? string.Empty,
                        Title = ReadString(entry, "title") ?? "Customer",
                        LastMessageBody = ReadString(entry, "lastMessageBody") ?? string.Empty,
                        LastMessageTimestamp = ReadString(entry, "lastMessageTimestamp"),
                        UnreadCount = entry.TryGetProperty("unreadCount", out var countElement) &&
                                       countElement.TryGetInt32(out var count)
                            ? count
                            : 0
                    });
                }
            }

            return new WhatsAppBackfillPayload { Ok = ok, Candidates = candidates };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var valueElement)
            ? valueElement.GetString()
            : null;

    private static bool ReadBool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var valueElement) &&
        valueElement.ValueKind == JsonValueKind.True;

    private sealed class WhatsAppBackfillPayload
    {
        public bool Ok { get; init; }

        public IReadOnlyList<WhatsAppBackfillCandidate> Candidates { get; init; } = [];
    }

    private sealed class WhatsAppBackfillCandidate
    {
        public string ChatKey { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string LastMessageBody { get; init; } = string.Empty;

        public string? LastMessageTimestamp { get; init; }

        public int UnreadCount { get; init; }
    }

    private sealed class BackfillAccumulator
    {
        public int TriageEnqueued { get; set; }

        public int TriageSkippedDuplicate { get; set; }

        public int AnalyticsInboundRecorded { get; set; }

        public int SlaCandidatesRecorded { get; set; }

        public int DailyAggregateDaysMerged { get; set; }

        public int SidebarRowsCaptured { get; set; }

        public int HistoryChunksProcessed { get; set; }

        public int DbConversationsFound { get; set; }

        public int AnsweredReconciled { get; set; }

        public int KeysMigrated { get; set; }

        public BackfillResult ToResult(string? error = null) =>
            new()
            {
                TriageEnqueued = TriageEnqueued,
                TriageSkippedDuplicate = TriageSkippedDuplicate,
                AnalyticsInboundRecorded = AnalyticsInboundRecorded,
                SlaCandidatesRecorded = SlaCandidatesRecorded,
                DailyAggregateDaysMerged = DailyAggregateDaysMerged,
                SidebarRowsCaptured = SidebarRowsCaptured,
                HistoryChunksProcessed = HistoryChunksProcessed,
                DbConversationsFound = DbConversationsFound,
                AnsweredReconciled = AnsweredReconciled,
                KeysMigrated = KeysMigrated,
                ErrorMessage = error
            };
    }
}
