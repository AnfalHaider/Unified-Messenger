using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// WhatsApp-specific hotkey copilot prompts with operational context and tone preferences.
/// </summary>
public static class AiWhatsAppCopilotPromptService
{
    public const string SystemPrompt =
        """
        You are the WhatsApp Business reply copilot for a multi-branch beauty salon group in Pakistan.
        Draft only the reply text the human manager will review and send. No JSON, markdown, code fences, or meta commentary.
        Match the draft tone instruction. Keep replies warm, concise, and salon-professional.
        Use Roman Urdu when the client writes in Urdu script or Roman Urdu.
        For bridal or urgent Saturday bookings, acknowledge urgency and mention hold/deposit when appropriate.
        Never claim a booking is confirmed unless the thread already confirms it.
        """;

    public static AiDraftPromptRequest BuildPrompt(
        string instanceDisplayName,
        string? branchKey,
        string customerName,
        string lastInbound,
        IReadOnlyList<ConversationMessageEntry> messages,
        WhatsAppConversationMetadata? metadata,
        string instanceId,
        string conversationKey)
    {
        var operational = WhatsAppOperationalContextBuilder.BuildOperationalReferenceBlock(
            instanceDisplayName,
            branchKey,
            metadata,
            conversationKey,
            instanceId);

        var tone = BuildDraftToneInstruction(AppSettingsService.Instance.Settings.DraftTonePreference);
        var transcript = BuildTranscriptBlock(messages);
        var customer = string.IsNullOrWhiteSpace(customerName) ? "Customer" : customerName.Trim();
        var fencedInbound = AiDraftPromptService.FenceCustomerMessage(lastInbound);

        var userPrompt = $"""
            {operational}

            Draft tone: {tone}

            Customer: {customer}
            Latest WhatsApp message:
            <customer_message>
            {fencedInbound}
            </customer_message>

            {transcript}

            Draft a reply the human agent can review and send.
            """;

        return new AiDraftPromptRequest
        {
            SystemPrompt = SystemPrompt,
            UserPrompt = userPrompt
        };
    }

    internal static string BuildDraftToneInstruction(DraftTonePreference preference) =>
        preference switch
        {
            DraftTonePreference.Formal =>
                "Formal English. Professional salon tone. Avoid slang and Roman Urdu unless the client used Urdu.",
            DraftTonePreference.RomanUrdu =>
                "Roman Urdu preferred when the client writes in Urdu or Roman Urdu; otherwise warm bilingual English with light Roman Urdu phrases.",
            _ =>
                "Warm and concise. Salon-professional English with empathetic phrasing; Roman Urdu when the client uses Urdu."
        };

    private static string BuildTranscriptBlock(IReadOnlyList<ConversationMessageEntry> messages)
    {
        if (messages.Count == 0)
        {
            return string.Empty;
        }

        var lines = messages
            .TakeLast(8)
            .Select(message => $"{message.Direction}: {message.Text.Trim()}");

        return "Recent transcript:\n" + string.Join("\n", lines);
    }
}
