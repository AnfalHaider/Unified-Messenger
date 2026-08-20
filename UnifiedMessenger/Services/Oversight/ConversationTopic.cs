namespace UnifiedMessenger.Services;

/// <summary>What a waiting customer appears to want, so the queue can be filtered by it.</summary>
public enum ConversationTopic
{
    /// <summary>Nothing in the message identifies a topic.</summary>
    Unknown,

    /// <summary>A complaint, a price objection, or a stated intention to go elsewhere.</summary>
    AtRisk,

    /// <summary>Asking about price, services or availability — money on the table.</summary>
    Enquiry,

    /// <summary>Arranging, confirming, moving or cancelling an appointment.</summary>
    Booking,

    /// <summary>Asking about work, or replying to a hiring or training form. Not customer service.</summary>
    JobApplicant,

    /// <summary>Inbound business outreach, marketing, or an automated notice.</summary>
    BusinessOutreach
}

/// <summary>
/// Sorts a waiting conversation by what the customer wants.
///
/// <para>
/// <b>Why these six.</b> The obvious taxonomy — urgent / leads / follow-up — describes a generic sales
/// inbox and would have missed the two largest groups in this owner's actual queue. Roughly 25 of their
/// waiting conversations are people asking about jobs or replying to a training form ("I need jop dear",
/// "I fill the form", plus a run of bare names), and a handful are B2B outreach ("reaching out from SJP
/// Photography_Films"). Neither is customer service, and being able to set both aside clears more of the
/// list than any amount of urgency ranking.
/// </para>
/// <para>
/// <b>AtRisk is checked first and deliberately over-reaches.</b> The whole reason the count needed fixing
/// is that a customer reporting bruising and another saying <i>"warna main kahin aur chali jawon"</i> —
/// otherwise I'll go elsewhere — were invisible among four hundred "ok"s. A false positive here costs a
/// second glance; a false negative costs the customer.
/// </para>
/// <para>
/// This classifies <b>topic</b> only. Whether a reply is owed at all is <see cref="ReplyNeed"/>'s job, and
/// the two are kept separate so a filter can never quietly change a count.
/// </para>
/// </summary>
public static class ConversationTopics
{
    // Complaints and churn signals. Roman Urdu carries most of the churn language here: "kahin aur" is
    // "somewhere else", "chali jawon" is "I'll go", "wapas" is "back/refund" in this context.
    private static readonly string[] AtRiskTerms =
    [
        "complaint", "complain", "unprofessional", "rude", "bad service", "worst", "terrible", "awful",
        "horrible", "disappointed", "disappointing", "unhappy", "not happy", "never coming", "never again",
        "refund", "money back", "compensate", "ruined", "damaged", "damage", "burn", "burnt", "burned",
        "bruise", "bruises", "bruised", "rash", "allergic", "infection", "pain", "hurt", "bleeding",
        "wrong", "mistake", "poor", "waste", "wasted", "cheated", "fraud", "scam", "overcharged",
        "expensive", "costly", "too much", "mehnga", "mehngi", "zyada",
        "kahin aur", "kisi aur", "chali jawon", "chala jaunga", "nahi aaungi", "nahi aaunga",
        "shikayat", "kharab", "ganda", "bura", "galat", "wapas", "paise wapas", "bakwas",
        "شکایت", "خراب", "غلط", "برا", "مہنگا",

        // BEING IGNORED IS A CHURN SIGNAL, and it was the largest miss in this list. Measured against the
        // owner's real queue: "At risk" showed 1, while sitting unclassified in "Uncategorised" were
        // "Apny koi reply nhi kiya dubara" (you didn't reply again), "Reply me please" and "Or else share
        // your pr team number". A customer chasing a reply is one message from leaving, and this list
        // exists precisely so those are not the ones that stay buried.
        "no reply", "not replying", "didn't reply", "didnt reply", "did not reply", "no response",
        "not responding", "no answer", "still waiting", "waiting since", "waiting for your",
        // NOT a bare "respond": it matched "…we will respond shortly" in another business's out-of-hours
        // auto-reply and filed that as a customer about to leave. The chasing phrasings are specific.
        "any update", "any response", "reply me", "answer me", "please respond", "kindly respond",
        "ignoring", "ignored",
        "or else", "second time", "again and again",
        "reply nahi", "reply nhi", "jawab nahi", "jawab nhi", "koi reply", "koi jawab",
        "dubara", "dobara", "phir se", "response nahi", "response nhi",
        "جواب نہیں", "دوبارہ"
    ];

    private static readonly string[] JobTerms =
    [
        "job", "jop", "jab", "vacancy", "vacancies", "hiring", "hire me", "position", "post available",
        "cv", "resume", "apply", "applied", "application", "internship", "intern", "trainee", "training",
        "course", "academy", "certificate", "diploma", "salary", "stipend", "experience letter",
        "naukri", "nokri", "kaam chahiye", "kam chahiye", "job chahiye", "jop chahiye",
        "fill the form", "filled the form", "submitted", "i submitted", "registered",
        "نوکری", "کام چاہیے", "فارم"
    ];

    private static readonly string[] BookingTerms =
    [
        "appointment", "appointments", "book", "booking", "booked", "reserve", "reservation", "slot",
        "reschedule", "postpone", "cancel my", "confirm my", "coming at", "come at", "be there",
        // Bare day-words are deliberately absent. "aaj" (today) and "kal" (tomorrow) appear in all sorts of
        // messages, and including them filed "aaj ka … kiya charge kerain gaye" — a price question — as a
        // booking, because Booking is checked before Enquiry. A day-word is context, not intent.
        "اپائنٹمنٹ", "بکنگ"
    ];

    private static readonly string[] EnquiryTerms =
    [
        "price", "prices", "pricing", "rate", "rates", "charge", "charges", "charging", "cost", "fee",
        "fees", "package", "packages", "deal", "discount", "offer", "how much", "kitna", "kitni", "kitne",
        "kitnay", "kimat", "qeemat", "services", "service", "menu", "list", "available", "availability",
        "open", "timing", "timings", "address", "location", "where are you", "do you do", "do you have",
        "قیمت", "ریٹ", "سروس", "پتہ",

        // ROMAN URDU, WHICH THE ORIGINAL LIST BARELY COVERED. Every term below is from a real message that
        // was landing in "Uncategorised" — "AP srves dety ho", "Bramch kb close hgi", "Possible hai ?",
        // "Aap please mujha actual amount mention kr dain". These are customers asking what things cost and
        // when the branch is open, which is the single most valuable thing in the queue to answer.
        "srves", "srvices", "servs", "dety ho", "dety hain", "deti ho", "krty ho", "karty ho", "krte ho",
        "kb", "kab", "kis din", "kon si", "konsi", "which date", "what date",
        "branch", "bramch", "close hgi", "close hogi", "khulta", "khulti", "band hoti", "band hota",
        "possible hai", "possible h", "ho sakta", "ho sakti", "hosakta",
        "amount", "total bill", "actual amount", "mention kr", "batao", "bata dain", "bta dn", "bta den",
        "scheme", "stylist", "artist", "loyalty", "makeup look", "look of", "signature",
        "kya rate", "rate kya", "kitna time", "time kya"
    ];

    private static readonly string[] OutreachTerms =
    [
        "reaching out", "reach out", "i'm from", "im from", "we are from", "on behalf of", "collab",
        "collaboration", "partnership", "partner with", "promote your", "grow your", "our agency",
        "our company", "marketing services", "seo", "leads for", "digital marketing", "influencer",
        "barter", "pr package", "media kit", "invoice attached", "quotation attached",
        "attendance is compulsory", "scan the qr", "unavailable right now", "this is an automated",
        // Auto-replies from other businesses. Caught by the probe: a clinic's out-of-hours responder was
        // landing under job applicants, which is where an unmatched message with a stray "intern" inside
        // "International" ended up.
        "currently unavailable", "leave your message", "thank you for contacting", "we will get back",
        "our team is", "office hours", "out of office"
    ];

    /// <summary>
    /// Classifies a waiting conversation from the customer's last message.
    /// </summary>
    /// <remarks>
    /// Order is the design. AtRisk wins outright — a complaint that also mentions a price is a complaint,
    /// not an enquiry. Outreach is checked before the commercial topics so "promote your salon, our rates
    /// are…" is not filed as a customer asking about rates. Job comes before booking because a training
    /// enrolment ("filled the form, when does the course start") is a hiring matter, not an appointment.
    /// </remarks>
    public static ConversationTopic Classify(string? preview)
    {
        var text = (preview ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ConversationTopic.Unknown;
        }

        if (ContainsAny(text, AtRiskTerms))
        {
            return ConversationTopic.AtRisk;
        }

        if (ContainsAny(text, OutreachTerms))
        {
            return ConversationTopic.BusinessOutreach;
        }

        if (ContainsAny(text, JobTerms))
        {
            return ConversationTopic.JobApplicant;
        }

        if (ContainsAny(text, BookingTerms))
        {
            return ConversationTopic.Booking;
        }

        if (ContainsAny(text, EnquiryTerms))
        {
            return ConversationTopic.Enquiry;
        }

        // LAST, and last on purpose. A bare personal name is the answer to "what name should I put the
        // appointment under?" — it was about a quarter of everything sitting in "Uncategorised" on the
        // owner's real queue ("Aiza Anwar", "Hira Sabir", "Palwasha zaib", "Talha Uzair", …) while the
        // Bookings chip read 3. Running it after every keyword rule means a message that merely happens to
        // start with a capitalised word cannot outrank a real complaint, enquiry or job application.
        if (LooksLikeBareName(text))
        {
            return ConversationTopic.Booking;
        }

        return ConversationTopic.Unknown;
    }

    /// <summary>
    /// Words that disqualify a short capitalised phrase from being read as somebody's name.
    /// </summary>
    /// <remarks>
    /// Without this the rule filed "For mens", "See you", "No need", "Send me here" and "What is this" as
    /// bookings. They are two or three capitalised-looking words and nothing else, so only a vocabulary
    /// check separates them from "Hira Sabir".
    /// </remarks>
    private static readonly HashSet<string> NotNameWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "for", "and", "the", "on", "in", "at", "to", "of", "is", "are", "am", "be", "was", "were",
        "my", "me", "you", "your", "we", "us", "our", "it", "this", "that", "these", "those",
        "no", "not", "yes", "ok", "okay", "see", "how", "what", "when", "where", "why", "which", "who",
        "can", "could", "do", "does", "did", "will", "would", "should", "shall", "may", "might",
        "send", "need", "want", "give", "tell", "call", "come", "go", "get", "make", "take", "let",
        "please", "thanks", "thank", "hi", "hello", "hey", "salam", "sir", "madam", "mam", "miss",
        "here", "there", "now", "then", "today", "tomorrow", "yesterday", "time", "date", "day",
        "kya", "kia", "hai", "hain", "ho", "hy", "ap", "aap", "mujhe", "mujha", "kar", "kr", "ki", "ka",
        "se", "me", "mein", "nahi", "nhi", "koi", "sab", "bhi", "abhi",
        // Locatives and fillers. "Near chandni chok" is a direction, not a person, and without these the
        // rule reads any capitalised two- or three-word fragment as somebody's name.
        "near", "next", "opposite", "behind", "front", "side", "road", "street", "chok", "chowk",
        "branch", "shop", "plaza", "market", "block", "phase", "sector", "town", "city",
        "islamabad", "lahore", "karachi", "pindi", "rawalpindi",
        "both", "all", "some", "any", "only", "just", "well"
    };

    /// <summary>
    /// True when the message is nothing but a person's name — two or three alphabetic words, the first
    /// capitalised, no digits, no question mark, and no word that belongs to ordinary conversation.
    /// </summary>
    /// <remarks>
    /// <b>Two words minimum, deliberately.</b> A single word is far too ambiguous: "Please", "well" and
    /// "Appreciated" are all single capitalised words that are not names, and mis-filing those would put
    /// real requests under Bookings. Requiring at least two words gives up a handful of single-name replies
    /// in exchange for a rule that does not guess.
    ///
    /// <para>
    /// Note this only ever moves a row between buckets — a name still counts as needing a reply. Nothing
    /// here can close a conversation, which is why a wrong guess costs a filter position and not a customer.
    /// </para>
    /// </remarks>
    internal static bool LooksLikeBareName(string text)
    {
        if (text.Length is 0 or > 32 || text.Contains('?') || text.Any(char.IsDigit))
        {
            return false;
        }

        // Whitespace only, then trim trailing punctuation. Splitting on '.' as well would turn
        // "Islamabad.pdf" into two clean alphabetic words and file an attachment as somebody's name.
        var words = text
            .Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.TrimEnd('.', ',', '!'))
            .Where(w => w.Length > 0)
            .ToArray();

        if (words.Length is < 2 or > 3)
        {
            return false;
        }

        if (!char.IsUpper(words[0][0]))
        {
            return false;
        }

        foreach (var word in words)
        {
            if (!word.All(char.IsLetter) || NotNameWords.Contains(word))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The chip label for a topic.</summary>
    public static string Label(ConversationTopic topic) => topic switch
    {
        ConversationTopic.AtRisk => "At risk",
        ConversationTopic.Enquiry => "Enquiries",
        ConversationTopic.Booking => "Bookings",
        ConversationTopic.JobApplicant => "Job & training",
        ConversationTopic.BusinessOutreach => "Business outreach",
        _ => "Uncategorised"
    };

    /// <summary>What the filter is actually selecting, for the chip's tooltip.</summary>
    public static string Describe(ConversationTopic topic) => topic switch
    {
        ConversationTopic.AtRisk =>
            "Complaints, price objections, and customers saying they will go elsewhere. Deliberately "
            + "over-inclusive — a wrong guess here costs you a second glance, missing one costs a customer.",
        ConversationTopic.Enquiry =>
            "Asking about price, services, timings or availability — the ones with money attached.",
        ConversationTopic.Booking => "Making, moving or cancelling an appointment.",
        ConversationTopic.JobApplicant =>
            "People asking about work, or replying to a hiring or training form. Not customer service — "
            + "filter these out to see the customers.",
        ConversationTopic.BusinessOutreach =>
            "Agencies, marketers and automated notices sent to your business rather than customers.",
        _ => "No topic could be identified from the last message."
    };

    /// <summary>
    /// One rule, chosen because plain substring matching failed against real traffic: <b>a term containing
    /// a space is matched as a substring; a single word is matched whole.</b>
    ///
    /// <para>
    /// Multi-word phrases need substring matching — "kahin aur", "money back" and "reaching out" all span
    /// punctuation and inflection, and demanding word boundaries around the whole phrase would miss them.
    /// Single words must not, and the reason is specific: the first version flagged <i>"Madiha rashid"</i>
    /// as a complaint because "rash" appears inside "Rashid", and filed a clinic's auto-reply under jobs
    /// because "intern" appears inside "International". Rashid and Burney are common surnames here, so that
    /// first bug would have fired constantly — in the one list that has to be trustworthy.
    /// </para>
    /// <para>
    /// Boundaries are non-letter/digit, so "mehngi," and "(expensive)" still match while "Spain" does not
    /// match "pain".
    /// </para>
    /// </summary>
    private static bool ContainsAny(string text, string[] terms)
    {
        foreach (var term in terms)
        {
            var matched = term.Contains(' ')
                ? text.Contains(term, StringComparison.OrdinalIgnoreCase)
                : ContainsWord(text, term);

            if (matched)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsWord(string text, string word)
    {
        var index = 0;
        while (index <= text.Length - word.Length)
        {
            var found = text.IndexOf(word, index, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                return false;
            }

            var before = found == 0 || !char.IsLetterOrDigit(text[found - 1]);
            var afterIndex = found + word.Length;
            var after = afterIndex >= text.Length || !char.IsLetterOrDigit(text[afterIndex]);

            if (before && after)
            {
                return true;
            }

            index = found + 1;
        }

        return false;
    }
}
