using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.VoiceNotes;

namespace UnifiedMessenger.Services.Adapters;

internal static class WhatsAppIngressHandler
{
    public static bool TryHandle(string? type, JsonElement root, MessengerInstance instance)
    {
        if (AdapterMessageTypes.WhatsAppThreadContext.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            HandleThreadContext(root, instance);
            return true;
        }

        if (AdapterMessageTypes.WhatsAppOutgoingStatus.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            HandleOutgoingStatus(root, instance);
            return true;
        }

        if (AdapterMessageTypes.WhatsAppVoicePayload.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            HandleVoicePayload(root, instance);
            return true;
        }

        return false;
    }

    public static InboundMessageSelection BuildInboundSelection(
        JsonElement root,
        MessengerInstance instance,
        string resolvedKey,
        string customerName,
        string messageText,
        DateTimeOffset timestamp)
    {
        var labels = ReadStringArray(root, "businessLabels");
        var verified = ReadOptionalString(root, "verifiedBusinessName");
        var profilePhone = ReadOptionalString(root, "profilePhoneNumber");
        var contactPhone = ReadOptionalString(root, "contactPhoneNumber");

        var metadata = new WhatsAppConversationMetadata
        {
            BusinessLabels = labels,
            VerifiedBusinessName = verified,
            ProfilePhoneNumber = profilePhone,
            ContactPhoneNumber = contactPhone,
            ChatJid = resolvedKey
        };

        WhatsAppBusinessContextService.Instance.UpsertThreadContext(new WhatsAppThreadContextSnapshot
        {
            InstanceId = instance.Id,
            ConversationKey = resolvedKey,
            CustomerName = customerName,
            BusinessLabels = labels,
            VerifiedBusinessName = verified,
            ProfilePhoneNumber = profilePhone,
            ContactPhoneNumber = contactPhone,
            CapturedAtUtc = timestamp
        });

        return new InboundMessageSelection
        {
            InstanceId = instance.Id,
            Platform = instance.Platform,
            MessageText = messageText,
            CustomerName = customerName,
            ConversationKey = resolvedKey,
            ConversationHint = root.TryGetProperty("conversationHint", out var hintElement)
                ? hintElement.GetString() ?? string.Empty
                : string.Empty,
            TimestampUtc = timestamp,
            BusinessLabels = labels,
            VerifiedBusinessName = verified,
            ProfilePhoneNumber = profilePhone,
            ContactPhoneNumber = contactPhone
        };
    }

    private static void HandleThreadContext(JsonElement root, MessengerInstance instance)
    {
        var conversationKey = ReadOptionalString(root, "conversationKey");
        if (string.IsNullOrWhiteSpace(conversationKey))
        {
            return;
        }

        var labels = ReadStringArray(root, "businessLabels");
        WhatsAppBusinessContextService.Instance.UpsertThreadContext(new WhatsAppThreadContextSnapshot
        {
            InstanceId = instance.Id,
            ConversationKey = conversationKey,
            CustomerName = ReadOptionalString(root, "customerName") ?? string.Empty,
            BusinessLabels = labels,
            VerifiedBusinessName = ReadOptionalString(root, "verifiedBusinessName"),
            ProfilePhoneNumber = ReadOptionalString(root, "profilePhoneNumber"),
            ContactPhoneNumber = ReadOptionalString(root, "contactPhoneNumber"),
            CapturedAtUtc = WebMessageParser.ReadTimestampUtc(root, DateTimeOffset.UtcNow)
        });
    }

    public static WhatsAppVoiceNotePayload? TryParseVoicePayload(JsonElement root, MessengerInstance instance)
    {
        var conversationKey = ReadOptionalString(root, "conversationKey");
        var audioBase64 = ReadOptionalString(root, "audioBase64");
        if (string.IsNullOrWhiteSpace(conversationKey) || string.IsNullOrWhiteSpace(audioBase64))
        {
            return null;
        }

        var duration = root.TryGetProperty("durationSeconds", out var durationElement) &&
                       durationElement.TryGetDouble(out var durationValue)
            ? durationValue
            : 0;

        return new WhatsAppVoiceNotePayload
        {
            InstanceId = instance.Id,
            Platform = instance.Platform,
            ConversationKey = conversationKey.Trim(),
            CustomerName = ReadOptionalString(root, "customerName") ?? "Customer",
            DurationSeconds = duration,
            MimeType = ReadOptionalString(root, "mimeType") ?? "audio/ogg",
            AudioBase64 = audioBase64,
            TimestampUtc = WebMessageParser.ReadTimestampUtc(root, DateTimeOffset.UtcNow),
            BusinessLabels = ReadStringArray(root, "businessLabels"),
            VerifiedBusinessName = ReadOptionalString(root, "verifiedBusinessName"),
            ProfilePhoneNumber = ReadOptionalString(root, "profilePhoneNumber"),
            ContactPhoneNumber = ReadOptionalString(root, "contactPhoneNumber")
        };
    }

    public static InboundMessageSelection BuildVoiceInboundSelection(
        WhatsAppVoiceNotePayload payload,
        MessengerInstance instance,
        string messageText,
        double transcriptConfidence)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            conversationKey = payload.ConversationKey,
            customerName = payload.CustomerName,
            businessLabels = payload.BusinessLabels,
            verifiedBusinessName = payload.VerifiedBusinessName,
            profilePhoneNumber = payload.ProfilePhoneNumber,
            contactPhoneNumber = payload.ContactPhoneNumber
        }));

        var selection = BuildInboundSelection(
            document.RootElement,
            instance,
            payload.ConversationKey,
            payload.CustomerName,
            messageText,
            payload.TimestampUtc);

        return new InboundMessageSelection
        {
            InstanceId = selection.InstanceId,
            Platform = selection.Platform,
            MessageText = selection.MessageText,
            CustomerName = selection.CustomerName,
            ConversationHint = selection.ConversationHint,
            ConversationKey = selection.ConversationKey,
            TimestampUtc = selection.TimestampUtc,
            BusinessLabels = selection.BusinessLabels,
            VerifiedBusinessName = selection.VerifiedBusinessName,
            ProfilePhoneNumber = selection.ProfilePhoneNumber,
            ContactPhoneNumber = selection.ContactPhoneNumber,
            MessageKind = InboundMessageKind.VoiceNote,
            VoiceDurationSeconds = payload.DurationSeconds,
            TranscriptConfidence = transcriptConfidence
        };
    }

    private static void HandleVoicePayload(JsonElement root, MessengerInstance instance)
    {
        if (TryParseVoicePayload(root, instance) is not { } payload)
        {
            return;
        }

        WhatsAppBusinessContextService.Instance.UpsertThreadContext(new WhatsAppThreadContextSnapshot
        {
            InstanceId = instance.Id,
            ConversationKey = payload.ConversationKey,
            CustomerName = payload.CustomerName,
            BusinessLabels = payload.BusinessLabels,
            VerifiedBusinessName = payload.VerifiedBusinessName,
            ProfilePhoneNumber = payload.ProfilePhoneNumber,
            ContactPhoneNumber = payload.ContactPhoneNumber,
            CapturedAtUtc = payload.TimestampUtc,
            LastVoiceNoteAtUtc = payload.TimestampUtc
        });

        VoiceNotePipelineService.Instance.Enqueue(payload);
    }

    private static void HandleOutgoingStatus(JsonElement root, MessengerInstance instance)
    {
        var conversationKey = ReadOptionalString(root, "conversationKey");
        var status = ReadOptionalString(root, "deliveryStatus");
        if (string.IsNullOrWhiteSpace(conversationKey) || string.IsNullOrWhiteSpace(status))
        {
            return;
        }

        var timestamp = WebMessageParser.ReadTimestampUtc(root, DateTimeOffset.UtcNow);
        var normalizedStatus = WhatsAppDeliveryStatusLabel.Normalize(status);
        WhatsAppBusinessContextService.Instance.RecordOutgoingStatus(new WhatsAppOutgoingStatusEvent
        {
            InstanceId = instance.Id,
            ConversationKey = conversationKey,
            DeliveryStatus = normalizedStatus,
            MessagePreview = ReadOptionalString(root, "messagePreview"),
            TimestampUtc = timestamp
        });

        ThreadRegistryService.Instance.UpdateWhatsAppDeliveryStatus(
            instance.Id,
            conversationKey,
            normalizedStatus,
            timestamp);

        if (normalizedStatus.Equals(WhatsAppDeliveryStatusLabel.Delivered, StringComparison.Ordinal) ||
            normalizedStatus.Equals(WhatsAppDeliveryStatusLabel.Read, StringComparison.Ordinal))
        {
            MessageAnalyticsService.Instance.RecordMessageSent(
                instance.Id,
                conversationKey: conversationKey);
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element.EnumerateArray()
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ReadOptionalString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var element)
            ? element.GetString()
            : null;
}
