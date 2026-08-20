namespace UnifiedMessenger.Services;

/// <summary>What kind of thing a waiting row actually is, from the owner's point of view.</summary>
public enum QueueFacet
{
    /// <summary>Nothing identifiable. Honest, and the largest bucket until a model replaces the lexicon.</summary>
    Unknown,

    /// <summary>A customer rang and did not get through. Actionable by calling back, not by typing.</summary>
    MissedCall,

    /// <summary>A complaint, a price objection, or a stated intention to go elsewhere.</summary>
    AtRisk,

    /// <summary>Asking about price, services, timings or availability.</summary>
    Enquiry,

    /// <summary>Arranging, confirming or moving an appointment.</summary>
    Booking,

    /// <summary>Asking about work, or replying to a hiring or training form.</summary>
    JobApplicant,

    /// <summary>Inbound business outreach or another business's automated notice.</summary>
    BusinessOutreach,

    /// <summary>A photo or voice note with no caption — usually "can you do this?".</summary>
    Media,

    /// <summary>A message the app could not read. Counted, but nothing can be said about it.</summary>
    Unreadable
}

/// <summary>
/// Resolves one facet per waiting row, so the queue can be filtered by the thing the owner is actually
/// deciding between: <i>do I type, do I call, or is this not even a customer?</i>
///
/// <para>
/// <b>Why this exists rather than a separate screen.</b> The missed calls needed their own worklist — 81 of
/// them on real data, invisible until the message-type work because they carried no text and sat in the
/// queue as unreadable rows. Building that as a fifth view would have meant a list that could not also be
/// narrowed by branch or age. As a facet it composes with both for free, and the count is visible on the
/// chip before the owner clicks it.
/// </para>
/// <para>
/// <b>Why it is not simply <see cref="ConversationTopic"/>.</b> Topic is classified from message text and is
/// deliberately kept that way — pure, and independent of whether a reply is owed. A missed call has no text;
/// its signal is the message <i>type</i>, which lives in <see cref="ReplyNeedReason"/>. This is the thin
/// layer that reads both without either one having to know about the other.
/// </para>
/// </summary>
public static class QueueFacets
{
    /// <summary>
    /// The facet for a waiting conversation. Reason wins over topic where they overlap, because a missed
    /// call or an unreadable message is a different <i>kind</i> of row — no amount of topic classification
    /// changes what the owner can do about it.
    /// </summary>
    public static QueueFacet Resolve(OversightChatSnapshotService.ChatEntry chat) =>
        Resolve(OversightChatSnapshotService.ClassifyReplyNeed(chat).Reason, chat.Preview);

    internal static QueueFacet Resolve(ReplyNeedReason reason, string? preview) => reason switch
    {
        ReplyNeedReason.MissedCall => QueueFacet.MissedCall,
        ReplyNeedReason.MediaWithoutCaption => QueueFacet.Media,
        ReplyNeedReason.NoPreviewAvailable => QueueFacet.Unreadable,
        _ => ResolveFromText(preview)
    };

    /// <summary>
    /// Topic first, then the one thing topic cannot see: a preview that is just an attachment's filename.
    /// </summary>
    /// <remarks>
    /// <c>MediaWithoutCaption</c> only fires when a photo or voice note has no text at all. A document
    /// arrives with its filename as the preview — "Islamabad.pdf", "PERIO IMP STATIONS .pdf",
    /// "VID_20260816_145608.mp4" — so it has text, no topic words, and was landing in "Uncategorised".
    /// Filed as Media because that is what the owner is looking at when they open it.
    ///
    /// <para>
    /// Checked <i>after</i> topic so a real sentence that happens to end in an attachment name — "here is
    /// the quote you asked for, invoice.pdf" — is still classified by what it says.
    /// </para>
    /// </remarks>
    private static QueueFacet ResolveFromText(string? preview)
    {
        var topic = FromTopic(ConversationTopics.Classify(preview));
        return topic == QueueFacet.Unknown && LooksLikeFileName(preview)
            ? QueueFacet.Media
            : topic;
    }

    private static readonly string[] AttachmentExtensions =
    [
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".csv", ".txt", ".zip", ".rar",
        ".jpg", ".jpeg", ".png", ".jfif", ".webp", ".heic", ".gif",
        ".mp4", ".mov", ".3gp", ".webm", ".avi", ".mp3", ".ogg", ".opus", ".m4a", ".wav", ".vcf"
    ];

    /// <summary>
    /// True when the preview is a bare attachment name rather than a message.
    /// </summary>
    /// <remarks>
    /// The word limit is what keeps this from swallowing sentences: a filename is a handful of tokens, and
    /// anything longer that merely mentions a file is a message about a file.
    /// </remarks>
    internal static bool LooksLikeFileName(string? preview)
    {
        var text = (preview ?? string.Empty).Trim();
        if (text.Length is 0 or > 60)
        {
            return false;
        }

        if (!AttachmentExtensions.Any(e => text.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 5;
    }

    private static QueueFacet FromTopic(ConversationTopic topic) => topic switch
    {
        ConversationTopic.AtRisk => QueueFacet.AtRisk,
        ConversationTopic.Enquiry => QueueFacet.Enquiry,
        ConversationTopic.Booking => QueueFacet.Booking,
        ConversationTopic.JobApplicant => QueueFacet.JobApplicant,
        ConversationTopic.BusinessOutreach => QueueFacet.BusinessOutreach,
        _ => QueueFacet.Unknown
    };

    /// <summary>
    /// Display order for the chips: what costs money if ignored, then what earns it, then what can be set
    /// aside. Not alphabetical, and not the enum order.
    /// </summary>
    public static readonly QueueFacet[] DisplayOrder =
    [
        QueueFacet.AtRisk,
        QueueFacet.MissedCall,
        QueueFacet.Enquiry,
        QueueFacet.Booking,
        QueueFacet.Media,
        QueueFacet.JobApplicant,
        QueueFacet.BusinessOutreach,
        QueueFacet.Unreadable,
        QueueFacet.Unknown
    ];

    public static string Label(QueueFacet facet) => facet switch
    {
        QueueFacet.MissedCall => "Missed calls",
        QueueFacet.AtRisk => "At risk",
        QueueFacet.Enquiry => "Enquiries",
        QueueFacet.Booking => "Bookings",
        QueueFacet.JobApplicant => "Job & training",
        QueueFacet.BusinessOutreach => "Business outreach",
        QueueFacet.Media => "Photos & voice notes",
        QueueFacet.Unreadable => "Could not read",
        _ => "Uncategorised"
    };

    public static string Describe(QueueFacet facet) => facet switch
    {
        QueueFacet.MissedCall =>
            "Customers who rang and did not get through. These need a call back, not a message — the row "
            + "gives you the number.",
        QueueFacet.AtRisk =>
            "Complaints, price objections, and customers saying they will go elsewhere. Deliberately "
            + "over-inclusive: a wrong guess costs a glance, missing one costs a customer.",
        QueueFacet.Enquiry => "Asking about price, services, timings or availability — the ones with money attached.",
        QueueFacet.Booking => "Making, moving or cancelling an appointment.",
        QueueFacet.JobApplicant =>
            "People asking about work, or replying to a hiring or training form. Not customer service — "
            + "filter these out to see the customers.",
        QueueFacet.BusinessOutreach => "Agencies, marketers and other businesses' automated notices.",
        QueueFacet.Media => "A photo or voice note with no caption. Usually \"can you do this?\".",
        QueueFacet.Unreadable => "The message could not be read, so nothing can be said about it yet.",
        _ => "No topic could be identified from the last message."
    };

    /// <summary>
    /// True for facets the owner deals with by dialling rather than typing, so the row can offer the right
    /// action.
    /// </summary>
    public static bool IsCallBack(QueueFacet facet) => facet == QueueFacet.MissedCall;
}
