using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-OFFLINE-06 — the scan told the owner to open an account when the real cause was no internet.
///
/// <para>
/// The advice "open the account once to finish loading" is correct for lazy loading, and was written for
/// it. The trap is that a dropped connection produces the <i>same stages</i>: the page cannot load, so
/// <c>indexedDB.open</c> never returns and the watchdog fires. Observed live during the offline test —
/// <c>stage 'databases-rejected' — this account's page is not loaded. Open the account once to finish
/// loading.</c> — which cannot work while the network is down.
/// </para>
/// <para>
/// The stage cannot tell the two apart. These tests pin that the connection status is what does.
/// </para>
/// </summary>
public class ScanBlockedMessageTests
{
    private const string Offline = "ConnectionAborted";

    // ---- Telling the two causes apart ---------------------------------------------------------------

    [Theory]
    [InlineData("watchdog-timeout")]
    [InlineData("no-model-storage")]
    [InlineData("no-indexeddb")]
    [InlineData("databases-rejected")]
    public void AnOfflineAccountIsNotToldToOpenTheAccount(string stage)
    {
        var message = ScanBlockedMessage.DescribeUnfinished(
            stage, pageNotReady: true, InstanceConnectionStatus.Error, Offline);

        Assert.DoesNotContain("Open the account", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no internet connection", message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("watchdog-timeout")]
    [InlineData("no-model-storage")]
    [InlineData("databases-rejected")]
    public void ASleepingAccountIsStillToldToOpenIt(string stage)
    {
        // The case the wording was written for, and which v4.99.19 was specifically about. It must
        // survive the fix — otherwise a lazily-unloaded account gets blamed on the network.
        var message = ScanBlockedMessage.DescribeUnfinished(
            stage, pageNotReady: true, InstanceConnectionStatus.Connected, connectionDetail: null);

        Assert.Contains("Open the account once", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no internet", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnOfflineAccountIsToldItWillRecoverByItself()
    {
        // There is nothing for the owner to do, and the retry added in v4.99.22 means that is literally
        // true — so say so, rather than leaving them staring at a warning with no action.
        var message = ScanBlockedMessage.DescribeUnfinished(
            "watchdog-timeout", pageNotReady: true, InstanceConnectionStatus.Error, Offline);

        Assert.Contains("on its own once the connection is back", message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- What must NOT be masked --------------------------------------------------------------------

    [Fact]
    public void AGenuineScraperFailureIsStillReportedEvenWhileOffline()
    {
        // The dangerous direction. If the scan settles at an unrecognised stage — the signature of a
        // WhatsApp Web schema change — an offline machine must not relabel it as a network problem and
        // hide a real regression.
        var message = ScanBlockedMessage.DescribeUnfinished(
            "some-new-stage", pageNotReady: false, InstanceConnectionStatus.Error, Offline);

        Assert.Contains("settled at stage 'some-new-stage'", message, StringComparison.Ordinal);
        Assert.DoesNotContain("no internet", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnUnknownStageStillReadsUnknownRatherThanBlank()
    {
        var message = ScanBlockedMessage.DescribeUnfinished(
            null, pageNotReady: false, InstanceConnectionStatus.Connected, null);

        Assert.Contains("'unknown'", message, StringComparison.Ordinal);
    }

    // ---- The offline test itself --------------------------------------------------------------------

    [Theory]
    [InlineData("ConnectionAborted")]
    [InlineData("HostNameNotResolved")]
    [InlineData("ServerUnreachable")]
    [InlineData("Disconnected")]
    public void EveryConnectivityErrorCountsAsOffline(string detail) =>
        Assert.True(ScanBlockedMessage.LooksOffline(InstanceConnectionStatus.Error, detail));

    [Theory]
    [InlineData("CertificateExpired")]
    [InlineData("ValidProxyAuthenticationRequired")]
    [InlineData("Session failed to start")]
    [InlineData(null)]
    public void ANonNetworkFailureIsNotCalledOffline(string? detail)
    {
        // A bad certificate or a proxy demanding credentials is not "no internet", and telling the owner
        // it is would send them to check a router that is working fine.
        Assert.False(ScanBlockedMessage.LooksOffline(InstanceConnectionStatus.Error, detail));
    }

    [Theory]
    [InlineData(InstanceConnectionStatus.Connected)]
    [InlineData(InstanceConnectionStatus.Initializing)]
    [InlineData(InstanceConnectionStatus.LoggedOut)]
    public void OnlyAnErroredAccountCanBeOffline(InstanceConnectionStatus status)
    {
        // A stale detail string left over on an account that has since reconnected must not resurrect the
        // offline wording.
        Assert.False(ScanBlockedMessage.LooksOffline(status, Offline));
    }

    // ---- The not-injected path shares the same rule --------------------------------------------------

    [Fact]
    public void TheNotInjectedMessageAlsoStopsBlamingTheOwnerWhenOffline()
    {
        var offline = ScanBlockedMessage.DescribeNotInjected(InstanceConnectionStatus.Error, Offline);
        var sleeping = ScanBlockedMessage.DescribeNotInjected(InstanceConnectionStatus.Connected, null);

        Assert.Contains("no internet connection", offline, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Open the account", offline, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("Open the account once", sleeping, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no internet", sleeping, StringComparison.OrdinalIgnoreCase);
    }

    // ---- The retry masking its own signal -------------------------------------------------------------
    //
    // This is the half that the first live re-test caught. The status-derived check above is correct but
    // not durable: reloading an account cancels its in-flight navigation, and the cancellation reports a
    // status the describer does not recognise, so the account flipped straight back to "not loaded" the
    // moment the first reconnect fired. The scheduler's own belief is the stable signal, because a retry
    // is only ever scheduled for a connectivity failure in the first place.

    [Theory]
    [InlineData(ReconnectState.Retrying)]
    [InlineData(ReconnectState.GaveUp)]
    public void APendingReconnectOutranksAnUnrecognisedStatus(ReconnectState reconnect)
    {
        // "" stands in for the status the cancelled navigation reports: not in the connectivity set, so
        // the status-derived check alone returns false.
        Assert.False(ScanBlockedMessage.LooksOffline(InstanceConnectionStatus.Error, string.Empty));

        var message = ScanBlockedMessage.DescribeUnfinished(
            "databases-rejected", pageNotReady: true, InstanceConnectionStatus.Error, string.Empty, reconnect);

        Assert.Contains("no internet connection", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Open the account once to finish loading", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OnlyAPendingRetryMayPromiseTheAppWillFixItself()
    {
        var retrying = ScanBlockedMessage.DescribeUnfinished(
            "watchdog-timeout", pageNotReady: true, InstanceConnectionStatus.Error, Offline, ReconnectState.Retrying);
        var gaveUp = ScanBlockedMessage.DescribeUnfinished(
            "watchdog-timeout", pageNotReady: true, InstanceConnectionStatus.Error, Offline, ReconnectState.GaveUp);

        Assert.Contains("pick up on its own", retrying, StringComparison.OrdinalIgnoreCase);

        // Once the backoff is exhausted nothing is coming, so the app must not say it is.
        Assert.DoesNotContain("pick up on its own", gaveUp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reopen the account", gaveUp, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ARealScraperFailureIsStillReportedWhileOffline()
    {
        // An offline machine must not become a blanket excuse that hides a genuine scraper break.
        var message = ScanBlockedMessage.DescribeUnfinished(
            "collection-missing", pageNotReady: false, InstanceConnectionStatus.Error, Offline, ReconnectState.Retrying);

        Assert.Contains("collection-missing", message, StringComparison.Ordinal);
        Assert.DoesNotContain("no internet", message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- The third case: it failed, and the app does not know why -------------------------------------

    [Fact]
    public void APageThatFailedForAnUnknownReasonIsNotDescribedAsOneThatWasNeverOpened()
    {
        // Observed live: an account whose only navigation failure was `Unknown` — an aborted navigation,
        // not a diagnosable error — got the never-opened sentence. Both halves of it were wrong: the page
        // had been opened, and opening it again is not the fix being implied.
        var message = ScanBlockedMessage.DescribeUnfinished(
            "databases-rejected", pageNotReady: true, InstanceConnectionStatus.Error, "Unknown");

        Assert.Contains("failed to load", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("once to finish loading", message, StringComparison.OrdinalIgnoreCase);

        // And it must not claim a network cause it has no evidence for — that would be the original
        // defect with the blame moved rather than removed.
        Assert.DoesNotContain("no internet", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ANeverOpenedAccountStillGetsTheAdviceThatActuallyWorksForIt()
    {
        // The lazy-loading case is the common one and the wording was correct for it. Every branch added
        // since has to leave it alone: an account that is simply asleep is not in Error at all.
        foreach (var status in new[] { InstanceConnectionStatus.Initializing, InstanceConnectionStatus.Connected })
        {
            var message = ScanBlockedMessage.DescribeUnfinished(
                "watchdog-timeout", pageNotReady: true, status, null);

            Assert.Contains("Open the account once to finish loading", message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheNotInjectedPathHasTheSameThreeAnswers()
    {
        var offline = ScanBlockedMessage.DescribeNotInjected(InstanceConnectionStatus.Error, "ConnectionAborted");
        var failed = ScanBlockedMessage.DescribeNotInjected(InstanceConnectionStatus.Error, "Unknown");
        var asleep = ScanBlockedMessage.DescribeNotInjected(InstanceConnectionStatus.Initializing, null);

        Assert.Contains("no internet connection", offline, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("failed to load", failed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not loaded yet", asleep, StringComparison.OrdinalIgnoreCase);

        // Three distinct causes must not collapse back to one sentence — that collapse is the finding.
        Assert.Equal(3, new HashSet<string>([offline, failed, asleep]).Count);
    }
}
