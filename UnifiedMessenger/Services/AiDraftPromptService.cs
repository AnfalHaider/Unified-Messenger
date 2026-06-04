using System.Text.Json;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

public sealed class AiDraftPromptRequest
{
    public required string SystemPrompt { get; init; }

    public required string UserPrompt { get; init; }
}

public static class AiDraftPromptService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string DefaultSystemPrompt =
        "You are a professional customer-service assistant. Draft a concise, empathetic reply. Do not mention AI.";

    public static AiDraftPromptRequest BuildPrompt(
        string platform,
        string messageText,
        string? customerName = null,
        string? conversationHint = null)
    {
        var document = LoadDocument();
        var platformKey = PlatformDefinition.NormalizePlatformId(platform);
        var platformPrompt = document.Platforms.TryGetValue(platformKey, out var entry) ? entry : null;

        var systemPrompt = platformPrompt?.SystemPrompt
            ?? document.DefaultSystemPrompt
            ?? DefaultSystemPrompt;

        var prefix = platformPrompt?.UserPrefix ?? "Customer message:";
        var customer = string.IsNullOrWhiteSpace(customerName) ? "Customer" : customerName.Trim();
        var hint = string.IsNullOrWhiteSpace(conversationHint) ? string.Empty : $"\nContext: {conversationHint.Trim()}";

        var userPrompt =
            $"{prefix}\nFrom: {customer}\nMessage: {messageText.Trim()}{hint}\n\nDraft a reply the human agent can review and send.";

        return new AiDraftPromptRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt
        };
    }

    private static AiDraftPromptDocument LoadDocument()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Config", "ai-draft-prompts.json");
            if (!File.Exists(path))
            {
                return new AiDraftPromptDocument();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AiDraftPromptDocument>(json, JsonOptions)
                ?? new AiDraftPromptDocument();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load AI draft prompts: {ex.Message}");
            return new AiDraftPromptDocument();
        }
    }

    private sealed class AiDraftPromptDocument
    {
        public string? DefaultSystemPrompt { get; init; }

        public Dictionary<string, AiDraftPlatformPrompt> Platforms { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class AiDraftPlatformPrompt
    {
        public string? SystemPrompt { get; init; }

        public string? UserPrefix { get; init; }
    }
}
