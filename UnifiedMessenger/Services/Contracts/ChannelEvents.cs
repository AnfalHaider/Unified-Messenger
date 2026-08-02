namespace UnifiedMessenger.Services;

/// <summary>
/// One normalized fact observed on a channel, in the shape the WhatsApp-gateway projects settled on for
/// their webhook payloads (WAHA, Evolution API): who it happened to, on which platform, when, and what
/// kind of thing it was.
/// </summary>
/// <remarks>
/// <b>In-process only.</b> These are never serialized to a network. The app has no HTTP surface and does
/// not send data anywhere — the borrowed idea here is the normalized shape, not the transport.
/// <para>
/// The point is that every channel — the WhatsApp store bridge, the IndexedDB scan, the Google review
/// scraper, and the Telegram/Messenger scrapers still to be written — reports through one type, so
/// consumers (oversight rollup, dashboard, notifications) don't each grow their own per-adapter plumbing.
/// </para>
/// </remarks>
public interface IChannelEvent
{
    string InstanceId { get; }

    string PlatformId { get; }

    DateTimeOffset TimestampUtc { get; }
}

/// <summary>A fresh oversight read landed for an instance — the rollup and dashboard can update.</summary>
public sealed record ChannelSnapshotEvent(
    string InstanceId,
    string PlatformId,
    DateTimeOffset TimestampUtc,
    int Active,
    int CaughtUp,
    int Awaiting,
    string Source) : IChannelEvent
{
    /// <summary>Which reader produced this — "store-bridge" or "indexeddb". Useful for diagnosis.</summary>
    public string Source { get; } = Source;
}

/// <summary>An instance's session lifecycle state changed.</summary>
public sealed record ChannelSessionStateEvent(
    string InstanceId,
    string PlatformId,
    DateTimeOffset TimestampUtc,
    SessionState State,
    SessionState Previous) : IChannelEvent;
