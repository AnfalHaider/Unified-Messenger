using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Services;

public interface ITriageLlmClient
{
    Task<string?> GenerateTriageJsonAsync(
        RichTriageInferenceJob job,
        CancellationToken cancellationToken,
        bool strictJsonRetry = false);
}

public sealed class OllamaTriageLlmClient : ITriageLlmClient
{
    public static OllamaTriageLlmClient Instance { get; } = new();

    public async Task<string?> GenerateTriageJsonAsync(
        RichTriageInferenceJob job,
        CancellationToken cancellationToken,
        bool strictJsonRetry = false)
    {
        if (!AppSettingsService.Instance.Settings.EnableLocalAi)
        {
            return null;
        }

        string userPrompt;
        string systemPrompt;

        if (WhatsAppOperationalContextBuilder.IsWhatsAppPlatform(job.Platform))
        {
            userPrompt = AiWhatsAppTriagePromptService.BuildUserPrompt(
                job,
                job.WhatsAppMetadata,
                job.BranchKey,
                strictJsonRetry);
            systemPrompt = AiWhatsAppTriagePromptService.SystemPrompt;
        }
        else
        {
            userPrompt = AiTriagePromptService.BuildUserPrompt(
                job.InstanceDisplayName,
                job.Platform,
                job.MessageText,
                job.CustomerName,
                job.ConversationHint,
                job.ConversationTranscript,
                strictJsonRetry);
            systemPrompt = AiTriagePromptService.SystemPrompt;
        }

        return await OllamaInferenceCoordinator.Instance
            .CollectGenerateAsync(
                InferencePriority.Background,
                userPrompt,
                systemPrompt,
                responseFormat: "json",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed class MessageTriageInferenceRunner
{
    public static readonly TimeSpan InferenceTimeout = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan StrictRetryTimeout = TimeSpan.FromSeconds(45);
    private const int MaxActionableSummaryWords = 15;

    private readonly ITriageLlmClient _llmClient;

    public MessageTriageInferenceRunner(ITriageLlmClient? llmClient = null)
    {
        _llmClient = llmClient ?? OllamaTriageLlmClient.Instance;
    }

    public async Task<RichTriageLlmResponse?> TryInferAsync(
        RichTriageInferenceJob job,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var firstCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            firstCts.CancelAfter(InferenceTimeout);

            var raw = await _llmClient
                .GenerateTriageJsonAsync(job, firstCts.Token)
                .ConfigureAwait(false);

            if (TryParseResponse(raw, out var parsed))
            {
                return parsed;
            }

            using var retryCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            retryCts.CancelAfter(StrictRetryTimeout);

            raw = await _llmClient
                .GenerateTriageJsonAsync(job, retryCts.Token, strictJsonRetry: true)
                .ConfigureAwait(false);

            return TryParseResponse(raw, out parsed) ? parsed : null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Rich triage inference failed: {ex.Message}");
            return null;
        }
    }

    internal static bool TryParseResponse(string? raw, out RichTriageLlmResponse? response)
    {
        response = null;
        if (string.IsNullOrWhiteSpace(raw) ||
            !JsonRepairUtility.TryDeserialize<RichTriageLlmResponse>(raw, out var parsed) ||
            parsed is null)
        {
            return false;
        }

        response = NormalizeSchema(parsed);
        return HasActionableContent(response);
    }

    internal static RichTriageLlmResponse NormalizeSchema(RichTriageLlmResponse response)
    {
        var operationalUrgency = ApplyWhatsAppSubIntentBoost(response, ResolveOperationalUrgency(response));
        var intentCategory = ResolveIntentCategory(response);
        var isSpam = response.IsSpamOrPromo ||
                     intentCategory.Equals(UnifiedMessengerIntentCategory.Spam, StringComparison.OrdinalIgnoreCase) ||
                     ConversationNoiseFilter.IsPromoSpam(response.ActionableSummary) ||
                     ConversationNoiseFilter.IsPromoSpam(response.NextActionSummary);
        var actionableSummary = TrimActionableSummary(
            CoalesceSummary(response.ActionableSummary, response.NextActionSummary, response.CoreSummary));
        var suggestedAction = string.IsNullOrWhiteSpace(response.SuggestedAction)
            ? isSpam ? "Ignore" : string.Empty
            : response.SuggestedAction.Trim();
        var legacyUrgencyScore = response.LegacyUrgencyScore > 0
            ? response.LegacyUrgencyScore
            : operationalUrgency * 20;

        var clientSentiment = ResolveClientSentiment(response);
        var services = response.RequestedServices?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        var tags = response.Tags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        return new RichTriageLlmResponse
        {
            LegacyUrgencyScore = Math.Clamp(legacyUrgencyScore, 0, 100),
            Sentiment = response.Sentiment,
            CustomerIntent = isSpam ? "Spam" : MapCustomerIntent(intentCategory, response.CustomerIntent),
            ExtractedEntities = EnrichEntities(response.ExtractedEntities, services),
            CoreSummary = TrimCoreSummary(
                CoalesceSummary(response.CoreSummary, actionableSummary, null)),
            AiIntentCategory = intentCategory,
            ClientSentiment = clientSentiment,
            OperationalUrgency = operationalUrgency,
            EstimatedValue = Math.Max(0, response.EstimatedValue),
            NextActionSummary = actionableSummary,
            ActionableSummary = actionableSummary,
            IsRevenueLeakageRisk = !isSpam && response.IsRevenueLeakageRisk,
            IsSpamOrPromo = isSpam,
            IntentCategory = intentCategory,
            SuggestedAction = suggestedAction,
            RequestedServices = services,
            BranchTarget = string.IsNullOrWhiteSpace(response.BranchTarget)
                ? string.Empty
                : response.BranchTarget.Trim(),
            SuggestedDraftResponse = string.IsNullOrWhiteSpace(response.SuggestedDraftResponse)
                ? string.Empty
                : response.SuggestedDraftResponse.Trim(),
            IntentConfidence = Math.Clamp(response.IntentConfidence, 0, 1),
            SubIntent = string.IsNullOrWhiteSpace(response.SubIntent)
                ? string.Empty
                : response.SubIntent.Trim(),
            Tags = tags
        };
    }

    public static MessageTriageItem ApplyInference(
        MessageTriageItem baseline,
        RichTriageLlmResponse response)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(response);

        var normalized = NormalizeSchema(response);
        var entities = normalized.ExtractedEntities ?? new RichTriageExtractedEntities();
        var customerName = string.IsNullOrWhiteSpace(entities.CustomerName)
            ? baseline.CustomerName
            : entities.CustomerName.Trim();
        var conversationKey = string.IsNullOrWhiteSpace(baseline.ConversationKey)
            ? customerName
            : baseline.ConversationKey;
        var threadId = ConversationKeyResolver.BuildThreadId(baseline.InstanceId, conversationKey);
        var operationalUrgency = normalized.OperationalUrgency;
        var nextAction = TrimActionableSummary(
            CoalesceSummary(normalized.ActionableSummary, normalized.NextActionSummary, normalized.CoreSummary));

        return new MessageTriageItem
        {
            Id = baseline.Id,
            InstanceId = baseline.InstanceId,
            InstanceDisplayName = baseline.InstanceDisplayName,
            Platform = baseline.Platform,
            MessagePreview = baseline.MessagePreview,
            MessageFullText = baseline.MessageFullText,
            CustomerName = customerName,
            UrgencyScore = Math.Clamp(normalized.LegacyUrgencyScore > 0
                ? normalized.LegacyUrgencyScore
                : operationalUrgency * 20, 0, 100),
            Sentiment = ParseSentiment(normalized.Sentiment, baseline.Sentiment),
            TimestampUtc = baseline.TimestampUtc,
            InferenceSource = TriageInferenceSource.LocalAi,
            CustomerIntent = ParseCustomerIntent(normalized.CustomerIntent),
            CoreSummary = TrimCoreSummary(
                CoalesceSummary(normalized.CoreSummary, nextAction, null)),
            ExtractedEntities = entities,
            ThreadId = threadId,
            ConversationKey = conversationKey,
            BranchName = baseline.BranchName,
            OperationalUrgency = operationalUrgency,
            AiIntentCategory = normalized.AiIntentCategory,
            ClientSentiment = ParseClientSentiment(normalized.ClientSentiment, normalized.Sentiment),
            NextActionSummary = nextAction,
            SuggestedAction = normalized.SuggestedAction,
            IsSpamOrPromo = normalized.IsSpamOrPromo,
            EstimatedValue = Math.Max(0, normalized.EstimatedValue),
            IsRevenueLeakageRisk = normalized.IsRevenueLeakageRisk,
            SuggestedDraftResponse = normalized.SuggestedDraftResponse,
            RequestedServices = normalized.RequestedServices,
            BranchTarget = normalized.BranchTarget,
            SubIntent = normalized.SubIntent,
            IntentTags = normalized.Tags,
            IntentConfidence = normalized.IntentConfidence,
            MessageKind = baseline.MessageKind,
            VoiceDurationSeconds = baseline.VoiceDurationSeconds,
            TranscriptConfidence = baseline.TranscriptConfidence
        };
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

    internal static string ParseAiIntentCategory(string? raw, string? customerIntentFallback)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return MapIntentCategory(raw);
        }

        return ParseCustomerIntent(customerIntentFallback) switch
        {
            CustomerIntent.Booking => UnifiedMessengerIntentCategory.Booking,
            CustomerIntent.Complaint => UnifiedMessengerIntentCategory.Complaint,
            CustomerIntent.Spam => UnifiedMessengerIntentCategory.Spam,
            _ => UnifiedMessengerIntentCategory.Inquiry
        };
    }

    internal static string ParseClientSentiment(string? raw, string? legacySentiment)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw.Trim() switch
            {
                "Positive" => ClientSentimentLabel.Positive,
                "Neutral" => ClientSentimentLabel.Neutral,
                "Frustrated" => ClientSentimentLabel.Frustrated,
                "Critical" => ClientSentimentLabel.Critical,
                _ => raw.Trim()
            };
        }

        return legacySentiment?.Trim().ToLowerInvariant() switch
        {
            "positive" => ClientSentimentLabel.Positive,
            "negative" => ClientSentimentLabel.Frustrated,
            _ => ClientSentimentLabel.Neutral
        };
    }

    internal static string TrimNextActionSummary(string? nextAction, string? coreSummary) =>
        TrimActionableSummary(CoalesceSummary(nextAction, coreSummary, null));

    internal static MessageSentiment ParseSentiment(string? raw, MessageSentiment fallback) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "positive" => MessageSentiment.Positive,
            "negative" => MessageSentiment.Negative,
            "neutral" => MessageSentiment.Neutral,
            _ => fallback
        };

    internal static CustomerIntent ParseCustomerIntent(string? raw) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "booking" => CustomerIntent.Booking,
            "complaint" => CustomerIntent.Complaint,
            "spam" => CustomerIntent.Spam,
            _ => CustomerIntent.Inquiry
        };

    internal static string TrimCoreSummary(string? summary) => TrimActionableSummary(summary);

    private static bool HasActionableContent(RichTriageLlmResponse response)
    {
        if (response.IsSpamOrPromo)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(response.SuggestedDraftResponse))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(
            CoalesceSummary(response.ActionableSummary, response.NextActionSummary, response.CoreSummary));
    }

    internal static int ApplyWhatsAppSubIntentBoost(RichTriageLlmResponse response, int urgency)
    {
        if (string.IsNullOrWhiteSpace(response.SubIntent))
        {
            return urgency;
        }

        return response.SubIntent.Trim().ToLowerInvariant() switch
        {
            "bridalurgent" or "complaintescalation" => Math.Max(urgency, 5),
            "reschedulerequest" => Math.Max(urgency, 4),
            "depositquery" => Math.Max(urgency, 3),
            _ => urgency
        };
    }

    private static int ResolveOperationalUrgency(RichTriageLlmResponse response)
    {
        if (response.OperationalUrgency is >= 1 and <= 5)
        {
            return response.OperationalUrgency;
        }

        if (response.LegacyOperationalUrgency is >= 1 and <= 5)
        {
            return response.LegacyOperationalUrgency;
        }

        if (response.LegacyUrgencyScore is >= 1 and <= 5)
        {
            return response.LegacyUrgencyScore;
        }

        return response.LegacyUrgencyScore > 0
            ? MapOperationalUrgency(response.LegacyUrgencyScore)
            : 1;
    }

    private static string ResolveIntentCategory(RichTriageLlmResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.IntentCategory))
        {
            return MapIntentCategory(response.IntentCategory);
        }

        if (!string.IsNullOrWhiteSpace(response.CustomerIntentSchema))
        {
            return MapIntentCategory(response.CustomerIntentSchema);
        }

        if (!string.IsNullOrWhiteSpace(response.AiIntentCategory))
        {
            return MapIntentCategory(response.AiIntentCategory);
        }

        return MapIntentCategory(response.CustomerIntent);
    }

    private static string MapIntentCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return UnifiedMessengerIntentCategory.Inquiry;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "booking" or "bookingrequest" => UnifiedMessengerIntentCategory.Booking,
            "complaint" => UnifiedMessengerIntentCategory.Complaint,
            "pricing" or "price_inquiry" or "price inquiry" or "pricinginquiry" => UnifiedMessengerIntentCategory.PriceInquiry,
            "lead" => UnifiedMessengerIntentCategory.Lead,
            "spam" => UnifiedMessengerIntentCategory.Spam,
            "general" or "inquiry" => UnifiedMessengerIntentCategory.Inquiry,
            _ => UnifiedMessengerIntentCategory.Inquiry
        };
    }

    private static string ResolveClientSentiment(RichTriageLlmResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.ClientSentiment))
        {
            return ParseClientSentiment(response.ClientSentiment, response.Sentiment);
        }

        var sentimentRaw = !string.IsNullOrWhiteSpace(response.SentimentSchema)
            ? response.SentimentSchema
            : response.Sentiment;

        if (!string.IsNullOrWhiteSpace(sentimentRaw))
        {
            return sentimentRaw.Trim() switch
            {
                "Frustrated" => ClientSentimentLabel.Frustrated,
                "Positive" => ClientSentimentLabel.Positive,
                "Neutral" => ClientSentimentLabel.Neutral,
                _ => ParseClientSentiment(sentimentRaw, null)
            };
        }

        return ClientSentimentLabel.Neutral;
    }

    private static RichTriageExtractedEntities EnrichEntities(
        RichTriageExtractedEntities? entities,
        IReadOnlyList<string> requestedServices)
    {
        var baseEntities = entities ?? new RichTriageExtractedEntities();
        if (requestedServices.Count == 0)
        {
            return baseEntities;
        }

        var serviceType = string.IsNullOrWhiteSpace(baseEntities.ServiceType)
            ? string.Join(", ", requestedServices)
            : baseEntities.ServiceType;

        return new RichTriageExtractedEntities
        {
            CustomerName = baseEntities.CustomerName,
            ContactNumber = baseEntities.ContactNumber,
            RequestedDate = baseEntities.RequestedDate,
            RequestedTime = baseEntities.RequestedTime,
            ServiceType = serviceType,
            ActionRequired = baseEntities.ActionRequired
        };
    }

    private static string MapCustomerIntent(string intentCategory, string? fallback) =>
        intentCategory.Equals(UnifiedMessengerIntentCategory.Spam, StringComparison.OrdinalIgnoreCase)
            ? "Spam"
            : fallback ?? "Inquiry";

    private static string CoalesceSummary(string? primary, string? secondary, string? tertiary)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(secondary))
        {
            return secondary.Trim();
        }

        return string.IsNullOrWhiteSpace(tertiary) ? string.Empty : tertiary.Trim();
    }

    private static string TrimActionableSummary(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return string.Empty;
        }

        var words = summary.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= MaxActionableSummaryWords
            ? summary.Trim()
            : string.Join(' ', words.Take(MaxActionableSummaryWords));
    }
}
