namespace UnifiedMessenger.Services;

/// <summary>Why a conversation was, or was not, counted as needing a reply.</summary>
public enum ReplyNeedReason
{
    /// <summary>The customer asked or requested something. Always counted.</summary>
    AsksSomething,

    /// <summary>Substantive text that is not a closer. Counted, because the app cannot rule it out.</summary>
    Substantive,

    /// <summary>No preview text was read for this chat, so nothing can be judged. Counted.</summary>
    NoPreviewAvailable,

    /// <summary>A photo/voice/document with no caption. Counted — it usually IS a question.</summary>
    MediaWithoutCaption,

    /// <summary>The customer's last word was an acknowledgement: "ok", "thanks", "ji", "jazakallah".</summary>
    Acknowledgement,

    /// <summary>A bare greeting or sign-off with nothing asked: "salam", "bye", "take care".</summary>
    GreetingOrSignOff,

    /// <summary>Emoji, punctuation or reaction only.</summary>
    EmojiOnly,

    /// <summary>Local AI judged the thread closed. Only ever reached for text the rules could not settle.</summary>
    AiJudgedClosed,

    /// <summary>
    /// The conversation has no last message at all — deleted for everyone, or expired under disappearing
    /// messages. There is nothing left to reply to.
    /// </summary>
    MessageNoLongerAvailable,

    /// <summary>
    /// The last "message" is WhatsApp's own bookkeeping — a security-code change, a protocol notice, a
    /// template notification. Nobody wrote it and nobody is waiting on an answer.
    /// </summary>
    SystemNotice,

    /// <summary>
    /// The last entry is a call, not a message. Counted, because a missed call from a customer is worth
    /// returning — but named honestly instead of appearing as an unreadable message.
    /// </summary>
    MissedCall,

    /// <summary>
    /// The last entry is a call the OWNER placed, not one the customer made. Nothing is waiting: you do
    /// not call yourself back. Split out of <see cref="MissedCall"/> after reading real call-log entries
    /// live — the branch had ignored direction entirely.
    /// </summary>
    OutgoingCall,

    /// <summary>
    /// The customer called and the call was ANSWERED — picked up here, or on the owner's phone
    /// ("Accepted on another device"). Nothing is waiting: they got through and spoke to someone.
    /// </summary>
    CallAnswered
}

/// <summary>
/// Whether a conversation whose last message came from the customer actually needs a reply.
///
/// <para>
/// <b>Why this exists.</b> "Awaiting" was a pure direction flag — the customer had the last word and we
/// have not typed since. On the owner's real data that produced <b>466</b> customers supposedly waiting,
/// with the oldest at 82 days. Measured against that snapshot, only <b>41</b> of them actually asked
/// anything; <b>82</b> were the word "ok" or its relatives, and <b>454 of 466 were already read</b>. The
/// number was measuring "nobody typed back", not "somebody is waiting".
/// </para>
/// <para>
/// The cost was not the size of the number, it was what the number hid. Buried in that list, unanswered:
/// a customer reporting bruising after a waxing appointment, and another saying
/// <i>"warna main kahin aur chali jawon"</i> — otherwise I'll go elsewhere. A queue that cannot surface
/// those is not doing the one job this product exists to do.
/// </para>
/// <para>
/// <b>The bias is deliberate and one-directional.</b> Counting a finished conversation costs the owner a
/// glance. Dropping a real one costs them a customer. So every rule here only ever closes a chat on
/// <i>positive</i> evidence that it is finished; anything unrecognised, unreadable or merely short stays
/// counted. <see cref="AsksSomething"/> is checked first and overrides every closing rule, so "ok, but
/// what time?" is never treated as an "ok".
/// </para>
/// </summary>
public readonly record struct ReplyNeedVerdict(bool NeedsReply, ReplyNeedReason Reason)
{
    /// <summary>True when the app decided this on its own rather than being unable to judge.</summary>
    public bool IsConfidentClose => !NeedsReply;

    /// <summary>Short, plain-English explanation for the "closed automatically" list.</summary>
    public string Explain() => Reason switch
    {
        ReplyNeedReason.Acknowledgement => "Last message was an acknowledgement",
        ReplyNeedReason.GreetingOrSignOff => "Last message was a greeting or sign-off",
        ReplyNeedReason.EmojiOnly => "Last message was only an emoji",
        ReplyNeedReason.AiJudgedClosed => "Conversation looks finished",
        ReplyNeedReason.AsksSomething => "Customer asked something",
        ReplyNeedReason.MediaWithoutCaption => "Customer sent a photo, voice note or contact",
        ReplyNeedReason.MessageNoLongerAvailable => "The message no longer exists — deleted or expired",
        ReplyNeedReason.SystemNotice => "Not a message — a WhatsApp system notice",
        ReplyNeedReason.MissedCall => "Customer called and did not get through",
        ReplyNeedReason.OutgoingCall => "You called them — nothing is waiting on a reply",
        ReplyNeedReason.CallAnswered => "Customer called and the call was answered",
        ReplyNeedReason.NoPreviewAvailable => "Message could not be read",
        _ => "Customer sent a message"
    };
}

public static class ReplyNeed
{
    /// <summary>
    /// Words and phrases that end a conversation. English, Roman Urdu and Urdu script together, because
    /// the owner's customers mix all three in a single message — "ok jazakallah", "walaikum us salam",
    /// "ok thnx 👍🏻" are verbatim from real data. An English-only list would have closed almost nothing.
    /// </summary>
    private static readonly HashSet<string> ClosingWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Assent / acknowledgement
        "ok", "okay", "okey", "oky", "okk", "okkk", "oka", "okie", "k", "kk", "kay",
        "yes", "yep", "yup", "yeah", "ya", "sure", "fine", "alright", "right", "correct",
        "han", "haan", "ha", "hn", "hnn", "ho", "hoo", "ji", "jee", "g", "gg", "jji",
        "acha", "achha", "achaa", "acchaa", "achi", "theek", "thik", "teek", "tk", "sahi", "bilkul",
        "done", "noted", "got", "gotcha", "understood", "roger",

        // Thanks
        "thanks", "thank", "thankyou", "thanku", "thankz", "thnx", "thnks", "thx", "tks", "tysm", "ty",
        // Typos and elongations found sitting in the live queue as "needs reply". Every one of these is a
        // customer signing off, and each was costing the owner a row: "Ohky", "Okhy", "Ok thankd",
        // "Appreciated", "Yuppp. Acknowledged". A lexicon that only knows correctly-spelled English closers
        // does not close a real inbox.
        "ohky", "okhy", "okhay", "okey", "okies", "okii", "okiii", "thankd", "thnkx", "thanx",
        "appreciated", "appreciate", "acknowledged", "acknowledge", "ackn",
        "yupp", "yuppp", "yess", "yesss", "yeah", "yaa", "haanji", "hanji",
        "shukriya", "shukria", "shukrya", "jazakallah", "jazakhallah", "jzk", "jazak",

        // Praise / warmth that closes rather than asks
        "great", "good", "nice", "perfect", "lovely", "excellent", "awesome", "cool", "super",
        "welcome", "wc", "np", "problem", "issue", "worries", "mention", "oh", "ohh", "hmm", "hm",

        // Greetings and sign-offs
        "salam", "salaam", "slam", "assalam", "assalamualaikum", "asalamualaikum", "walaikum",
        "walaikumsalam", "wsalam", "aoa", "hi", "hello", "hey",
        "bye", "goodbye", "gudbye", "tc", "care", "take", "cu", "khuda", "allah", "hafiz",
        "inshallah", "insha", "mashallah", "masha", "ameen", "amin", "alhamdulillah",

        // Glue words that must not by themselves stop a phrase counting as a closer
        "so", "much", "very", "you", "u", "your", "youre", "its", "it", "is", "no", "not", "for",
        "the", "a", "an", "and", "my", "me", "i", "am", "be", "will", "then", "now", "too", "also",
        "dear", "sis", "sir", "madam", "mam", "miss", "bro", "bhai", "baji", "api", "apa", "ap",
        "koi", "baat", "nahi", "nahin", "masla", "kea", "hai", "he", "hy",

        // Urdu script
        "شکریہ", "ٹھیک", "ہے", "جی", "اچھا", "ہاں", "اوکے", "بہت", "السلام", "علیکم", "وعلیکم",
        "جزاک", "اللہ", "حافظ", "انشاء", "ماشاء", "آمین", "خدا", "بھی", "ٹھیکہے"
    };

    /// <summary>
    /// Anything here means the customer wants something back, and **overrides every closing rule**.
    ///
    /// <para>
    /// Word-boundary matched, so "book" fires on "book me in" but not on "facebook". Roman Urdu question
    /// words carry most of the weight for this business — "kitna", "kab", "chahiye", "karani hai" — and
    /// none of them use a question mark, which is exactly why punctuation alone was never enough.
    /// </para>
    /// </summary>
    private static readonly string[] RequestWords =
    [
        // English interrogatives and requests
        "what", "when", "where", "which", "who", "whom", "whose", "why", "how",
        "can", "could", "would", "should", "shall", "may", "will",
        "do", "does", "did", "is", "are", "was", "were", "have", "has",
        "please", "plz", "pls", "kindly", "want", "need", "needed", "looking", "interested",
        "send", "share", "tell", "let", "confirm", "check", "help", "reply", "call",
        // "contact" is deliberately absent: the label for a shared contact card is literally "Shared a
        // contact", and matching it here would report a forwarded vCard as a customer asking something.
        // "please contact me" still fires on "please".
        "available", "availability", "book", "booking", "bookings", "appointment", "appointments",
        "price", "prices", "pricing", "rate", "rates", "charge", "charges", "cost", "fee", "fees",
        "discount", "offer", "package", "deal", "timing", "timings", "time", "open", "close", "closed",
        "slot", "slots", "address", "location", "number", "menu", "list", "detail", "details", "info",
        "cancel", "reschedule", "change", "refund", "complaint", "wrong", "bad",
        // "problem" and "issue" are deliberately NOT here. They collide with "no problem" / "ok no
        // issue", both verbatim closers in the real data, and this list wins over every closing rule.
        // Nothing is lost by omitting them: a real complaint runs longer than MaxClosingWords and is
        // kept as Substantive anyway, which is the same outcome by a safer route.

        // Roman Urdu
        "kya", "kia", "kiya", "kb", "kab", "kabhi", "kahan", "kahaan", "kaha", "kidhar",
        "kaise", "kaisay", "kese", "kesay", "kitna", "kitni", "kitne", "kitnay", "kitna",
        "konsa", "kaunsa", "kon", "kaun", "chahiye", "chahye", "chaiye", "chahie", "chahta", "chahti",
        "karani", "karana", "karwana", "karwani", "karna", "karni", "krna", "krni", "krwana",
        "milega", "milegi", "milta", "milti", "mil", "batao", "bataen", "bata", "batayen", "batadein",
        "bhej", "bhejo", "bhejein", "bhejden", "dedo", "dedein", "chahiay", "zarurat", "zaroorat",
        "rate", "raate", "kimat", "qeemat", "waqt", "time",

        // Urdu script
        "کیا", "کب", "کہاں", "کیسے", "کتنا", "کتنی", "کتنے", "کون", "چاہیے", "چاہئے",
        "بھیج", "بتائیں", "بتاو", "ملے", "قیمت", "ریٹ", "وقت", "پتہ", "نمبر"
    ];

    /// <summary>
    /// Preview text WhatsApp substitutes for a message with no text of its own. These are counted, not
    /// closed: on this business's data an uncaptioned photo is usually "can you do this?" — a question
    /// with no words in it.
    /// </summary>
    private static readonly HashSet<string> MediaPlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "photo", "photos", "image", "video", "videos", "voice", "audio", "voice message", "sticker",
        "gif", "document", "file", "contact", "location", "poll", "message",
        // The label ChatEntryParser substitutes for a contact card whose payload would otherwise render
        // as a raw JID. Counted like any other wordless message: "book my friend in too" is a real
        // request, and nothing here can tell it from an idle forward.
        "shared a contact"
    };

    /// <summary>The longest a message can be and still be dismissed as a closer.</summary>
    /// <remarks>
    /// Five, not two. Real closers in the data run to "ok thank you so much", "ok fine no problem",
    /// "walaikum us salam", "ok jazakallah". Length is not what makes a message safe to close —
    /// <see cref="AsksSomething"/> is — so this bound exists only to stop a long message full of
    /// incidental short words slipping through.
    /// </remarks>
    internal const int MaxClosingWords = 5;

    /// <summary>
    /// How long a conversation with no last message must have been idle before the app concludes the
    /// message is gone rather than merely not loaded yet.
    /// </summary>
    /// <remarks>
    /// Two days, against a real load latency measured in seconds — deliberately three orders of magnitude
    /// of headroom, because the cost of being wrong is a dropped customer. On the owner's data 163 of 204
    /// blank-preview conversations are older than a week, so a generous threshold still catches nearly all
    /// of them.
    /// </remarks>
    internal static readonly TimeSpan MissingMessageIsGoneAfter = TimeSpan.FromDays(2);

    /// <summary>
    /// WhatsApp's own bookkeeping entries, which are stored in the message stream but were not written by
    /// anyone. Observed live in the owner's data with these exact type strings.
    /// </summary>
    internal static bool IsSystemNoticeType(string? type)
    {
        var value = (type ?? string.Empty).Trim();
        return value.Equals("e2e_notification", StringComparison.OrdinalIgnoreCase)   // security code changed
            || value.Equals("protocol", StringComparison.OrdinalIgnoreCase)
            || value.Equals("notification_template", StringComparison.OrdinalIgnoreCase)
            || value.Equals("gp2", StringComparison.OrdinalIgnoreCase)                // group membership change
            || value.Equals("broadcast_notification", StringComparison.OrdinalIgnoreCase)
            || value.Equals("ciphertext", StringComparison.OrdinalIgnoreCase)         // not yet decrypted
            || value.Equals("revoked", StringComparison.OrdinalIgnoreCase)
            || value.Equals("keychange", StringComparison.OrdinalIgnoreCase)
            || value.Equals("payment_notification", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether WhatsApp's own verdict on a call says the customer got through.
    /// </summary>
    /// <remarks>
    /// The vocabulary was read live over CDP from 378 real call entries on the owner's accounts, not
    /// guessed: <c>Missed</c>, <c>Completed</c>, <c>AcceptedElsewhere</c>, <c>Rejected</c>, <c>Ongoing</c>,
    /// <c>Failed</c>. Of the 317 INBOUND calls in that sample, only 166 were <c>Missed</c> — 102 were
    /// <c>Completed</c> and 33 <c>AcceptedElsewhere</c>, meaning someone picked up, very often on the
    /// owner's own phone. Every one of them was being presented as a customer to ring back.
    ///
    /// <para>
    /// <c>Rejected</c> deliberately does NOT count as answered: the call was actively declined and the
    /// customer still did not get what they rang for. <c>Failed</c> likewise. Anything unrecognised —
    /// including the empty string the IndexedDB fallback produces — stays counted, keeping this
    /// classifier's one-directional bias: close only on positive evidence.
    /// </para>
    /// </remarks>
    internal static bool IsAnsweredCallOutcome(string? outcome)
    {
        var value = (outcome ?? string.Empty).Trim();
        return value.Equals("Completed", StringComparison.OrdinalIgnoreCase)
            || value.Equals("AcceptedElsewhere", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Ongoing", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A voice or video call entry rather than a message.</summary>
    internal static bool IsCallType(string? type)
    {
        var value = (type ?? string.Empty).Trim();
        return value.Equals("call_log", StringComparison.OrdinalIgnoreCase)
            || value.Equals("call", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Message types that carry no text of their own but are still a customer reaching out.</summary>
    internal static bool IsMediaType(string? type)
    {
        var value = (type ?? string.Empty).Trim();
        return value.Equals("image", StringComparison.OrdinalIgnoreCase)
            || value.Equals("video", StringComparison.OrdinalIgnoreCase)
            || value.Equals("ptt", StringComparison.OrdinalIgnoreCase)      // push-to-talk voice note
            || value.Equals("audio", StringComparison.OrdinalIgnoreCase)
            || value.Equals("document", StringComparison.OrdinalIgnoreCase)
            || value.Equals("sticker", StringComparison.OrdinalIgnoreCase)
            || value.Equals("vcard", StringComparison.OrdinalIgnoreCase)
            || value.Equals("location", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Decides whether a customer's last message leaves anything to answer.
    /// </summary>
    /// <param name="preview">The last message text, as scraped. Empty when it could not be read.</param>
    public static ReplyNeedVerdict Classify(string? preview) => Classify(preview, null, null, null);

    /// <summary>
    /// As <see cref="Classify(string?)"/>, but able to tell an empty preview's three very different causes
    /// apart using what the scraper reported about the message itself.
    /// </summary>
    /// <param name="hasLastMessage">
    /// Whether a last message exists at all. <see langword="null"/> when unknown (an older snapshot, or a
    /// throttled background webview whose persisted <c>lastMessage</c> is simply absent).
    /// </param>
    /// <param name="lastMessageType">WhatsApp's message type, e.g. <c>chat</c>, <c>image</c>, <c>ptt</c>.</param>
    /// <param name="waitingFor">How long the customer has been waiting, used only to weigh a missing message.</param>
    /// <remarks>
    /// This exists because a blank preview is <b>ambiguous</b>, and the first version of this fix nearly
    /// got it badly wrong. <c>bodyOf()</c> in the scraper returns an empty string for an uncaptioned photo
    /// just as it does for a message that is gone — and those need opposite treatment. An uncaptioned photo
    /// is very often "can you do this?" and must be answered; a vanished message has nothing to answer.
    /// Closing on age alone would have dropped real customers sending pictures.
    /// </remarks>
    public static ReplyNeedVerdict Classify(
        string? preview,
        bool? hasLastMessage,
        string? lastMessageType,
        TimeSpan? waitingFor) => Classify(preview, hasLastMessage, lastMessageType, waitingFor, null, null);

    /// <param name="lastMessageFromMe">
    /// Whether the last entry was sent by the owner. Only consulted for call logs today, where it is the
    /// difference between "a customer rang and got no answer" and "we rang them".
    /// </param>
    public static ReplyNeedVerdict Classify(
        string? preview,
        bool? hasLastMessage,
        string? lastMessageType,
        TimeSpan? waitingFor,
        bool? lastMessageFromMe) =>
        Classify(preview, hasLastMessage, lastMessageType, waitingFor, lastMessageFromMe, null);

    /// <param name="lastCallOutcome">
    /// WhatsApp's verdict on a call entry. Empty when unknown, which keeps the call counted.
    /// </param>
    public static ReplyNeedVerdict Classify(
        string? preview,
        bool? hasLastMessage,
        string? lastMessageType,
        TimeSpan? waitingFor,
        bool? lastMessageFromMe,
        string? lastCallOutcome)
    {
        var text = (preview ?? string.Empty).Trim();

        // Checked before the text, because these are not messages and their content is WhatsApp's, not a
        // customer's. Measured on real data: of 212 conversations the app was reporting as customers
        // waiting, 39 were security-code changes and protocol notices — nobody wrote them and nobody is
        // waiting on an answer. They accounted for a large share of the "unreadable" previews.
        if (IsSystemNoticeType(lastMessageType))
        {
            return new ReplyNeedVerdict(false, ReplyNeedReason.SystemNotice);
        }

        // A missed call IS worth returning, so it stays counted — but 36 of those 212 were calls showing up
        // as messages with no readable text, which told the owner nothing about what to do.
        //
        // DIRECTION MATTERS, and this branch used to ignore it: every call-log entry became "missed call ·
        // needs reply · Call back", including calls the SALON PLACED. Read live over CDP against the
        // owner's own accounts: of the call-log entries sitting at the end of a chat, one WhatsApp session
        // had 3 of 19 outbound and another had 7 of 7 outbound. Prompting someone to "call back" a number
        // they themselves rang is not a small cosmetic error — it is work invented out of nothing.
        if (IsCallType(lastMessageType))
        {
            if (lastMessageFromMe == true)
            {
                return new ReplyNeedVerdict(false, ReplyNeedReason.OutgoingCall);
            }

            // An inbound call that WhatsApp says was answered is not a missed call. This is the owner's
            // reported case: "Voice call — Accepted on another device", queued with a Call back button
            // for someone they had already spoken to on their phone.
            return IsAnsweredCallOutcome(lastCallOutcome)
                ? new ReplyNeedVerdict(false, ReplyNeedReason.CallAnswered)
                : new ReplyNeedVerdict(true, ReplyNeedReason.MissedCall);
        }

        if (text.Length == 0)
        {
            // Positively reported as having no message, and old enough that a still-syncing store is not a
            // credible explanation — WhatsApp fills message bodies in seconds, not days. Observed live: a
            // chat 57 days old with no body, and nothing in the thread when the owner opened it.
            //
            // The age gate is what makes this safe on the IndexedDB path, where a throttled background
            // webview legitimately has no persisted lastMessage yet. Recent chats keep their place.
            if (hasLastMessage == false && waitingFor is { } waited && waited > MissingMessageIsGoneAfter)
            {
                return new ReplyNeedVerdict(false, ReplyNeedReason.MessageNoLongerAvailable);
            }

            // A message exists but carries no text — an uncaptioned photo or voice note. Say what it is
            // instead of leaving the row blank, and keep it counted.
            if (IsMediaType(lastMessageType))
            {
                return new ReplyNeedVerdict(true, ReplyNeedReason.MediaWithoutCaption);
            }

            // Nothing was read and nothing explains why. The app has no basis to close this, and saying
            // otherwise would be inventing a judgement.
            return new ReplyNeedVerdict(true, ReplyNeedReason.NoPreviewAvailable);
        }

        // A question beats every closing rule below, including its own. Checked first and unconditionally
        // so "ok but what time" can never be filed as an "ok".
        if (AsksSomething(text))
        {
            return new ReplyNeedVerdict(true, ReplyNeedReason.AsksSomething);
        }

        if (IsMediaPlaceholder(text))
        {
            return new ReplyNeedVerdict(true, ReplyNeedReason.MediaWithoutCaption);
        }

        // No letters and no digits in any script — a thumbs-up, a heart, "...". Nothing is being asked.
        if (!HasWordCharacter(text))
        {
            return new ReplyNeedVerdict(false, ReplyNeedReason.EmojiOnly);
        }

        var words = Tokenize(text);
        if (words.Count == 0 || words.Count > MaxClosingWords)
        {
            return new ReplyNeedVerdict(true, ReplyNeedReason.Substantive);
        }

        // Every word has to be a closing word. One unrecognised word and the message keeps its place in
        // the queue — that is the safe direction, and it is why "near chandni chok" stays counted.
        foreach (var word in words)
        {
            if (!IsClosingWord(word))
            {
                return new ReplyNeedVerdict(true, ReplyNeedReason.Substantive);
            }
        }

        // A bare opening greeting comes back null: every word was recognised, but what they add up to is
        // someone starting a conversation, not finishing one.
        return ClassifyClosing(words) is { } reason
            ? new ReplyNeedVerdict(false, reason)
            : new ReplyNeedVerdict(true, ReplyNeedReason.Substantive);
    }

    /// <summary>
    /// True when the text contains any interrogative or request term. Word-boundary matched so "book"
    /// fires on "book me" and not on "facebook".
    /// </summary>
    public static bool AsksSomething(string? preview)
    {
        var text = (preview ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return false;
        }

        if (text.Contains('?') || text.Contains('？') || text.Contains('؟'))
        {
            return true;
        }

        var words = Tokenize(text);
        foreach (var word in words)
        {
            foreach (var request in RequestWords)
            {
                if (string.Equals(word, request, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// True when the whole preview is WhatsApp's stand-in for a message with no text — "Photo", "Voice".
    /// A caption alongside it means there IS text to judge, so only a bare placeholder matches.
    /// </summary>
    internal static bool IsMediaPlaceholder(string text)
    {
        var trimmed = text.Trim().TrimEnd('.', '!', ' ');
        return MediaPlaceholders.Contains(trimmed);
    }

    /// <summary>
    /// A word that can appear in a closing message. The union matters: the greeting list is a *subset*
    /// used only to pick the right reason label, and keeping the two sets separate once meant
    /// "walaikum us salam" failed the closing check on the word "us" alone.
    /// </summary>
    private static bool IsClosingWord(string word) =>
        ClosingWords.Contains(word) || GreetingWords.Contains(word);

    /// <summary>
    /// A greeting that <b>opens</b> a conversation rather than ending one. These must never close a chat.
    ///
    /// <para>
    /// Caught by running the classifier over real traffic: a bare "Hi" and a bare "Salam" were being
    /// dismissed as sign-offs. They are the opposite — a customer who said "Salam" and got nothing back
    /// is an <i>unanswered new lead</i>, which for a salon is the single most expensive thing to drop. A
    /// reciprocal greeting is different in kind and in the word itself: "walaikum" means "and upon you",
    /// so it can only ever be an answer to a greeting, never the start of one.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> OpeningGreetings = new(StringComparer.OrdinalIgnoreCase)
    {
        "hi", "hello", "hey", "salam", "salaam", "slam", "assalam", "assalamualaikum",
        "asalamualaikum", "aoa", "السلام", "علیکم"
    };

    /// <summary>
    /// Words that can only ever be an <i>answer</i> to a greeting. "Walaikum" is literally "and upon
    /// you" — it cannot start a conversation, so unlike a bare "salam" it does close one. Kept separate
    /// because "walaikum us salam" contains "salam", and matching on that alone put a reply in the same
    /// bucket as an opener.
    /// </summary>
    private static readonly HashSet<string> ReciprocalGreetings = new(StringComparer.OrdinalIgnoreCase)
    {
        "walaikum", "walaikumsalam", "wsalam", "walikum", "waalaikum", "وعلیکم"
    };

    private static ReplyNeedReason? ClassifyClosing(List<string> words)
    {
        var sawAcknowledgement = false;
        var sawOpeningGreeting = false;

        foreach (var word in words)
        {
            if (ReciprocalGreetings.Contains(word))
            {
                // Answering a greeting ends the exchange. Nothing else in the message can make it an
                // opener, so this settles it outright.
                return ReplyNeedReason.GreetingOrSignOff;
            }
        }

        foreach (var word in words)
        {
            if (OpeningGreetings.Contains(word))
            {
                sawOpeningGreeting = true;
            }
            else if (!GreetingWords.Contains(word) && ClosingWords.Contains(word))
            {
                sawAcknowledgement = true;
            }
        }

        // "Salam" on its own opens a conversation; "ok salam" ends one. The acknowledgement is what makes
        // the difference, so an opening greeting only closes a chat when something else already did.
        if (sawOpeningGreeting && !sawAcknowledgement)
        {
            return null;
        }

        // Only report "greeting" when that is genuinely all it was — "ok salam" is an acknowledgement
        // with a sign-off attached, and the owner reading the reason should see the part that matters.
        return sawAcknowledgement ? ReplyNeedReason.Acknowledgement : ReplyNeedReason.GreetingOrSignOff;
    }

    private static readonly HashSet<string> GreetingWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "salam", "salaam", "slam", "assalam", "assalamualaikum", "asalamualaikum", "walaikum",
        "walaikumsalam", "wsalam", "aoa", "hi", "hello", "hey", "bye", "goodbye", "gudbye",
        "tc", "care", "take", "cu", "khuda", "allah", "hafiz", "us", "o", "sir", "madam", "mam",
        "miss", "dear", "bhai", "baji", "api", "apa",
        "السلام", "علیکم", "وعلیکم", "خدا", "حافظ"
    };

    private static bool HasWordCharacter(string text)
    {
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Splits into comparable words: strips punctuation and emoji, keeps letters and digits in every
    /// script so Urdu text survives, and drops the variation selectors and skin-tone modifiers that
    /// otherwise leave an invisible "word" behind after an emoji.
    /// </summary>
    internal static List<string> Tokenize(string text)
    {
        var words = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(char.ToLowerInvariant(c));
                continue;
            }

            if (c == '\'' || c == '’')
            {
                continue; // "you're" -> "youre", matching the lexicon
            }

            if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return words;
    }
}
