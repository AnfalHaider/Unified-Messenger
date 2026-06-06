using System.Text;
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

        var userPrompt = AiTriagePromptService.BuildUserPrompt(
            job.InstanceDisplayName,
            job.Platform,
            job.MessageText,
            job.CustomerName,
            job.ConversationHint,
            job.ConversationTranscript,
            strictJsonRetry);

        var builder = new StringBuilder();
        await foreach (var token in OllamaOrchestrationService.Instance
                           .StreamGenerateAsync(
                               userPrompt,
                               AiTriagePromptService.SystemPrompt,
                               responseFormat: "json",
                               priority: InferencePriority.Background,
                               cancellationToken: cancellationToken)
                           .ConfigureAwait(false))
        {
            builder.Append(token);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}

public sealed class MessageTriageInferenceRunner
{
    public static readonly TimeSpan InferenceTimeout = TimeSpan.FromSeconds(90);
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
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(InferenceTimeout);

            var raw = await _llmClient
                .GenerateTriageJsonAsync(job, timeoutCts.Token)
                .ConfigureAwait(false);

            if (TryParseResponse(raw, out var parsed))
            {
                return parsed;
            }

            raw = await _llmClient
                .GenerateTriageJsonAsync(job, timeoutCts.Token, strictJsonRetry: true)
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
        var operationalUrgency = ResolveOperationalUrgency(response);
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

        return new RichTriageLlmResponse
        {
            LegacyUrgencyScore = Math.Clamp(legacyUrgencyScore, 0, 100),
            Sentiment = response.Sentiment,
            CustomerIntent = isSpam ? "Spam" : MapCustomerIntent(intentCategory, response.CustomerIntent),
            ExtractedEntities = response.ExtractedEntities ?? new RichTriageExtractedEntities(),
            CoreSummary = TrimCoreSummary(
                CoalesceSummary(response.CoreSummary, actionableSummary, null)),
            AiIntentCategory = intentCategory,
            ClientSentiment = response.ClientSentiment,
            OperationalUrgency = operationalUrgency,
            EstimatedValue = Math.Max(0, response.EstimatedValue),
            NextActionSummary = actionableSummary,
            ActionableSummary = actionableSummary,
            IsRevenueLeakageRisk = !isSpam && response.IsRevenueLeakageRisk,
            IsSpamOrPromo = isSpam,
            IntentCategory = intentCategory,
            SuggestedAction = suggestedAction
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
            ThreadId = baseline.ThreadId,
            ConversationKey = baseline.ConversationKey,
            BranchName = baseline.BranchName,
            OperationalUrgency = operationalUrgency,
            AiIntentCategory = normalized.AiIntentCategory,
            ClientSentiment = ParseClientSentiment(normalized.ClientSentiment, normalized.Sentiment),
            NextActionSummary = nextAction,
            SuggestedAction = normalized.SuggestedAction,
            IsSpamOrPromo = normalized.IsSpamOrPromo,
            EstimatedValue = Math.Max(0, normalized.EstimatedValue),
            IsRevenueLeakageRisk = normalized.IsRevenueLeakageRisk
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

        return !string.IsNullOrWhiteSpace(
            CoalesceSummary(response.ActionableSummary, response.NextActionSummary, response.CoreSummary));
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
            "booking" => UnifiedMessengerIntentCategory.Booking,
            "complaint" => UnifiedMessengerIntentCategory.Complaint,
            "pricing" or "price_inquiry" or "price inquiry" => UnifiedMessengerIntentCategory.PriceInquiry,
            "lead" => UnifiedMessengerIntentCategory.Lead,
            "spam" => UnifiedMessengerIntentCategory.Spam,
            _ => UnifiedMessengerIntentCategory.Inquiry
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
