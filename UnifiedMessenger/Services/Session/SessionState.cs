using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// The lifecycle state of one account's session, in the shape WhatsApp-gateway projects converged on
/// (WAHA, Evolution API): a single value that answers "can this account produce data right now?".
/// </summary>
/// <remarks>
/// Deliberately a <b>projection</b>, not a store. The truth still lives in
/// <see cref="InstanceConnectionStatusService"/> (is the page logged in?),
/// <see cref="AdapterHealthMonitor"/> (is the scraper still reporting?) and
/// <see cref="StoreBridgeHealth"/> (which reader is live). Those three could previously disagree with
/// nothing reconciling them, and the UI only ever showed the first. <see cref="SessionStateProjection"/>
/// combines them into the one state the owner actually cares about.
/// </remarks>
public enum SessionState
{
    /// <summary>Session is booting — the page is loading or the adapter hasn't reported in yet.</summary>
    Starting,

    /// <summary>Logged out: the account needs someone to scan the QR code before any data can flow.</summary>
    ScanQr,

    /// <summary>Healthy and producing data.</summary>
    Working,

    /// <summary>Connected, but the data is going stale — the scraper stopped reporting, or reads are failing.</summary>
    Degraded,

    /// <summary>Unusable until something changes: an error the session can't recover from on its own.</summary>
    Failed
}

/// <summary>
/// Derives a <see cref="SessionState"/> for an instance by reconciling connection status, adapter health
/// and snapshot freshness. Pure and side-effect free so it can be unit-tested without a WebView.
/// </summary>
public static class SessionStateProjection
{
    /// <summary>
    /// A connected account whose snapshot is older than this is Degraded, not Working. The background
    /// monitor re-reads every 90s, so three missed cycles means something is actually wrong rather than
    /// a slow pass.
    /// </summary>
    public static readonly TimeSpan StaleSnapshotThreshold = TimeSpan.FromMinutes(5);

    public static SessionState Resolve(string instanceId, DateTimeOffset nowUtc) =>
        Resolve(
            InstanceConnectionStatusService.Instance.GetStatus(instanceId),
            OversightChatSnapshotService.Instance.TryGetCapturedAtUtc(instanceId),
            nowUtc);

    /// <summary>Testable core: everything the projection needs, passed in.</summary>
    public static SessionState Resolve(
        InstanceConnectionStatus connection,
        DateTimeOffset? snapshotCapturedUtc,
        DateTimeOffset nowUtc) =>
        connection switch
        {
            InstanceConnectionStatus.Error => SessionState.Failed,
            InstanceConnectionStatus.LoggedOut => SessionState.ScanQr,
            InstanceConnectionStatus.Initializing => SessionState.Starting,
            InstanceConnectionStatus.Connected => ResolveConnected(snapshotCapturedUtc, nowUtc),
            _ => SessionState.Starting
        };

    // Connected is the only status that can mean two different things to the owner: producing data, or
    // sitting there looking fine while the numbers quietly rot. Freshness is what separates them.
    private static SessionState ResolveConnected(DateTimeOffset? snapshotCapturedUtc, DateTimeOffset nowUtc)
    {
        if (snapshotCapturedUtc is null)
        {
            // Connected but nothing read yet — still coming up, not broken.
            return SessionState.Starting;
        }

        return nowUtc - snapshotCapturedUtc.Value > StaleSnapshotThreshold
            ? SessionState.Degraded
            : SessionState.Working;
    }

    /// <summary>Short label for the account-card chip.</summary>
    public static string ToLabel(SessionState state) => state switch
    {
        SessionState.Starting => "Starting",
        SessionState.ScanQr => "Scan QR",
        SessionState.Working => "Live",
        SessionState.Degraded => "Stale",
        SessionState.Failed => "Failed",
        _ => "Unknown"
    };

    /// <summary>Plain-language tooltip — the owner should never have to guess what a chip means.</summary>
    public static string ToDescription(SessionState state) => state switch
    {
        SessionState.Starting => "This account is still loading. Numbers will appear once it finishes.",
        SessionState.ScanQr => "Signed out. Open this account and scan the QR code to reconnect it.",
        SessionState.Working => "Connected and up to date.",
        SessionState.Degraded => "Connected, but these numbers haven't refreshed recently. Try Re-sync.",
        SessionState.Failed => "This account hit an error and isn't reporting. Open it to see why.",
        _ => "State unknown."
    };
}
