namespace UnifiedMessenger.Services;

/// <summary>A subject that came up in more than one waiting review.</summary>
public readonly record struct ReviewTheme(
    string Label,
    int Count,
    IReadOnlyList<string> Branches,
    bool IsComplaint);

/// <summary>
/// Finds what the waiting reviews keep saying — deterministically, before any model is involved.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the counting is not done by the AI.</b> The line this feeds ("three of them mention waiting time,
/// all at F-11") is only worth showing if the three is true. A language model asked to both find and count
/// themes will produce a fluent number, and nobody reading the dashboard can tell a counted three from an
/// invented one. So the clustering and the arithmetic happen here, and the model is given the finished facts
/// and asked only to phrase them — see <c>ReviewInsightService</c>.
/// </para>
/// <para>
/// <b>Scope is narrower than it looks, and the wording must admit it.</b> The scrape captures text only for
/// reviews still awaiting a reply, and only the first several per account. These themes therefore describe
/// the unanswered queue, not the business's reviews as a whole — <see cref="Describe"/> says "waiting" for
/// exactly that reason.
/// </para>
/// </remarks>
public static class ReviewThemes
{
    /// <summary>
    /// A subject only counts as a theme once it has been said more than once.
    /// </summary>
    /// <remarks>
    /// One customer mentioning parking is an anecdote. The whole value of this line is separating the
    /// recurring from the one-off, so a threshold of 1 would defeat it.
    /// </remarks>
    private const int MinimumMentions = 2;

    private static readonly (string Label, bool IsComplaint, string[] Terms)[] Vocabulary =
    [
        ("waiting time", true,
            ["waited", "waiting", "wait ", "late", "delay", "delayed", "on time", "hour past", "appointment time",
             "took too long", "slow service", "kept me"]),

        ("appointments not honoured", true,
            ["appointment", "booked", "booking", "reschedul", "cancel", "don't commit", "didnt commit",
             "no show", "double book"]),

        ("staff attitude", true,
            ["rude", "unprofessional", "attitude", "ignored", "impolite", "disrespect", "argu", "shouted"]),

        ("cleanliness", true,
            ["dirty", "unclean", "not clean", "hygiene", "unhygienic", "messy", "smell"]),

        ("price", true,
            ["expensive", "overcharg", "over charg", "price", "pricing", "costly", "charged me", "rip off",
             "not worth"]),

        ("the result not matching what was promised", true,
            ["not what", "mediocre", "poor service", "bad service", "ruined", "damaged", "worst",
             "not satisfied", "disappointed", "waste of"]),

        ("parking", true, ["parking", "car park", "no space to park"]),

        ("refunds", true, ["refund", "money back", "reimburse"]),

        ("friendly staff", false,
            ["friendly", "polite", "welcoming", "courteous", "lovely staff", "great staff", "helpful"]),

        ("good results", false,
            ["excellent", "amazing", "fantastic", "brilliant", "highly recommend", "best service",
             "very good", "great experience", "professional"])
    ];

    /// <summary>
    /// Groups the waiting reviews by subject, commonest first.
    /// </summary>
    /// <remarks>
    /// A review can land in more than one theme — "waited an hour and the staff were rude" is genuinely both,
    /// and forcing a single label would undercount whichever lost. Counts are therefore per theme and do not
    /// sum to the number of reviews, which is why <see cref="Describe"/> never presents them as a share.
    /// </remarks>
    public static IReadOnlyList<ReviewTheme> Extract(IEnumerable<QueuedReview> reviews)
    {
        var hits = new Dictionary<string, (int Count, HashSet<string> Branches, bool IsComplaint)>(StringComparer.Ordinal);

        foreach (var review in reviews)
        {
            if (string.IsNullOrWhiteSpace(review.Text))
            {
                continue;
            }

            var text = " " + review.Text.ToLowerInvariant() + " ";

            foreach (var (label, isComplaint, terms) in Vocabulary)
            {
                if (!terms.Any(term => text.Contains(term, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (!hits.TryGetValue(label, out var entry))
                {
                    entry = (0, new HashSet<string>(StringComparer.OrdinalIgnoreCase), isComplaint);
                }

                entry.Count++;
                if (!string.IsNullOrWhiteSpace(review.AccountName))
                {
                    entry.Branches.Add(review.AccountName);
                }

                hits[label] = entry;
            }
        }

        return hits
            .Where(kv => kv.Value.Count >= MinimumMentions)
            .OrderByDescending(kv => kv.Value.IsComplaint)   // complaints first: they are what needs acting on
            .ThenByDescending(kv => kv.Value.Count)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new ReviewTheme(
                kv.Key,
                kv.Value.Count,
                kv.Value.Branches.OrderBy(b => b, StringComparer.OrdinalIgnoreCase).ToList(),
                kv.Value.IsComplaint))
            .ToList();
    }

    /// <summary>
    /// The plain sentence, used as-is when the local model is off and as the model's source facts when it is on.
    /// </summary>
    /// <param name="reviewsWithText">
    /// How many waiting reviews actually had words in them — the population these counts came from.
    /// </param>
    /// <returns>Null when there is nothing recurring to report, so the caller can hide the strip entirely.</returns>
    public static string? Describe(IReadOnlyList<ReviewTheme> themes, int reviewsWithText)
    {
        if (themes.Count == 0 || reviewsWithText == 0)
        {
            return null;
        }

        var lead = themes[0];
        var where = lead.Branches.Count == 1 ? $", all at {lead.Branches[0]}" : string.Empty;
        var sentence = $"{Count(lead.Count)} of the {reviewsWithText} waiting reviews with text mention {lead.Label}{where}.";

        if (themes.Count > 1)
        {
            var second = themes[1];
            sentence += $" {Count(second.Count)} mention {second.Label}.";
        }

        return sentence;
    }

    private static string Count(int n) => n switch
    {
        2 => "Two",
        3 => "Three",
        4 => "Four",
        5 => "Five",
        _ => n.ToString()
    };
}
