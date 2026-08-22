using System.Text.RegularExpressions;

namespace UnifiedMessenger.Services;

/// <summary>Why a generated draft was refused, or <see cref="Ok"/> when it may be shown.</summary>
public enum DraftVerdict
{
    Ok,
    Empty,
    TooLong,
    ContainsPlaceholder,
    ContainsLink,
    ModelTalkedAboutItself,
    PromisesMoney
}

/// <summary>
/// Builds the prompt for a review reply, and refuses the drafts that should never reach the owner.
/// </summary>
/// <remarks>
/// <para>
/// <b>The app never sends this.</b> A draft is written into the clipboard and Google's own reply box is
/// opened; the owner reads it, edits it, and presses send themselves. That is the project's standing
/// never-auto-send rule, and it is what keeps a bad model output from becoming a public reply to an angry
/// one-star customer.
/// </para>
/// <para>
/// <b>The owner editing it is not a reason to skip validation.</b> A draft that promises a refund is worse
/// than no draft even when reviewed, because the wording arrives pre-approved-looking and the commitment is
/// easy to miss on a quick read. Anything this refuses is simply not offered, and the owner writes their own.
/// </para>
/// </remarks>
public static class ReviewReplyDraft
{
    /// <summary>
    /// Google's public reply box. Long replies read as defensive, and nobody reads past a few lines.
    /// </summary>
    private const int MaxCharacters = 700;

    public const string SystemPrompt =
        "You write short public replies for the owner of a beauty salon responding to a Google review. " +
        "Rules you must never break: reply in ONE short paragraph, at most 60 words; plain sentences, no " +
        "markdown, no bullet points, no quotation marks around the reply; never promise a refund, discount, " +
        "free treatment, compensation or any specific date; never invent details that are not in the review; " +
        "never mention being an AI; never include links or phone numbers; do not sign off with a name. " +
        "For an unhappy review: acknowledge the specific thing they raised, apologise plainly, and invite " +
        "them to contact the salon so it can be put right. For a positive review: thank them warmly and " +
        "briefly, mentioning what they praised.";

    /// <summary>The prompt for one review.</summary>
    /// <remarks>
    /// Only the reviewer's first name, star rating and their own public words are included. There is nothing
    /// else to add: any other "context" would be the model's to invent, which is what the rules forbid.
    /// </remarks>
    public static string BuildPrompt(QueuedReview review, string businessName)
    {
        var name = FirstName(review.Reviewer);
        var stars = review.Stars is >= 1 and <= 5 ? $"{review.Stars} out of 5 stars" : "an unknown rating";
        var body = string.IsNullOrWhiteSpace(review.Text)
            ? "(They left a rating with no written review.)"
            : review.Text.Trim();

        return $"""
                Business: {businessName}
                Reviewer: {(string.IsNullOrWhiteSpace(name) ? "(name not shown)" : name)}
                Rating: {stars}
                Their review: {body}

                Write the owner's public reply.
                """;
    }

    /// <summary>
    /// Cleans a model response and decides whether it is fit to show.
    /// </summary>
    /// <param name="draft">Raw model output.</param>
    /// <param name="cleaned">The tidied reply, when the verdict is <see cref="DraftVerdict.Ok"/>.</param>
    public static DraftVerdict Validate(string? draft, out string cleaned)
    {
        cleaned = string.Empty;

        if (string.IsNullOrWhiteSpace(draft))
        {
            return DraftVerdict.Empty;
        }

        var text = draft.Trim();

        // Models routinely wrap a reply in quotes, and a reply that begins with a quote mark looks like a
        // mistake when pasted into Google.
        text = text.Trim('"', '“', '”', '\'').Trim();

        // Strip a leading "Reply:" / "Here is the reply:" preamble rather than refusing over it.
        text = Regex.Replace(text, @"^(here('s| is)? (the |a )?(suggested )?reply[:\-]\s*|reply[:\-]\s*)", string.Empty,
            RegexOptions.IgnoreCase);
        text = text.Trim();

        if (text.Length == 0)
        {
            return DraftVerdict.Empty;
        }

        if (text.Length > MaxCharacters)
        {
            return DraftVerdict.TooLong;
        }

        // Unfilled template slots — "[name]", "{{customer}}", "XXXX" — are the clearest sign the model
        // produced a form letter rather than a reply.
        if (Regex.IsMatch(text, @"\[[^\]]{0,40}\]|\{\{?[^}]{0,40}\}?\}|\bXXX+\b", RegexOptions.IgnoreCase))
        {
            return DraftVerdict.ContainsPlaceholder;
        }

        if (Regex.IsMatch(text, @"https?://|www\.|\b\d{4,}\b"))
        {
            return DraftVerdict.ContainsLink;
        }

        if (Regex.IsMatch(text, @"\bas an ai\b|\bi am an ai\b|\blanguage model\b|\bi cannot\b", RegexOptions.IgnoreCase))
        {
            return DraftVerdict.ModelTalkedAboutItself;
        }

        // The commitment guard. Offering to "put this right" is fine and is what the prompt asks for; naming
        // a refund, a free treatment or money back is a promise the owner has not agreed to make.
        if (Regex.IsMatch(
                text,
                @"\brefund(ed|ing)?\b|\bmoney back\b|\bfree (of charge|treatment|service|session)\b|\bdiscount\b|\bcompensat",
                RegexOptions.IgnoreCase))
        {
            return DraftVerdict.PromisesMoney;
        }

        cleaned = text;
        return DraftVerdict.Ok;
    }

    /// <summary>Why the owner is seeing no draft, in words that say what to do next.</summary>
    public static string ExplainRefusal(DraftVerdict verdict) => verdict switch
    {
        DraftVerdict.PromisesMoney => "The draft promised a refund or discount, so it was discarded. Write this one yourself.",
        DraftVerdict.ContainsPlaceholder => "The draft came back half-finished. Try again, or write this one yourself.",
        DraftVerdict.TooLong => "The draft was too long for a public reply. Try again, or write this one yourself.",
        DraftVerdict.ContainsLink => "The draft included a link or number it made up, so it was discarded.",
        DraftVerdict.ModelTalkedAboutItself => "The draft mentioned being an AI, so it was discarded.",
        _ => "No draft could be written. Write this one yourself."
    };

    /// <summary>The reviewer's first name, when it is safe to use one.</summary>
    /// <remarks>
    /// Returns empty for anything that does not look like a name — handles, single letters, names made of
    /// digits. Opening a public reply with the wrong name is worse than opening with none.
    /// </remarks>
    public static string FirstName(string? reviewer)
    {
        if (string.IsNullOrWhiteSpace(reviewer))
        {
            return string.Empty;
        }

        var first = reviewer.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        first = first.Trim('.', ',', '-');

        var looksLikeAName = first.Length is >= 2 and <= 20 && first.All(char.IsLetter);
        return looksLikeAName ? first : string.Empty;
    }
}
