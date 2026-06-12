using System.Text.Json;
using UnifiedMessenger.Models;

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

        if (AdapterMessageTypes.WhatsAppTelemetry.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            HandleTelemetry(root, instance);
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

    private static void HandleTelemetry(JsonElement root, MessengerInstance instance)
    {
        var conversationKey = ReadOptionalString(root, "conversationKey");
        if (string.IsNullOrWhiteSpace(conversationKey))
        {
            return;
        }

        var capturedAt = WebMessageParser.ReadTimestampUtc(root, DateTimeOffset.UtcNow);
        var lastReceivedAt = ReadOptionalTimestamp(root, "lastReceivedAtUtc");
        var lastSentAt = ReadOptionalTimestamp(root, "lastSentAtUtc");
        var receivedKind = ParseMessageKind(ReadOptionalString(root, "lastReceivedKind"));
        var sentKind = ParseMessageKind(ReadOptionalString(root, "lastSentKind"));

        var payload = new WhatsAppTelemetryPayload
        {
            InstanceId = instance.Id,
            ConversationKey = conversationKey,
            CustomerName = ReadOptionalString(root, "customerName") ?? string.Empty,
            ContactPhoneNumber = ReadOptionalString(root, "contactPhoneNumber"),
            ProfilePhoneNumber = ReadOptionalString(root, "profilePhoneNumber"),
            LastReceivedAtUtc = lastReceivedAt,
            LastSentAtUtc = lastSentAt,
            LastReceivedKind = receivedKind,
            LastSentKind = sentKind,
            ActiveMessagePreview = ReadOptionalString(root, "activeMessagePreview"),
            CapturedAtUtc = capturedAt
        };

        WhatsAppBusinessContextService.Instance.UpsertThreadContext(new WhatsAppThreadContextSnapshot
        {
            InstanceId = instance.Id,
            ConversationKey = conversationKey,
            CustomerName = payload.CustomerName,
            BusinessLabels = ReadStringArray(root, "businessLabels"),
            VerifiedBusinessName = ReadOptionalString(root, "verifiedBusinessName"),
            ProfilePhoneNumber = payload.ProfilePhoneNumber,
            ContactPhoneNumber = payload.ContactPhoneNumber,
            CapturedAtUtc = capturedAt
        });

        if (lastReceivedAt is not null)
        {
            MessageAnalyticsService.Instance.RecordMessageReceived(
                instance.Id,
                conversationKey,
                lastReceivedAt);
            ThreadRegistryService.Instance.UpdateLastMessageKind(
                instance.Id,
                conversationKey,
                receivedKind,
                lastReceivedAt.Value);
        }

        if (lastSentAt is not null)
        {
            MessageAnalyticsService.Instance.RecordMessageSent(
                instance.Id,
                chatHint: payload.CustomerName,
                conversationKey: conversationKey,
                sentAtUtc: lastSentAt);
        }
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

    private static DateTimeOffset? ReadOptionalTimestamp(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(element.GetString(), out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return null;
    }

    internal static InboundMessageKind ParseMessageKind(string? raw) =>
        (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "image" => InboundMessageKind.Image,
            "audio" => InboundMessageKind.Audio,
            "catalog" => InboundMessageKind.Catalog,
            "booking" => InboundMessageKind.Booking,
            "voice" or "voicenote" => InboundMessageKind.VoiceNote,
            _ => InboundMessageKind.Text
        };
}
