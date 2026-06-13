using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services.Ai;

public static class TranscriptBuilder
{
    private const int MaxMessageChars = 800;

    public static string Build(MessageTriageItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var customer = string.IsNullOrWhiteSpace(item.CustomerName) ? "Customer" : item.CustomerName.Trim();
        var message = string.IsNullOrWhiteSpace(item.MessageFullText)
            ? item.MessagePreview
            : item.MessageFullText;

        message = message.Trim();
        if (message.Length > MaxMessageChars)
        {
            message = message[..MaxMessageChars] + "...";
        }

        return $"""
                Customer: {customer}
                Platform: {item.Platform}
                Message: {message}
                """;
    }
}
