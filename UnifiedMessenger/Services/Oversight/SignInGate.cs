using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// The single answer to two questions that were previously answered in different places, and
/// inconsistently: <i>may this account be scraped?</i> and <i>may its figures be shown?</i>
///
/// <para>
/// <b>The defect this closes (A12).</b> Sign-in state was tracked — <see cref="InstanceConnectionStatus"/>
/// has had a <c>LoggedOut</c> member since the beginning — but almost nothing consulted it.
/// <see cref="OversightAlertMonitor"/> skipped accounts that were not <c>Connected</c>;
/// <see cref="OversightSnapshotReader"/>, which is the actual scan and the path manual Re-sync takes,
/// did not check at all. So a signed-out account was scraped, found nothing, and reported as caught up.
/// </para>
/// <para>
/// That is the same false calm <see cref="AccountReadHealth"/> exists to prevent, arriving through a
/// door it does not watch: a read that <i>succeeds</i> and returns zero is indistinguishable, to every
/// downstream consumer, from a genuinely quiet account. The read never failed, so nothing warned.
/// </para>
/// <para>
/// <b>Why the two questions share one type.</b> They must never diverge. If scanning is gated but
/// display is not, the screen shows the last numbers read before the session expired, ageing silently
/// with nothing to say they are frozen — which is worse than showing nothing, because a stale figure
/// still reads as a measurement. Keeping both answers on one gate makes divergence a visible edit
/// rather than an oversight.
/// </para>
/// </summary>
public static class SignInGate
{
    /// <summary>
    /// True when this account's client is showing a sign-in screen.
    ///
    /// <para>
    /// Deliberately narrow: only <see cref="InstanceConnectionStatus.LoggedOut"/> counts.
    /// <c>Initializing</c> is a page still booting and <c>Error</c> is usually the network, and neither
    /// is evidence about credentials — treating them as "signed out" would tell the owner to go and log
    /// in when nothing is wrong with their session.
    /// </para>
    /// </summary>
    public static bool IsSignedOut(string? instanceId) =>
        !string.IsNullOrWhiteSpace(instanceId) &&
        InstanceConnectionStatusService.Instance.GetStatus(instanceId!) == InstanceConnectionStatus.LoggedOut;

    /// <summary>
    /// True when a scraper may run against this account.
    ///
    /// <para>
    /// Only a confirmed sign-in screen blocks a scan. An account that is merely still initializing is
    /// allowed through, because the scan has its own readiness handling and blocking it here would
    /// simply move a "page isn't ready" outcome to a place with less context to explain it.
    /// </para>
    /// </summary>
    public static bool MayScan(string? instanceId) => !IsSignedOut(instanceId);

    /// <summary>
    /// True when this account's measured figures may be put on screen.
    ///
    /// <para>
    /// Same condition as <see cref="MayScan"/> by construction, not by coincidence — see the type
    /// remarks. Callers should render the account with no figures at all rather than zeroes: zero is a
    /// measurement, and an account nothing has read has not been measured.
    /// </para>
    /// </summary>
    public static bool MayShowFigures(string? instanceId) => MayScan(instanceId);

    /// <summary>
    /// How many of the given accounts are showing a sign-in screen.
    /// </summary>
    public static int CountSignedOut(IEnumerable<MessengerInstance>? instances) =>
        instances is null ? 0 : instances.Count(instance => IsSignedOut(instance?.Id));

    /// <summary>
    /// The one sentence a surface should print when some accounts are signed out, or null when none are.
    ///
    /// <para>
    /// Says what is missing and what to do, and never implies the owner did something wrong — a session
    /// expiring is the platform's decision, not theirs.
    /// </para>
    /// </summary>
    public static string? DescribeSignedOut(IEnumerable<MessengerInstance>? instances)
    {
        var signedOut = (instances ?? []).Where(instance => IsSignedOut(instance?.Id)).ToList();
        if (signedOut.Count == 0)
        {
            return null;
        }

        var names = signedOut
            .Select(instance => PlatformDefinition.FindById(instance.Platform)?.DisplayName ?? "Account")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var channels = names.Count switch
        {
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => string.Join(", ", names.Take(names.Count - 1)) + $" and {names[^1]}"
        };

        return signedOut.Count == 1
            ? $"1 account is signed out — {channels} contributes nothing to the figures here until you sign in."
            : $"{signedOut.Count} accounts are signed out — {channels} contribute nothing to the figures here until you sign in.";
    }
}
