using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// WhatsApp / WhatsApp Business triage prompts with multi-intent extraction schema.
/// </summary>
public static class AiWhatsAppTriagePromptService
{
    public const string SystemPrompt =
        """
        You are the WhatsApp Business operations AI for a multi-branch beauty salon group in Pakistan.
        Return ONLY one minified JSON object. No markdown, no prose, no code fences.

        Required schema (exact keys, camelCase):
        {
          "customerIntent": "BookingRequest"|"PricingInquiry"|"Complaint"|"General",
          "intentConfidence": 0.0-1.0,
          "subIntent": "BridalUrgent"|"DepositQuery"|"RescheduleRequest"|"ComplaintEscalation"|"None",
          "tags": ["urgent","vip","deposit","bridal","followup"],
          "requestedServices": ["Haircare","Styling","Treatment","Nails","Bridal Makeup"],
          "branchTarget": "DHA-2"|"F-11"|"Men-DHA-2"|"Unknown",
          "sentiment": "Positive"|"Neutral"|"Frustrated",
          "actionableSummary": "<max 15 words, verb-first manager directive>",
          "suggestedDraftResponse": "<professional reply matching draft tone instruction>"
        }

        Rules:
        - customerIntent: BookingRequest for dates/slots/appointments; PricingInquiry for rates/packages; Complaint for service issues; General otherwise.
        - requestedServices: pick from operational services list when mentioned; empty array if unclear.
        - branchTarget: infer from branch reference block or message (DHA, F-11, men's branch); Unknown if not stated.
        - sentiment: Frustrated for angry/urgent negative tone; Positive for thanks/praise; Neutral default.
        - intentConfidence: model certainty for customerIntent (0.0 low, 1.0 high).
        - subIntent: BridalUrgent for same-day/Saturday bridal; DepositQuery for advance payment questions; RescheduleRequest for date changes; ComplaintEscalation for angry repeat issues; None otherwise.
        - tags: short operational labels (urgent, vip, deposit, bridal, followup) — empty array if none apply.
        - actionableSummary: NEVER repeat raw message text. State what the manager must do next.
        - suggestedDraftResponse: follow draft tone instruction; Roman Urdu when client writes in Urdu script or Roman Urdu.
        - Bridal/urgent Saturday booking => BookingRequest, subIntent BridalUrgent, Frustrated if delayed, include hold/deposit language in draft.
        - Voice note transcripts may contain Urdu, Roman Urdu, or English code-switching; infer sentiment from tone words and urgency cues.
        """;

    public static string BuildDraftToneInstruction(DraftTonePreference preference) =>
        AiWhatsAppCopilotPromptService.BuildDraftToneInstruction(preference);

    public const string StrictJsonRetrySuffix =
        "\n\nIMPORTANT: Your previous reply was not valid JSON. Return ONLY the JSON object matching the schema above.";

    public static string BuildUserPrompt(
        RichTriageInferenceJob job,
        WhatsAppConversationMetadata? metadata,
        string? branchKey,
        bool strictJsonRetry = false)
    {
        var operational = WhatsAppOperationalContextBuilder.BuildOperationalReferenceBlock(
            job.InstanceDisplayName,
            branchKey,
            metadata,
            job.ConversationHint,
            job.InstanceId);

        var customer = string.IsNullOrWhiteSpace(job.CustomerName) ? "Customer" : job.CustomerName.Trim();
        var message = ConversationNoiseFilter.CleanForInference(job.MessageText);
        var transcript = string.IsNullOrWhiteSpace(job.ConversationTranscript)
            ? string.Empty
            : job.ConversationTranscript.Trim();
        var retry = strictJsonRetry ? StrictJsonRetrySuffix : string.Empty;
        var tone = BuildDraftToneInstruction(AppSettingsService.Instance.Settings.DraftTonePreference);
        var voiceHint = job.MessageKind == InboundMessageKind.VoiceNote
            ? $"Message source: WhatsApp voice note ({job.VoiceDurationSeconds:F0}s). Transcript confidence: {job.TranscriptConfidence:F2}."
            : string.Empty;

        if (string.IsNullOrWhiteSpace(transcript))
        {
            return $"""
                {operational}
                Draft tone: {tone}
                {voiceHint}
                Customer: {customer}
                Latest WhatsApp message:
                <customer_message>
                {message}
                </customer_message>{retry}
                """;
        }

        return $"""
            {operational}
            Draft tone: {tone}
            {voiceHint}
            Customer: {customer}
            Recent thread:
            {transcript}
            Latest WhatsApp message:
            <customer_message>
            {message}
            </customer_message>{retry}
            """;
    }
}
