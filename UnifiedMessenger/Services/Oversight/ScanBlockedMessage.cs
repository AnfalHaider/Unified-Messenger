using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Explains why a conversation scan produced nothing — and, crucially, tells the owner something they can
/// actually act on.
///
/// <para>
/// <b>The defect this closes (F-OFFLINE-06).</b> The reader classified a blocked scan on the JS
/// <c>stage</c> alone. Every "page isn't ready" stage produced the same advice: <i>"this account's page is
/// not loaded. Open the account once to finish loading."</i> That was written for lazy loading, where it
/// is exactly right — a background account simply has not navigated yet, and opening it once fixes it.
/// </para>
/// <para>
/// But the same stages occur when the machine has <b>no internet</b>. The page cannot load, so
/// <c>indexedDB.open</c> never returns and the watchdog fires — identical symptom, completely different
/// cause. Observed live during the offline test:
/// <c>Conversation scan could not run yet (stage 'databases-rejected') — this account's page is not
/// loaded. Open the account once to finish loading.</c> Opening the account cannot work while the network
/// is down, so the app was sending its owner to do something futile and implying the fault was theirs.
/// </para>
/// <para>
/// The stage genuinely cannot distinguish the two. The connection status can, and it is already tracked —
/// it just was not consulted here.
/// </para>
/// </summary>
public static class ScanBlockedMessage
{
    /// <summary>
    /// True when this account's own connection state says the machine cannot reach the network, so any
    /// advice that depends on a page loading is futile.
    /// </summary>
    public static bool LooksOffline(InstanceConnectionStatus status, string? connectionDetail) =>
        status == InstanceConnectionStatus.Error &&
        NetworkFailureDescriber.DescribeWebViewStatus(connectionDetail) == NetworkFailureDescriber.AccountOffline;

    /// <summary>
    /// As <see cref="LooksOffline"/>, but also true while a reconnect is pending for this account.
    ///
    /// <para>
    /// The error status alone is not stable enough: reloading cancels the in-flight navigation, and the
    /// cancellation reports a status the describer does not recognise — so an account correctly reported
    /// as offline reverted to a generic failure the moment the first retry fired. A pending reconnect is
    /// only ever scheduled for a connectivity failure, so it is the reliable half of the signal.
    /// </para>
    /// </summary>
    public static bool LooksOffline(
        InstanceConnectionStatus status,
        string? connectionDetail,
        ReconnectState reconnect) =>
        reconnect != ReconnectState.None || LooksOffline(status, connectionDetail);

    /// <summary>
    /// True when the page load is known to have <i>failed</i> — as opposed to never having been attempted,
    /// which is the ordinary lazy-loading case — but the app cannot say why.
    ///
    /// <para>
    /// Observed live: an account whose only navigation failure was <c>Unknown</c> (an aborted navigation,
    /// not a diagnosable error) sat in <c>Error</c> and was told <i>"this account's page is not loaded.
    /// Open the account once to finish loading"</i> — the same sentence a never-opened account gets. It is
    /// the wrong sentence: the page did not fail to be opened, it failed to load. Asserting a cause the
    /// app does not have is how the original defect happened; this branch exists so the third case says
    /// what is actually known instead of borrowing one of the other two answers.
    /// </para>
    /// </summary>
    private static bool FailedForAnUnknownReason(
        InstanceConnectionStatus status,
        string? connectionDetail,
        ReconnectState reconnect) =>
        status == InstanceConnectionStatus.Error && !LooksOffline(status, connectionDetail, reconnect);

    /// <summary>
    /// What the owner should expect next. Only <see cref="ReconnectState.Retrying"/> may promise the app
    /// will fix itself; once the backoff is exhausted the account really does need reopening.
    /// </summary>
    private static string Outlook(ReconnectState reconnect) =>
        reconnect == ReconnectState.GaveUp
            ? "Reopen the account once you are back online."
            : "It will pick up on its own once the connection is back.";

    /// <summary>
    /// The scan function was never injected — the adapter script never ran on this page.
    /// </summary>
    public static string DescribeNotInjected(
        InstanceConnectionStatus status,
        string? connectionDetail,
        ReconnectState reconnect = ReconnectState.None)
    {
        if (LooksOffline(status, connectionDetail, reconnect))
        {
            return "Conversation scan could not run — there is no internet connection, so this account's "
                + $"page never loaded. {Outlook(reconnect)}";
        }

        if (FailedForAnUnknownReason(status, connectionDetail, reconnect))
        {
            return "Conversation scan function is not injected on this page — the account's page failed "
                + "to load. Open the account to see what it reports.";
        }

        return "Conversation scan function is not injected on this page — the account's page has probably "
            + "not loaded yet. Open the account once to finish loading.";
    }

    /// <summary>
    /// The scan ran but settled somewhere other than <c>done</c>.
    /// </summary>
    public static string DescribeUnfinished(
        string? stage,
        bool pageNotReady,
        InstanceConnectionStatus status,
        string? connectionDetail,
        ReconnectState reconnect = ReconnectState.None)
    {
        if (!pageNotReady)
        {
            // A stage the page-not-ready set does not cover is a genuine anomaly, and stays reported as
            // one whatever the network is doing — an offline machine must not mask a real scraper failure.
            return $"Conversation scan settled at stage '{stage ?? "unknown"}' instead of 'done'; no oversight data was read.";
        }

        if (LooksOffline(status, connectionDetail, reconnect))
        {
            return $"Conversation scan could not run (stage '{stage}') — there is no internet connection, "
                + $"so this account's page never loaded. {Outlook(reconnect)}";
        }

        if (FailedForAnUnknownReason(status, connectionDetail, reconnect))
        {
            return $"Conversation scan could not run (stage '{stage}') — this account's page failed to "
                + "load. Open the account to see what it reports.";
        }

        return $"Conversation scan could not run yet (stage '{stage}') — this account's page is not "
            + "loaded. Open the account once to finish loading.";
    }
}
