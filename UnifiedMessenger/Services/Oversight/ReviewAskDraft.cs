namespace UnifiedMessenger.Services;

/// <summary>
/// The WhatsApp message asking a customer for a Google review.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written, not generated.</b> Unlike a reply to a review, this message goes to a private phone number
/// and says nothing that needs composing — the same six lines work for everyone. A model here would add
/// variability and a failure mode without adding anything the customer notices, so this is a template.
/// </para>
/// <para>
/// <b>The app never sends it.</b> The text goes to the clipboard and the customer's chat is opened in
/// WhatsApp; the owner reads it and presses send. There is no code path in this app that transmits a
/// message, and this feature does not add one.
/// </para>
/// </remarks>
public static class ReviewAskDraft
{
    /// <summary>
    /// Builds the message.
    /// </summary>
    /// <param name="reviewLink">
    /// The salon's Google review link, when it is known. Omitted entirely when it is not — a made-up or
    /// generic link is worse than asking the customer to search, because it fails silently in their hands.
    /// </param>
    /// <remarks>
    /// Deliberately short, names the salon, and gives the customer an easy out. It also does not thank them
    /// for a review they have not written yet, which is the standard template mistake and reads as pressure.
    /// </remarks>
    public static string Build(string customerName, string salonName, string? reviewLink = null)
    {
        var first = ReviewReplyDraft.FirstName(customerName);
        var greeting = string.IsNullOrWhiteSpace(first) ? "Hello!" : $"Hi {first}!";
        var where = string.IsNullOrWhiteSpace(salonName) ? "us" : salonName;

        var ask = string.IsNullOrWhiteSpace(reviewLink)
            ? $"If you have a minute, would you mind leaving {where} a review on Google? "
            : $"If you have a minute, would you mind leaving {where} a review on Google? {reviewLink.Trim()} ";

        return greeting + " Thank you for your lovely message — it really made our day. " + ask +
               "It genuinely helps a small business like ours. No worries at all if you'd rather not.";
    }
}
