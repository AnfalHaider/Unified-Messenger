using System.Text.Json;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Backfill;

public sealed class WhatsAppBackfillProvider : IBackfillSyncProvider
{
    public const int DefaultMaxChats = 20;

    public string PlatformId => "whatsapp";

    public bool CanBackfill(MessengerInstance instance) =>
        instance.Platform.Equals(PlatformId, StringComparison.OrdinalIgnoreCase);

    public async Task<BackfillResult> RunAsync(BackfillContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var instance = context.Instance;
        var result = new BackfillAccumulator();

        await InstanceSessionManager.Instance
            .TryExecuteScriptOnInstanceAsync(instance.Id, "window.__unifiedMessengerPublishBadge && window.__unifiedMessengerPublishBadge();")
            .ConfigureAwait(false);

        await Task.Delay(400, cancellationToken).ConfigureAwait(false);

        var raw = await InstanceSessionManager.Instance
            .TryExecuteScriptOnInstanceAsync(
                instance.Id,
                $"window.__umCollectBackfillCandidates({DefaultMaxChats})")
            .ConfigureAwait(false);

        var payload = ParseCollectResponse(raw);
        if (payload?.Candidates is null || payload.Candidates.Count == 0)
        {
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

            var conversationKey = string.IsNullOrWhiteSpace(candidate.ChatKey)
                ? candidate.Title
                : candidate.ChatKey;

            if (!BackfillDedupeRegistry.TryAccept(
                    instance.Id,
                    instance.Platform,
                    conversationKey,
                    candidate.LastMessageBody))
            {
                result.TriageSkippedDuplicate++;
                continue;
            }

            var timestamp = ParseTimestamp(candidate.LastMessageTimestamp);
            MessageAnalyticsService.Instance.RecordBackfillInbound(instance.Id, timestamp, context.SlaThresholdMinutes);
            result.AnalyticsInboundRecorded++;
            result.SlaCandidatesRecorded += CountSlaFromBackfill(timestamp, context.SlaThresholdMinutes);

            var allowAi = context.TryConsumeAiInferenceSlot();
            MessageTriageService.Instance.Enqueue(
                new InboundMessageSelection
                {
                    InstanceId = instance.Id,
                    Platform = instance.Platform,
                    MessageText = candidate.LastMessageBody.Trim(),
                    CustomerName = string.IsNullOrWhiteSpace(candidate.Title) ? "Customer" : candidate.Title.Trim(),
                    ConversationHint = string.IsNullOrWhiteSpace(candidate.Title) ? string.Empty : candidate.Title.Trim(),
                    TimestampUtc = timestamp
                },
                instance.DisplayName,
                allowLlmInference: allowAi,
                skipDedupeCheck: true);

            result.TriageEnqueued++;
        }

        await CommitBaselineAsync(instance.Id, cancellationToken).ConfigureAwait(false);
        return result.ToResult();
    }

    private static async Task CommitBaselineAsync(string instanceId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        await InstanceSessionManager.Instance
            .TryExecuteScriptOnInstanceAsync(instanceId, "window.__umCommitInboundBaseline && window.__umCommitInboundBaseline();")
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

        return DateTimeOffset.TryParse(raw, out var parsed) ? parsed : DateTimeOffset.UtcNow;
    }

    private static WhatsAppBackfillPayload? ParseCollectResponse(string? raw)
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
                return ParseCollectRoot(innerDocument.RootElement);
            }

            return ParseCollectRoot(root);
        }
        catch (JsonException)
        {
            return null;
        }
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

        public BackfillResult ToResult(string? error = null) =>
            new()
            {
                TriageEnqueued = TriageEnqueued,
                TriageSkippedDuplicate = TriageSkippedDuplicate,
                AnalyticsInboundRecorded = AnalyticsInboundRecorded,
                SlaCandidatesRecorded = SlaCandidatesRecorded,
                ErrorMessage = error
            };
    }
}
