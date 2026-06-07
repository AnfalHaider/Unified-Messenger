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
        + @"^hi,?\s*welcome to|automated message|tap to learn more|message yourself|"
        + @"waiting for this message|^online$|^typing\.\.\.$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NoisePattern();

    [GeneratedRegex(
        @"^(assalam+u?a?l?a?ikum|salam|salaam|hi|hello|hey|good\s+(morning|afternoon|evening))[\s!.?]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GreetingOnlyPattern();

    [GeneratedRegex(
        @"custom foldable promo cards|perfect for packaging|mini campaign|we create custom|"
        + @"bulk (order|pricing|discount)|limited time offer|click here to (buy|order|subscribe)|"
        + @"\bunsubscribe\b|promotional (message|offer)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PromoSpamPattern();

    [GeneratedRegex(@"\b\d{1,2}:\d{2}(\s?[AP]M)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InlineTimestampPattern();

    [GeneratedRegex(@"^\d{1,2}:\d{2}(\s?[AP]M)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimestampTokenPattern();

    [GeneratedRegex(@"^[\w\s.+@]{1,48}:\s*", RegexOptions.CultureInvariant)]
    private static partial Regex SenderPrefixPattern();

    [GeneratedRegex(
        @"^(assalam+u?a?l?a?ikum|salam|salaam|hi|hello|hey|good\s+(morning|afternoon|evening))[\s!.?,]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingGreetingPattern();

    public static string CleanForInference(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = WhitespacePattern().Replace(text.Trim(), " ");
        normalized = InlineTimestampPattern().Replace(normalized, " ");
        normalized = WhitespacePattern().Replace(normalized, " ");
        normalized = SenderPrefixPattern().Replace(normalized, string.Empty).Trim();
        normalized = StripTimestampTokens(normalized);
        normalized = StripLeadingGreetings(normalized);

        if (normalized.Length < 2 ||
            NoisePattern().IsMatch(normalized) ||
            GreetingOnlyPattern().IsMatch(normalized))
        {
            return string.Empty;
        }

        return normalized;
    }

    public static string Strip(string? text) => CleanForInference(text);

    public static bool IsPromoSpam(string? text)
    {
        var cleaned = CleanForInference(text);
        return !string.IsNullOrWhiteSpace(cleaned) && PromoSpamPattern().IsMatch(cleaned);
    }

    public static bool IsGreetingOnly(string? text)
    {
        var normalized = WhitespacePattern().Replace(text?.Trim() ?? string.Empty, " ");
        return !string.IsNullOrWhiteSpace(normalized) && GreetingOnlyPattern().IsMatch(normalized);
    }

    public static bool ContainsTimestampNoise(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return InlineTimestampPattern().IsMatch(text) || TimestampTokenPattern().IsMatch(text.Trim());
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
            var cleaned = CleanForInference(text);
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

    private static string StripTimestampTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var kept = tokens.Where(token => !TimestampTokenPattern().IsMatch(token)).ToArray();
        return kept.Length == 0 ? string.Empty : string.Join(' ', kept);
    }

    private static string StripLeadingGreetings(string text)
    {
        var normalized = text;
        while (!string.IsNullOrWhiteSpace(normalized))
        {
            var match = LeadingGreetingPattern().Match(normalized);
            if (!match.Success)
            {
                break;
            }

            normalized = normalized[match.Length..].TrimStart();
        }

        return normalized;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(
        @"more_vert|flag as inappropriate|report review|share review|copy link|"
        + @"write a reply|reply publicly|sort by|filter reviews",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DomChromePattern();

    public static bool IsDomChromePollution(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = WhitespacePattern().Replace(text.Trim(), " ");
        return normalized.Length >= 4 && DomChromePattern().IsMatch(normalized);
    }

    public static string SanitizeSummary(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || IsDomChromePollution(text))
        {
            return string.Empty;
        }

        return text.Trim();
    }
}
