namespace UnifiedMessenger.Models;

public sealed class PlatformDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public string Description { get; init; } = string.Empty;

    public required string DefaultUrl { get; init; }

    public string IconGlyph { get; init; } = "\uE774";

    public string AccentColor { get; init; } = "#6B7280";

    /// <summary>
    /// What this channel can honestly contribute to oversight. Defaults to
    /// <see cref="PlatformCapabilities.EmbedOnly"/> — a new platform is measured only once it declares a
    /// capability and ships the adapter that backs it. See <see cref="PlatformCapabilities"/> for the
    /// platform-fact vs adapter-capability distinction.
    /// </summary>
    public PlatformCapabilities Capabilities { get; init; } = PlatformCapabilities.EmbedOnly;

    /// <summary>Capabilities for <paramref name="platformId"/>, or embed-only for anything unrecognised.</summary>
    public static PlatformCapabilities CapabilitiesFor(string? platformId) =>
        FindById(NormalizePlatformId(platformId))?.Capabilities ?? PlatformCapabilities.EmbedOnly;

    /// <summary>
    /// Whether this platform's tab lets the owner type any address and browse freely. True only for the
    /// generic "Custom URL" platform: a real service tab is pinned to its own site by the navigation
    /// guard, so an address bar there would only ever produce blocked navigations.
    /// </summary>
    /// <remarks>Derived from <see cref="DefaultUrl"/> being empty — the same signal the start-URL
    /// resolver already uses to decide a platform isn't host-restricted, so the two can't disagree.</remarks>
    public bool AllowsCustomUrl => string.IsNullOrWhiteSpace(DefaultUrl);

    /// <summary>Convenience lookup by platform id; unknown ids are treated as pinned (the safe default).</summary>
    public static bool AllowsFreeBrowsing(string? platformId) =>
        FindById(platformId)?.AllowsCustomUrl ?? false;

    // WhatsApp and WhatsApp Business share one adapter, so they share one capability set.
    // CanReadPreview is earned by the in-page Store bridge (whatsapp-store-bridge.js), which reads
    // WhatsApp Web's already-decrypted in-memory models: verified live at 82-88% preview coverage
    // across ALL chats, not the ~60 rendered sidebar rows the IndexedDB path was limited to (bodies
    // are encrypted at rest in msgRowOpaqueData). CanReadAck stays FALSE: the bridge reads message
    // direction (message.id.fromMe), not delivery/read acks - those are still only DOM tick glyphs.
    private static readonly PlatformCapabilities WhatsAppFamily = new()
    {
        IsMessageChannel = true,
        RequiresThreadOpenToRead = false,
        CanReadUnread = true,
        CanReadPreview = true,
        CanReadTimestamps = true,
        CanReadAck = false,
        CanReadContactIdentity = true,
        SupportsFrt = true,
        UsesWhatsAppIndexedDbPipeline = true
    };

    // Meta web clients mark a thread READ and fire a read receipt to the customer the moment it is opened,
    // so per-conversation reads are permanently off limits here — badge-level aggregates only. The flag is
    // set before any Meta adapter exists precisely so it constrains whoever writes one.
    private static readonly PlatformCapabilities MetaAggregateOnly = new()
    {
        IsMessageChannel = true,
        RequiresThreadOpenToRead = true
    };

    /// <summary>
    /// Instagram Direct (A13). Reads the client's own already-fetched Relay records on the feed — no
    /// navigation, no query, no thread opened.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="PlatformCapabilities.RequiresThreadOpenToRead"/> stays true, like the rest of Meta.</b>
    /// A first draft of this set it false, reasoning that the thread list needs no thread opened — which is
    /// true of the list and false of everything else. Reading a message body on Instagram still means
    /// opening the conversation and firing a read receipt at a real customer, and the flag exists to
    /// constrain whoever writes that code next. Weakening a safety flag because one route happens not to
    /// need it is how the constraint gets lost; the classification was the thing that was wrong, and
    /// <see cref="PlatformCapabilities.IsAggregateOnly"/> was fixed instead.
    /// </para>
    /// <para>
    /// <b><see cref="PlatformCapabilities.CanReadPreview"/> is false and will stay false on this route.</b>
    /// The feed's prefetch carries thread metadata only; a sweep for any snippet-shaped field returns
    /// empty. Message text is fetched by the Direct route the app does not open.
    /// </para>
    /// <para>
    /// <b><see cref="PlatformCapabilities.SupportsFrt"/> is false</b>, so Instagram is excluded from the
    /// on-time denominator rather than scored as a miss on timing it cannot supply.
    /// </para>
    /// </remarks>
    private static readonly PlatformCapabilities InstagramDirect = new()
    {
        IsMessageChannel = true,
        CanReadUnread = true,
        CanReadTimestamps = true,
        CanReadContactIdentity = true,
        CanReadPreview = false,
        RequiresThreadOpenToRead = true,
        SupportsFrt = false,
        UsesWhatsAppIndexedDbPipeline = false
    };

    public static IReadOnlyList<PlatformDefinition> All { get; } =
    [
        new PlatformDefinition
        {
            Id = "whatsapp",
            DisplayName = "WhatsApp",
            Description = "Full oversight — who's waiting, response times, on-time %.",
            DefaultUrl = "https://web.whatsapp.com/",
            IconGlyph = "\uE8BD",
            AccentColor = "#25D366",
            Capabilities = WhatsAppFamily
        },
        new PlatformDefinition
        {
            Id = "whatsappbusiness",
            DisplayName = "WhatsApp Business",
            Description = "Full oversight — who's waiting, response times, on-time %.",
            DefaultUrl = "https://web.whatsapp.com/",
            IconGlyph = "\uE8BD",
            AccentColor = "#128C7E",
            Capabilities = WhatsAppFamily
        },
        new PlatformDefinition
        {
            // Google Business reviews, embedded as a first-class channel. Routes to the NullPlatformAdapter
            // for now (no metric scraping yet — a GoogleBusinessAdapter that reads rating / % responded /
            // unanswered from the embedded DOM is future work that needs a live logged-in account to tune).
            Id = "googlebusiness",
            DisplayName = "Google Business",
            // Keep this in step with what GoogleReviewSnapshotService actually ships. It previously read
            // "(Review metrics scraping is planned.)" long after that scraping shipped, and the drift went
            // unnoticed for releases because nothing rendered Description. Something renders it now.
            Description = "Reviews and Q&A — rating, unanswered reviews, reply rate. No message channel.",
            // Land on the Business Profile Manager REVIEWS page — that's the oversight surface for this app
            // (which reviews need a reply). The bare business.google.com root redirects single-location
            // managers into a raw Google Search results page instead. (Google moved single-location editing
            // into Search/Maps, but the /reviews manager view still works.)
            DefaultUrl = "https://business.google.com/reviews",
            IconGlyph = "\uE774", // TODO: Replace with brand-specific glyph or image asset when Phase 5 is implemented.
            AccentColor = "#4285F4",
            // Reviews + Q&A only, permanently: Google Business Messages was shut down in July 2024 and the
            // data deleted. So IsMessageChannel is false and stays false. Google DOES contribute review
            // metrics (GoogleReviewSnapshotService: rating, lifetime total, unanswered, reply rate) - those
            // are a separate review-health surface, not conversation metrics, so they are not modelled here.
            Capabilities = PlatformCapabilities.EmbedOnly
        },
        new PlatformDefinition
        {
            // Telegram Web, embedded as a channel. NullPlatformAdapter for now — a Telegram adapter that
            // reads unread/awaiting from the web DOM (like the WhatsApp one) is future per-channel work.
            Id = "telegram",
            DisplayName = "Telegram",
            // Offered in the picker like every other channel. The description is held to the same bar:
            // say what it does now, not what it might do later. PlatformDescriptionTests enforces both
            // directions, which is what makes offering an unmeasured channel honest rather than misleading.
            Description = "Opens in its own tab. No oversight metrics — this channel is not measured.",
            DefaultUrl = "https://web.telegram.org/",
            IconGlyph = "\uE8BD", // TODO: Replace with brand-specific glyph or image asset when Phase 5 is implemented.
            AccentColor = "#0088CC",
            // Embed-only until the Telegram adapter ships. Telegram is the EASIEST channel to read once
            // started (both web clients are open source, and dialogs are cached UNENCRYPTED in IndexedDB,
            // unlike WhatsApp), so expect CanReadUnread / CanReadPreview / CanReadTimestamps /
            // CanReadContactIdentity to go true together, and SupportsFrt only once history reads land.
            // Do not flip a flag ahead of the read that backs it.
            Capabilities = PlatformCapabilities.EmbedOnly
        },
        new PlatformDefinition
        {
            // Meta's Messenger Web, embedded as a channel. NullPlatformAdapter for now — a Messenger adapter
            // (and an Instagram sibling) reading unread/awaiting is future per-channel work.
            Id = "messenger",
            DisplayName = "Messenger",
            // "Planned" is not something to tell a paying customer. Opening a Meta thread fires a read
            // receipt to the customer, so per-conversation reads are permanently off limits here (see
            // MetaAggregateOnly above) — this is a browse-only channel, and the copy says so plainly.
            Description = "Opens in its own tab. No oversight metrics — this channel is not measured.",
            DefaultUrl = "https://www.messenger.com/",
            IconGlyph = "\uE8F2", // TODO: Replace with brand-specific glyph or image asset when Phase 5 is implemented.
            AccentColor = "#0084FF",
            Capabilities = MetaAggregateOnly
        },
        new PlatformDefinition
        {
            // Discord, embedded as a channel (NullPlatformAdapter — no oversight metrics). WebViewPlatform-
            // Configurator gives discord a desktop Chrome UA + in-app new-window handling so login works.
            Id = "discord",
            DisplayName = "Discord",
            Description = "Opens in its own tab. No oversight metrics — this channel is not measured.",
            DefaultUrl = "https://discord.com/app",
            AccentColor = "#5865F2",
            Capabilities = PlatformCapabilities.EmbedOnly
        },
        new PlatformDefinition
        {
            // Meta Business Suite (manage Facebook/Instagram business), embedded. NullPlatformAdapter.
            Id = "metabusinesssuite",
            DisplayName = "Meta Business Suite",
            Description = "Meta Business Suite — embedded. No oversight metrics.",
            DefaultUrl = "https://business.facebook.com/",
            AccentColor = "#0064E0",
            // Not modelled as a message channel (it is a management console), but it does surface Meta
            // inboxes, so the read-receipt prohibition applies to anything that ever scrapes it.
            Capabilities = new PlatformCapabilities { RequiresThreadOpenToRead = true }
        },
        new PlatformDefinition
        {
            // Instagram (Meta). Measured since A13 — see InstagramSnapshotReader for what it can and
            // cannot read, and why that is a property of the page rather than of the adapter.
            Id = "instagram",
            DisplayName = "Instagram",
            Description = "Who is waiting in your DMs and for how long. Message text stays in Instagram.",
            DefaultUrl = "https://www.instagram.com/",
            AccentColor = "#E4405F",
            Capabilities = InstagramDirect
        },
        new PlatformDefinition
        {
            // A generic web page monitored in its own tab. No adapter scraping and no oversight data —
            // ResolveEnabledAdapter routes "generic" to the NullPlatformAdapter. DefaultUrl is intentionally
            // empty so the user-supplied URL isn't host-restricted (ResolveStartUrl skips the host-match
            // guard when DefaultUrl is blank) and so a custom URL is required.
            Id = "generic",
            DisplayName = "Custom URL (any website)",
            Description = "Any website, in its own tab with back / forward / reload. No oversight metrics.",
            DefaultUrl = string.Empty,
            IconGlyph = "\uE774",
            AccentColor = "#6B7280",
            Capabilities = PlatformCapabilities.EmbedOnly
        }
    ];

    public static PlatformDefinition? FindById(string? platformId)
    {
        if (string.IsNullOrWhiteSpace(platformId))
        {
            return null;
        }

        return All.FirstOrDefault(p => p.Id.Equals(platformId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizePlatformId(string? platformId)
    {
        if (string.IsNullOrWhiteSpace(platformId))
        {
            return "whatsapp";
        }

        var match = All.FirstOrDefault(p => p.Id.Equals(platformId.Trim(), StringComparison.OrdinalIgnoreCase));
        return match?.Id ?? "whatsapp";
    }
}
