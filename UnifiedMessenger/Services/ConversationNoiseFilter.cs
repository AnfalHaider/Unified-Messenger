using System.Text.RegularExpressions;

namespace UnifiedMessenger.Services;

/// <summary>
/// Strips non-conversational boilerplate before local LLM analysis.
/// Mirrors the JavaScript noise filter in conversation-context-scraper.js.
/// </summary>
public static partial class ConversationNoiseFilter
{
    [GeneratedRegex(
        @"messages and calls are end-to-end encrypted|security code changed|this business uses|"
        + @"^hi,?\s*welcome to|automated message|tap to learn more|message yourself",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NoisePattern();

    public static string Strip(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = WhitespacePattern().Replace(text.Trim(), " ");
        return NoisePattern().IsMatch(normalized) ? string.Empty : normalized;
    }

    public static string BuildTranscript(IEnumerable<(string Direction, string Text)> messages)
    {
        if (messages is null)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        foreach (var (direction, text) in messages)
        {
            var cleaned = Strip(text);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                continue;
            }

            var label = direction.Equals("outgoing", StringComparison.OrdinalIgnoreCase)
                ? "outgoing"
                : "incoming";
            lines.Add($"[{label}] {cleaned}");
        }

        return string.Join('\n', lines);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
