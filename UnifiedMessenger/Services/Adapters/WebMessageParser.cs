using System.Text.Json;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Adapters;

internal static class WebMessageParser
{
    public static JsonDocument Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new JsonException("WebMessage payload was empty.");
        }

        var document = JsonDocument.Parse(raw);
        if (document.RootElement.ValueKind == JsonValueKind.String)
        {
            var inner = document.RootElement.GetString();
            document.Dispose();

            if (string.IsNullOrWhiteSpace(inner))
            {
                throw new JsonException("WebMessage payload was an empty JSON string.");
            }

            return JsonDocument.Parse(inner);
        }

        return document;
    }

    internal static bool MatchesInstance(JsonElement root, MessengerInstance instance)
    {
        if (!root.TryGetProperty("instanceId", out var instanceIdElement))
        {
            return true;
        }

        var messageInstanceId = instanceIdElement.GetString();
        return string.IsNullOrWhiteSpace(messageInstanceId) ||
               messageInstanceId.Equals(instance.Id, StringComparison.OrdinalIgnoreCase);
    }

    internal static int ReadNonNegativeInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return 0;
        }

        return element.TryGetInt32(out var value) ? Math.Max(0, value) : 0;
    }

    internal static DateTimeOffset ReadTimestampUtc(JsonElement root, DateTimeOffset fallback)
    {
        if (root.TryGetProperty("timestampUtc", out var timestampElement) &&
            DateTimeOffset.TryParse(timestampElement.GetString(), out var parsed))
        {
            return parsed;
        }

        return fallback;
    }
}
