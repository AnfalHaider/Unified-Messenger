namespace UnifiedMessenger.Services;

/// <summary>
/// Whether an account — or the machine as a whole — currently has no way to reach the network.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this closes (F-OFFLINE-08).</b> The dashboard's advice for an account whose numbers had
/// gone cold was "out of date — click Re-sync", on the card, in its tooltip, and in its accessible name.
/// Re-sync reloads the account's page, which cannot succeed while the machine is offline, so the one
/// instruction the owner is given is the one thing that cannot work — and, worse, it reads as though the
/// staleness were something they had neglected to do.
/// </para>
/// <para>
/// The connection state that tells the two apart was already tracked and already joined correctly in
/// <see cref="ScanBlockedMessage"/>, which was written for the same mistake made in the log
/// (F-OFFLINE-06). It simply was not consulted anywhere the owner could see. This is that join, lifted so
/// both the log and the screen answer the question the same way — two surfaces disagreeing about whether
/// the machine is online would be its own defect.
/// </para>
/// <para>
/// Deliberately does <b>not</b> ask Windows whether a network is present. An account can be unreachable
/// while the machine has a perfectly good connection, and a machine can be "connected" to a network that
/// routes nowhere. What matters is what this app's own navigations actually did.
/// </para>
/// </remarks>
public static class OfflineState
{
    /// <summary>True when this account's last navigation failed for connectivity reasons.</summary>
    public static bool IsOffline(string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        var id = instanceId.Trim();
        return ScanBlockedMessage.LooksOffline(
            InstanceConnectionStatusService.Instance.GetStatus(id),
            InstanceConnectionStatusService.Instance.GetDetail(id),
            NavigationRetryScheduler.Instance.StateFor(id));
    }

    /// <summary>
    /// True when any of these accounts is offline.
    /// </summary>
    /// <remarks>
    /// Any, not all. A location card covers several accounts, and one of them being unreachable is already
    /// enough to make "click Re-sync" the wrong thing to say about that card — the same reasoning the
    /// freshness stamp uses when it shows its least-fresh member rather than its best one.
    /// </remarks>
    public static bool AnyOffline(IEnumerable<string>? instanceIds) =>
        instanceIds is not null && instanceIds.Any(IsOffline);
}
