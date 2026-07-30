namespace UnifiedMessenger.Models;

/// <summary>
/// What a channel can honestly contribute to oversight. Declared per platform on
/// <see cref="PlatformDefinition.Capabilities"/> so consumers ask a question instead of hard-coding a
/// platform-id list — the pattern that previously forced every new metric through the WhatsApp-only
/// <c>PlatformModuleSettingsHelper.IsPlatformModuleEnabled</c> gate.
/// </summary>
/// <remarks>
/// <para><b>Two kinds of flag live here, and the distinction matters.</b></para>
/// <list type="number">
/// <item><b>Platform facts</b> — <see cref="IsMessageChannel"/> and
/// <see cref="RequiresThreadOpenToRead"/>. Immutable truths about the channel itself. They do not change
/// when we write a better adapter. <see cref="RequiresThreadOpenToRead"/> in particular is a standing
/// <i>prohibition</i>, so it is declared before any code exists that could violate it.</item>
/// <item><b>Adapter capabilities</b> — everything else. These describe what the adapter
/// <i>currently implements</i>, not what the platform could theoretically support. A platform whose
/// adapter is still <c>NullPlatformAdapter</c> declares them all false, and the task that builds the
/// adapter flips each flag as it lands the corresponding read. Never set one optimistically: a true flag
/// is a promise that a consumer can read that field today.</item>
/// </list>
/// </remarks>
public sealed record PlatformCapabilities
{
    // ---- Platform facts (never change; not tied to adapter progress) --------------------------------

    /// <summary>
    /// True when the channel carries customer conversations at all. False for review/management surfaces
    /// such as Google Business (reviews + Q&amp;A only — Google Business Messages was shut down in July 2024
    /// and its
    /// data deleted, so there is no message channel to build) and for pure embeds like Discord or a custom
    /// URL. A false value means awaiting-reply, response-time and message-volume metrics are meaningless
    /// here and must not be requested, computed, or shown as zero.
    /// </summary>
    public bool IsMessageChannel { get; init; }

    /// <summary>
    /// True when the platform's per-conversation detail cannot be read without a user-visible side effect
    /// — Meta's web clients mark a thread read and fire a read receipt to the customer the moment the
    /// thread is opened. For an oversight app that is disqualifying: measuring the awaiting-reply signal
    /// would destroy it and tell the customer we looked. When this is true the adapter must have <b>no</b>
    /// thread-open code path, and only aggregate badge-level reads are permitted.
    /// </summary>
    public bool RequiresThreadOpenToRead { get; init; }

    // ---- Adapter capabilities (flip these as an adapter actually implements them) -------------------

    /// <summary>The adapter can read a per-conversation unread/awaiting signal.</summary>
    public bool CanReadUnread { get; init; }

    /// <summary>The adapter can read last-message preview text.</summary>
    public bool CanReadPreview { get; init; }

    /// <summary>The adapter can read per-message timestamps (required for any elapsed-time metric).</summary>
    public bool CanReadTimestamps { get; init; }

    /// <summary>The adapter can read outbound delivery/read acknowledgement state.</summary>
    public bool CanReadAck { get; init; }

    /// <summary>The adapter can resolve a stable customer identity (phone number or saved contact name).</summary>
    public bool CanReadContactIdentity { get; init; }

    /// <summary>
    /// The adapter can supply data good enough for forward-tracked First Response Time. Requires
    /// <see cref="CanReadTimestamps"/> plus message direction. When false the channel must be excluded
    /// from the on-time% <i>denominator</i> rather than counted as a miss — a channel we cannot measure
    /// is not a channel that failed.
    /// </summary>
    public bool SupportsFrt { get; init; }

    /// <summary>
    /// Routes this platform into the WhatsApp IndexedDB pipelines (history backfill, the WhatsApp adapter,
    /// delivery-status UI, dashboard analytics). Deliberately narrow — it is the data form of the
    /// long-standing "do NOT broaden this" rule. A new channel earns oversight metrics by declaring the
    /// capability flags above and shipping its own adapter, never by joining this pipeline.
    /// </summary>
    public bool UsesWhatsAppIndexedDbPipeline { get; init; }

    /// <summary>An embed with no oversight contribution: visible and usable, but never measured.</summary>
    public static readonly PlatformCapabilities EmbedOnly = new();

    /// <summary>
    /// True when this channel contributes any conversation metric today. Used to decide whether an account
    /// belongs in the command center at all — a channel that contributes nothing would otherwise strand at
    /// "syncing…" forever.
    /// </summary>
    public bool ContributesConversationMetrics => IsMessageChannel && CanReadUnread;

    /// <summary>
    /// True when only aggregate counts are readable — the channel is a message channel with an unread
    /// signal, but per-conversation detail is off limits (Meta) or simply not implemented yet. Callers
    /// should render a count and explicitly say detail is unavailable, rather than showing an empty list.
    /// </summary>
    public bool IsAggregateOnly =>
        ContributesConversationMetrics && (RequiresThreadOpenToRead || !CanReadPreview);
}
