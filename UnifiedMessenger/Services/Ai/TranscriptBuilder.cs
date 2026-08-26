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
            message = Truncate(message, MaxMessageChars) + "...";
        }

        return $"""
                Customer: {customer}
                Platform: {item.Platform}
                Message: {message}
                """;
    }

    /// <summary>
    /// Cuts to at most <paramref name="max"/> UTF-16 units without orphaning a surrogate.
    /// </summary>
    /// <remarks>
    /// The same defect the scrapers had (see <c>window.__umTruncate</c> in <c>adapter-core.js</c>): a
    /// range slice cuts by code unit, so an emoji at the boundary leaves a lone high surrogate. Here it
    /// reaches the model as U+FFFD rather than throwing, so it never showed up as a bug — but a prompt
    /// ending in a replacement character is still a prompt with a corrupted last word.
    /// </remarks>
    internal static string Truncate(string value, int max)
    {
        if (max <= 0 || value.Length <= max)
        {
            return value;
        }

        var end = char.IsHighSurrogate(value[max - 1]) ? max - 1 : max;
        return value[..end];
    }
}
