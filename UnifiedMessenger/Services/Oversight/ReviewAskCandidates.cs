namespace UnifiedMessenger.Services;

/// <summary>A customer it would be reasonable to ask for a review.</summary>
public readonly record struct ReviewAskCandidate(
    string InstanceId,
    string AccountName,
    string ConversationKey,
    string CustomerName,
    string Phone,
    DateTimeOffset LastActivityUtc)
{
    /// <summary>The identity the "asked once, ever" record is kept under.</summary>
    /// <remarks>
    /// The phone number when WhatsApp has resolved one, and the conversation key otherwise. Both are stable;
    /// a display name is not. Measured on real data, requiring a phone blocked 8 of the 9 non-awaiting chats
    /// — unsaved contacts sit under an @lid privacy id and the number is only recovered separately. Falling
    /// back to the conversation key keeps the promise intact while letting those customers be asked at all.
    /// </remarks>
    public string AskKey => string.IsNullOrWhiteSpace(Phone) ? ConversationKey.Trim() : Phone.Trim();
}

/// <summary>
/// Picks the customers worth asking for a Google review, from WhatsApp conversations that ended well.
/// </summary>
/// <remarks>
/// <para>
/// <b>This decides who gets messaged, so the rules are deliberately narrow.</b> Every customer this returns
/// is one the owner may send a request to, and the cost of a bad pick is a real message to a real person who
/// did not want one. Every condition below removes people; none adds them.
/// </para>
/// <para>
/// <b>The app still never sends.</b> This produces a list and a draft. The owner opens the chat and presses
/// send in WhatsApp themselves — see <see cref="ReviewAskDraft"/>.
/// </para>
/// <para>
/// <b>Once, ever.</b> A customer who has already been asked is excluded permanently, not for a cooling-off
/// period. Being asked twice for a review by the same salon is the behaviour that makes people mute a
/// business, and there is no version of it that reads as anything but automated.
/// </para>
/// </remarks>
public static class ReviewAskCandidates
{
    /// <summary>
    /// How recently the conversation must have ended for an ask to make sense.
    /// </summary>
    /// <remarks>
    /// Two weeks. Beyond that the visit is not fresh, the customer has to work to remember it, and the
    /// request reads as a marketing round rather than a follow-up to something that just happened.
    /// </remarks>
    public static readonly TimeSpan RecencyWindow = TimeSpan.FromDays(14);

    /// <summary>
    /// Gratitude only — deliberately not the wider "closing words" vocabulary.
    /// </summary>
    /// <remarks>
    /// <see cref="ReplyNeed"/> treats "ok", "done" and "noted" as closing a conversation, and they do. They
    /// are not evidence anyone was pleased. Asking someone for a public review because they said "ok" is how
    /// this feature would earn one-star reviews rather than five-star ones, so the bar here is thanks or
    /// praise and nothing softer.
    /// </remarks>
    private static readonly string[] GratitudeTerms =
    [
        "thank", "thanks", "thankyou", "thank you", "thanku", "thnx", "thnks", "thx", "tysm",
        "shukriya", "shukria", "shukrya", "jazakallah", "jazak allah",
        "mashallah", "mashaallah", "bohat acha", "bohat achi", "boht acha", "bahut acha",
        "great service", "excellent", "amazing", "loved it", "love it", "perfect",
        "appreciate", "appreciated", "grateful", "brilliant", "wonderful"
    ];

    /// <summary>
    /// Chooses who to ask, most recent first.
    /// </summary>
    /// <param name="alreadyAsked">Phone numbers already asked at any point. Excluded permanently.</param>
    public static IReadOnlyList<ReviewAskCandidate> Select(
        IEnumerable<(string InstanceId, string AccountName, IReadOnlyList<OversightChatSnapshotService.ChatEntry> Chats)> accounts,
        ISet<string> alreadyAsked,
        DateTimeOffset nowUtc,
        int max = 5)
    {
        var picked = new List<ReviewAskCandidate>();

        foreach (var (instanceId, accountName, chats) in accounts)
        {
            foreach (var chat in chats ?? [])
            {
                // Groups, broadcasts and Status are not customers.
                if (ChatEntryParser.IsNonCustomerConversation(chat.ConversationKey))
                {
                    continue;
                }

                // Nothing outstanding. Asking a favour of someone still waiting on an answer from you is
                // the worst possible moment, and the queue already knows who those are.
                if (chat.IsAwaiting)
                {
                    continue;
                }

                // The customer must have had the last word, and it must be a grateful one. If the salon
                // spoke last, "thanks" in the preview may well be the salon's own.
                if (chat.LastMessageFromMe)
                {
                    continue;
                }

                if (nowUtc - chat.LastActivityUtc > RecencyWindow || chat.LastActivityUtc > nowUtc)
                {
                    continue;
                }

                if (!ReadsAsGrateful(chat.Preview))
                {
                    continue;
                }

                // Identity must be something stable, or "ask once, ever" cannot be kept — a display name
                // is not. The phone when WhatsApp has resolved one, the conversation key when it has not.
                var phone = (chat.ContactPhone ?? string.Empty).Trim();
                var conversationKey = (chat.ConversationKey ?? string.Empty).Trim();
                var askKey = phone.Length > 0 ? phone : conversationKey;
                if (askKey.Length == 0 || alreadyAsked.Contains(askKey))
                {
                    continue;
                }

                picked.Add(new ReviewAskCandidate(
                    instanceId,
                    accountName,
                    // Normalised, not raw: the record declares this non-nullable and AskKey dereferences it
                    // when there is no phone. Passing the raw value put a null behind a non-null contract
                    // (CS8604) — harmless only because the guard above happens to reject the one case that
                    // reaches it today.
                    conversationKey,
                    string.IsNullOrWhiteSpace(chat.CustomerName) ? askKey : chat.CustomerName,
                    phone,
                    chat.LastActivityUtc));
            }
        }

        return picked
            .GroupBy(c => c.AskKey, StringComparer.Ordinal)   // one row per person, not per account
            .Select(g => g.OrderByDescending(c => c.LastActivityUtc).First())
            .OrderByDescending(c => c.LastActivityUtc)
            .Take(Math.Max(0, max))
            .ToList();
    }

    /// <summary>Whether the customer's last message reads as thanks or praise.</summary>
    public static bool ReadsAsGrateful(string? preview)
    {
        if (string.IsNullOrWhiteSpace(preview))
        {
            return false;
        }

        var text = " " + preview.ToLowerInvariant().Trim() + " ";
        return GratitudeTerms.Any(term => text.Contains(term, StringComparison.Ordinal));
    }

    /// <summary>How the visit is described in the list — "yesterday", "on Tuesday".</summary>
    public static string WhenLabel(DateTimeOffset lastActivityUtc, DateTimeOffset nowUtc)
    {
        var days = (int)(nowUtc.Date - lastActivityUtc.ToLocalTime().Date).TotalDays;
        return days switch
        {
            <= 0 => "today",
            1 => "yesterday",
            < 7 => $"on {lastActivityUtc.ToLocalTime():dddd}",
            _ => $"{days} days ago"
        };
    }
}
