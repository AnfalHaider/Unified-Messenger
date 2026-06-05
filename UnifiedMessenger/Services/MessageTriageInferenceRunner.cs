using System.Text;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Ollama;

namespace UnifiedMessenger.Services;

public interface ITriageLlmClient
{
    Task<string?> GenerateTriageJsonAsync(RichTriageInferenceJob job, CancellationToken cancellationToken);
}

public sealed class OllamaTriageLlmClient : ITriageLlmClient
{
    public static OllamaTriageLlmClient Instance { get; } = new();

    public async Task<string?> GenerateTriageJsonAsync(
        RichTriageInferenceJob job,
        CancellationToken cancellationToken)
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
            job.ConversationTranscript);

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

            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return JsonRepairUtility.TryDeserialize<RichTriageLlmResponse>(raw, out var parsed)
                ? parsed
                : null;
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

    public static MessageTriageItem ApplyInference(
        MessageTriageItem baseline,
        RichTriageLlmResponse response)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(response);

        var entities = response.ExtractedEntities ?? new RichTriageExtractedEntities();
        var customerName = string.IsNullOrWhiteSpace(entities.CustomerName)
            ? baseline.CustomerName
            : entities.CustomerName.Trim();

        return new MessageTriageItem
        {
            Id = baseline.Id,
            InstanceId = baseline.InstanceId,
            InstanceDisplayName = baseline.InstanceDisplayName,
            Platform = baseline.Platform,
            MessagePreview = baseline.MessagePreview,
            CustomerName = customerName,
            UrgencyScore = Math.Clamp(response.UrgencyScore, 0, 100),
            Sentiment = ParseSentiment(response.Sentiment, baseline.Sentiment),
            TimestampUtc = baseline.TimestampUtc,
            InferenceSource = TriageInferenceSource.LocalAi,
            CustomerIntent = ParseCustomerIntent(response.CustomerIntent),
            CoreSummary = TrimCoreSummary(response.CoreSummary),
            ExtractedEntities = entities,
            ThreadId = baseline.ThreadId,
            ConversationKey = baseline.ConversationKey,
            BranchName = baseline.BranchName,
            OperationalUrgency = Math.Clamp(
                response.OperationalUrgency > 0
                    ? response.OperationalUrgency
                    : MapOperationalUrgency(response.UrgencyScore),
                1,
                5),
            AiIntentCategory = ParseAiIntentCategory(response.AiIntentCategory, response.CustomerIntent),
            ClientSentiment = ParseClientSentiment(response.ClientSentiment, response.Sentiment),
            NextActionSummary = TrimNextActionSummary(response.NextActionSummary, response.CoreSummary),
            EstimatedValue = Math.Max(0, response.EstimatedValue),
            IsRevenueLeakageRisk = response.IsRevenueLeakageRisk
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
            return raw.Trim();
        }

        return ParseCustomerIntent(customerIntentFallback) switch
        {
            CustomerIntent.Booking => UnifiedMessengerIntentCategory.Booking,
            CustomerIntent.Complaint => UnifiedMessengerIntentCategory.Complaint,
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

    internal static string TrimNextActionSummary(string? nextAction, string? coreSummary)
    {
        if (!string.IsNullOrWhiteSpace(nextAction))
        {
            return nextAction.Trim();
        }

        return TrimCoreSummary(coreSummary);
    }

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

    internal static string TrimCoreSummary(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return string.Empty;
        }

        var words = summary.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= 10
            ? summary.Trim()
            : string.Join(' ', words.Take(10));
    }
}
