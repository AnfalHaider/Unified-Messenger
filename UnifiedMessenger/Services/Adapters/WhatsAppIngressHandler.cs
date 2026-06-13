using System.Text.Json;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services.Backfill;

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

        if (AdapterMessageTypes.WhatsAppHistoryChunk.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            HandleHistoryChunk(root, instance);
            return true;
        }

        if (AdapterMessageTypes.WhatsAppSidebarSnapshot.Equals(type, StringComparison.OrdinalIgnoreCase))
        {
            HandleSidebarSnapshot(root, instance);
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
        var receivedKind = ParseMessageKind(ReadOptionalString(root, "lastReceivedKind"));
        var customerName = ReadOptionalString(root, "customerName") ?? string.Empty;

        WhatsAppBusinessContextService.Instance.UpsertThreadContext(new WhatsAppThreadContextSnapshot
        {
            InstanceId = instance.Id,
            ConversationKey = conversationKey,
            CustomerName = customerName,
            BusinessLabels = ReadStringArray(root, "businessLabels"),
            VerifiedBusinessName = ReadOptionalString(root, "verifiedBusinessName"),
            ProfilePhoneNumber = ReadOptionalString(root, "profilePhoneNumber"),
            ContactPhoneNumber = ReadOptionalString(root, "contactPhoneNumber"),
            CapturedAtUtc = capturedAt
        });

        // Telemetry reflects the active conversation snapshot, not discrete new messages.
        // Analytics increments are handled by inbound previews, outgoing monitors, and delivery status.
        if (lastReceivedAt is not null)
        {
            ThreadRegistryService.Instance.UpdateLastMessageKind(
                instance.Id,
                conversationKey,
                receivedKind,
                lastReceivedAt.Value);
        }
    }

    private static void HandleSidebarSnapshot(JsonElement root, MessengerInstance instance)
    {
        if (!root.TryGetProperty("rows", out var rowsElement) || rowsElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var row in rowsElement.EnumerateArray())
        {
            var title = ReadOptionalString(row, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var conversationKey = ReadOptionalString(row, "conversationKey") ?? title;
            var capturedAt = WebMessageParser.ReadTimestampUtc(row, DateTimeOffset.UtcNow);

            WhatsAppBusinessContextService.Instance.UpsertThreadContext(new WhatsAppThreadContextSnapshot
            {
                InstanceId = instance.Id,
                ConversationKey = conversationKey,
                CustomerName = title,
                CapturedAtUtc = capturedAt
            });
        }
    }

    private static void HandleHistoryChunk(JsonElement root, MessengerInstance instance)
    {
        var conversationKey = ReadOptionalString(root, "conversationKey");
        var body = ReadOptionalString(root, "body");
        if (string.IsNullOrWhiteSpace(conversationKey) || string.IsNullOrWhiteSpace(body) || body.Trim().Length < 8)
        {
            return;
        }

        if (ReadOptionalBool(root, "isOutgoing"))
        {
            return;
        }

        var timestamp = WebMessageParser.ReadTimestampUtc(root, DateTimeOffset.UtcNow);
        var customerName = ReadOptionalString(root, "customerName") ?? "Customer";

        if (!BackfillDedupeRegistry.TryAccept(instance.Id, instance.Platform, conversationKey, body))
        {
            return;
        }

        MessageAnalyticsService.Instance.RecordBackfillInbound(
            instance.Id,
            timestamp,
            AppSettingsService.Instance.Settings.SlaThresholdMinutes);

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
            allowLlmInference: false);
    }

    private static bool ReadOptionalBool(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return false;
        }

        return element.ValueKind == JsonValueKind.True;
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
