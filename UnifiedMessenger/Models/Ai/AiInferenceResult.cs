namespace UnifiedMessenger.Models.Ai;

public sealed class AiInferenceResult
{
    public required string Intent { get; init; }

    public required string NextAction { get; init; }

    public required string SuggestedAction { get; init; }

    public string CoreSummary { get; init; } = string.Empty;

    public static AiInferenceResult? TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            var root = document.RootElement;

            var intent = ReadString(root, "intent");
            var nextAction = ReadString(root, "next_action");
            var suggestedAction = ReadString(root, "suggested_action");

            if (string.IsNullOrWhiteSpace(intent) ||
                string.IsNullOrWhiteSpace(nextAction) ||
                string.IsNullOrWhiteSpace(suggestedAction))
            {
                return null;
            }

            return new AiInferenceResult
            {
                Intent = NormalizeIntent(intent),
                NextAction = Truncate(nextAction, 120),
                SuggestedAction = NormalizeSuggestedAction(suggestedAction),
                CoreSummary = Truncate(nextAction, 220)
            };
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    internal static string NormalizeIntent(string value)
    {
        var normalized = value.Trim().Replace(' ', '_');
        return normalized switch
        {
            "PriceInquiry" or "Price Inquiry" => UnifiedMessengerIntentCategory.PriceInquiry,
            "Booking" => UnifiedMessengerIntentCategory.Booking,
            "Complaint" => UnifiedMessengerIntentCategory.Complaint,
            "Lead" => UnifiedMessengerIntentCategory.Lead,
            "Spam" => UnifiedMessengerIntentCategory.Spam,
            "Inquiry" => UnifiedMessengerIntentCategory.Inquiry,
            _ when normalized.Contains("price", StringComparison.OrdinalIgnoreCase) =>
                UnifiedMessengerIntentCategory.PriceInquiry,
            _ when normalized.Contains("book", StringComparison.OrdinalIgnoreCase) =>
                UnifiedMessengerIntentCategory.Booking,
            _ when normalized.Contains("complain", StringComparison.OrdinalIgnoreCase) =>
                UnifiedMessengerIntentCategory.Complaint,
            _ when normalized.Contains("spam", StringComparison.OrdinalIgnoreCase) =>
                UnifiedMessengerIntentCategory.Spam,
            _ => UnifiedMessengerIntentCategory.Inquiry
        };
    }

    internal static string NormalizeSuggestedAction(string value)
    {
        var normalized = value.Trim().Replace(' ', '_');
        return normalized switch
        {
            "Reply" => "Reply",
            "Escalate" => "Escalate",
            "Ignore" => "Ignore",
            "Follow_up" or "FollowUp" or "Follow-up" => "Follow_up",
            _ => "Reply"
        };
    }

    private static string? ReadString(System.Text.Json.JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..(maxLength - 3)] + "...";
    }
}
