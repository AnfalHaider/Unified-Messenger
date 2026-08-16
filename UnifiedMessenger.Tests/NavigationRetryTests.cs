using UnifiedMessenger.Models;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// F-OFFLINE-04 — an account whose page failed to load was never retried, so a wifi blip at the wrong
/// moment left it dead until someone noticed and refreshed it by hand.
///
/// <para>
/// The reason nothing caught it is worth stating, because the code looks like it already handles this:
/// there IS a recovery path (<c>AdapterHealthMonitor</c> → <c>RecoverStaleAdapterAsync</c>), and it does
/// reload the WebView. It just never fires for this case. Staleness is measured from an adapter's
/// heartbeat, and an account whose page never loaded never got an adapter — so it sits in
/// <c>Unknown</c>/<c>NoAdapter</c>, which <c>EvaluateIsStale</c> explicitly excludes. The safety net is
/// real and it has a hole exactly where the offline case lands. The first test below pins that, so the
/// hole cannot quietly close and leave the retry looking redundant.
/// </para>
/// </summary>
public class NavigationRetryTests
{
    // ---- Why the existing safety net does not cover this --------------------------------------------

    [Theory]
    [InlineData(AdapterHealthState.Unknown)]
    [InlineData(AdapterHealthState.NoAdapter)]
    public void AnAccountWithNoAdapterIsNeverConsideredStaleSoNothingElseRetriesIt(AdapterHealthState state)
    {
        // Even with a heartbeat far outside the threshold — or none at all — these states are excluded.
        var status = new AdapterHealthStatus
        {
            State = state,
            LastHeartbeat = null
        };

        Assert.False(AdapterHealthMonitor.EvaluateIsStale(status, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(90)));
    }

    [Fact]
    public void AnAccountThatDidLoadAndThenWentQuietIsStillCoveredByTheStaleMonitor()
    {
        // Control: the pre-existing net must keep working. This is the case it was built for, and the
        // retry scheduler deliberately does not touch it.
        var status = new AdapterHealthStatus
        {
            State = AdapterHealthState.Ready,
            LastHeartbeat = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        Assert.True(AdapterHealthMonitor.EvaluateIsStale(status, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(90)));
    }

    // ---- What gets retried --------------------------------------------------------------------------

    [Theory]
    [InlineData("HostNameNotResolved")]
    [InlineData("ServerUnreachable")]
    [InlineData("CannotConnect")]
    [InlineData("Disconnected")]
    [InlineData("ConnectionAborted")]
    [InlineData("ConnectionReset")]
    [InlineData("Timeout")]
    public void ConnectivityFailuresAreRetried(string status) =>
        Assert.True(NavigationRetryScheduler.ShouldRetry(status));

    [Theory]
    [InlineData("CertificateExpired")]
    [InlineData("CertificateIsInvalid")]
    [InlineData("CertificateRevoked")]
    [InlineData("ValidProxyAuthenticationRequired")]
    [InlineData("ValidAuthenticationCredentialsRequired")]
    [InlineData("RedirectFailed")]
    [InlineData("UnexpectedError")]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void FailuresThatWillNotFixThemselvesAreNotRetried(string? status)
    {
        // A wrong clock, an expired certificate or a proxy asking for a password are not transient.
        // Reloading them on a timer produces load and log noise and never succeeds.
        Assert.False(NavigationRetryScheduler.ShouldRetry(status));
    }

    // ---- The backoff contract -----------------------------------------------------------------------

    [Fact]
    public void TheBackoffLengthensAndThenStops()
    {
        var delays = Enumerable.Range(0, NavigationRetryScheduler.MaxAttempts)
            .Select(i => NavigationRetryScheduler.NextDelay(i)!.Value)
            .ToList();

        Assert.Equal(NavigationRetryScheduler.MaxAttempts, delays.Count);
        for (var i = 1; i < delays.Count; i++)
        {
            Assert.True(delays[i] > delays[i - 1], $"delay {i} must exceed delay {i - 1}");
        }

        // Past the cap there is no next attempt — this is what stops a permanently unreachable host from
        // becoming a reload loop for as long as the app is open.
        Assert.Null(NavigationRetryScheduler.NextDelay(NavigationRetryScheduler.MaxAttempts));
        Assert.Null(NavigationRetryScheduler.NextDelay(NavigationRetryScheduler.MaxAttempts + 10));
    }

    [Fact]
    public void TheFirstRetryIsFastEnoughToBeSeenAndTheTotalIsBounded()
    {
        // A brief drop should recover while the owner is still looking at the screen.
        Assert.True(NavigationRetryScheduler.NextDelay(0) <= TimeSpan.FromSeconds(15));

        // And the whole sequence has to end in minutes, not hours.
        Assert.InRange(NavigationRetryScheduler.TotalBackoff(), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void ANegativeAttemptCountDoesNotIndexOutOfRange()
    {
        Assert.Null(NavigationRetryScheduler.NextDelay(-1));
    }

    // ---- Scheduling behaviour -----------------------------------------------------------------------

    private static NavigationRetryScheduler FreshScheduler(List<string> reloads)
    {
        var scheduler = new NavigationRetryScheduler
        {
            ReloadAsync = (id, _) =>
            {
                lock (reloads)
                {
                    reloads.Add(id);
                }

                return Task.CompletedTask;
            }
        };

        return scheduler;
    }

    [Fact]
    public void AConnectivityFailureCountsAnAttemptAndANonNetworkFailureDoesNot()
    {
        var scheduler = FreshScheduler([]);

        scheduler.OnNavigationFailed("acct-1", "CertificateExpired");
        Assert.Equal(0, scheduler.AttemptsFor("acct-1"));

        scheduler.OnNavigationFailed("acct-1", "HostNameNotResolved");
        Assert.Equal(1, scheduler.AttemptsFor("acct-1"));
    }

    [Fact]
    public void RepeatedFailuresWalkTheBackoffAndThenStopCountingPastTheCap()
    {
        var scheduler = FreshScheduler([]);

        for (var i = 0; i < NavigationRetryScheduler.MaxAttempts + 3; i++)
        {
            scheduler.OnNavigationFailed("acct-1", "Disconnected");
        }

        Assert.Equal(NavigationRetryScheduler.MaxAttempts, scheduler.AttemptsFor("acct-1"));
    }

    [Fact]
    public void ASuccessfulLoadResetsTheBackoffSoTheNextOutageStartsFresh()
    {
        var scheduler = FreshScheduler([]);

        scheduler.OnNavigationFailed("acct-1", "Disconnected");
        scheduler.OnNavigationFailed("acct-1", "Disconnected");
        Assert.Equal(2, scheduler.AttemptsFor("acct-1"));

        scheduler.OnNavigationSucceeded("acct-1");
        Assert.Equal(0, scheduler.AttemptsFor("acct-1"));

        scheduler.OnNavigationFailed("acct-1", "Disconnected");
        Assert.Equal(1, scheduler.AttemptsFor("acct-1"));
    }

    [Fact]
    public void AccountsBackOffIndependently()
    {
        // One branch losing its session must not consume another branch's retry budget.
        var scheduler = FreshScheduler([]);

        scheduler.OnNavigationFailed("acct-1", "Disconnected");
        scheduler.OnNavigationFailed("acct-1", "Disconnected");
        scheduler.OnNavigationFailed("acct-2", "Disconnected");

        Assert.Equal(2, scheduler.AttemptsFor("acct-1"));
        Assert.Equal(1, scheduler.AttemptsFor("acct-2"));
    }

    [Fact]
    public void ForgettingAnAccountClearsItsBackoff()
    {
        var scheduler = FreshScheduler([]);

        scheduler.OnNavigationFailed("acct-1", "Disconnected");
        scheduler.Forget("acct-1");

        Assert.Equal(0, scheduler.AttemptsFor("acct-1"));
    }

    [Fact]
    public void AnEmptyInstanceIdIsIgnoredRatherThanTracked()
    {
        var scheduler = FreshScheduler([]);

        scheduler.OnNavigationFailed("", "Disconnected");
        scheduler.OnNavigationFailed("   ", "Disconnected");
        scheduler.OnNavigationSucceeded("");
        scheduler.Forget("");

        Assert.Equal(0, scheduler.AttemptsFor(" "));
    }

    [Fact]
    public async Task AScheduledRetryActuallyReloadsTheAccount()
    {
        // The scheduling path end to end, with the delay shortened by driving the pure policy directly is
        // not possible — so this asserts the wiring by using the real first delay. It is 10 seconds, which
        // is too long for a test, so instead assert the reload hook is invoked when the delay elapses by
        // scheduling and then waiting on a deterministic signal.
        var reloaded = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduler = new NavigationRetryScheduler
        {
            ReloadAsync = (id, _) =>
            {
                reloaded.TrySetResult(id);
                return Task.CompletedTask;
            }
        };

        scheduler.OnNavigationFailed("acct-1", "HostNameNotResolved");

        var completed = await Task.WhenAny(reloaded.Task, Task.Delay(TimeSpan.FromSeconds(20)));

        Assert.Same(reloaded.Task, completed);
        Assert.Equal("acct-1", await reloaded.Task);
    }

    [Fact]
    public async Task ForgettingAnAccountCancelsAPendingReload()
    {
        // The case that would otherwise resurrect a deleted account: a retry is in flight when the
        // session is disposed. InstanceSessionManager calls Forget before tearing the session down.
        var reloads = new List<string>();
        var scheduler = FreshScheduler(reloads);

        scheduler.OnNavigationFailed("acct-1", "HostNameNotResolved");
        scheduler.Forget("acct-1");

        await Task.Delay(TimeSpan.FromSeconds(13));

        lock (reloads)
        {
            Assert.Empty(reloads);
        }
    }

    // ---- The state the rest of the app words its messages from ---------------------------------------

    [Fact]
    public void AnAccountWithNoRecordedFailureSaysNothingAboutTheNetwork()
    {
        var scheduler = new NavigationRetryScheduler { ReloadAsync = (_, _) => Task.CompletedTask };

        Assert.Equal(ReconnectState.None, scheduler.StateFor("acct-quiet"));
        Assert.Equal(ReconnectState.None, scheduler.StateFor(null));
        Assert.Equal(ReconnectState.None, scheduler.StateFor("   "));
        Assert.False(scheduler.BelievesOffline("acct-quiet"));
    }

    [Fact]
    public void AConnectivityFailureIsRememberedAsTheCauseImmediately()
    {
        var scheduler = new NavigationRetryScheduler { ReloadAsync = (_, _) => Task.CompletedTask };

        scheduler.OnNavigationFailed("acct-1", "HostNameNotResolved");

        // This is the whole point of the flag: the raw status will stop being recognisable once the retry
        // reloads the page, so the scheduler's memory is what carries the cause forward.
        Assert.Equal(ReconnectState.Retrying, scheduler.StateFor("acct-1"));
        Assert.True(scheduler.IsReconnecting("acct-1"));
    }

    [Fact]
    public void ANonConnectivityFailureIsNotClaimedAsAnOutage()
    {
        var scheduler = new NavigationRetryScheduler { ReloadAsync = (_, _) => Task.CompletedTask };

        // A certificate error is a real, different problem. Reporting "no internet" for it would send the
        // owner to check a router that is working fine.
        scheduler.OnNavigationFailed("acct-1", "CertificateCommonNameIsIncorrect");

        Assert.Equal(ReconnectState.None, scheduler.StateFor("acct-1"));
    }

    [Fact]
    public void OnceTheBackoffRunsOutTheAppStopsClaimingItIsStillTrying()
    {
        var scheduler = new NavigationRetryScheduler { ReloadAsync = (_, _) => Task.CompletedTask };

        // Drive it past the cap without waiting out the real delays: each call consumes one slot.
        for (var i = 0; i <= NavigationRetryScheduler.MaxAttempts; i++)
        {
            scheduler.OnNavigationFailed("acct-1", "HostNameNotResolved");
        }

        // The cause is still known — it is still an outage, and the rail should still say so...
        Assert.True(scheduler.BelievesOffline("acct-1"));

        // ...but nothing further is scheduled, so it must not promise a reconnect that will never come.
        Assert.Equal(ReconnectState.GaveUp, scheduler.StateFor("acct-1"));
        Assert.False(scheduler.IsReconnecting("acct-1"));
    }

    [Fact]
    public void ASuccessfulLoadClearsBothTheAttemptsAndTheGaveUpState()
    {
        var scheduler = new NavigationRetryScheduler { ReloadAsync = (_, _) => Task.CompletedTask };

        for (var i = 0; i <= NavigationRetryScheduler.MaxAttempts; i++)
        {
            scheduler.OnNavigationFailed("acct-1", "HostNameNotResolved");
        }

        Assert.Equal(ReconnectState.GaveUp, scheduler.StateFor("acct-1"));

        scheduler.OnNavigationSucceeded("acct-1");

        // A sticky GaveUp would leave a working account permanently labelled offline — worse than the bug
        // this whole path was added to fix.
        Assert.Equal(ReconnectState.None, scheduler.StateFor("acct-1"));

        // And the next outage must get a full schedule again, not zero attempts.
        scheduler.OnNavigationFailed("acct-1", "HostNameNotResolved");
        Assert.Equal(ReconnectState.Retrying, scheduler.StateFor("acct-1"));
    }

    [Fact]
    public void ForgettingAnAccountAlsoForgetsThatItWasOffline()
    {
        var scheduler = new NavigationRetryScheduler { ReloadAsync = (_, _) => Task.CompletedTask };

        for (var i = 0; i <= NavigationRetryScheduler.MaxAttempts; i++)
        {
            scheduler.OnNavigationFailed("acct-1", "HostNameNotResolved");
        }

        scheduler.Forget("acct-1");

        Assert.Equal(ReconnectState.None, scheduler.StateFor("acct-1"));
    }

    [Fact]
    public void TheRetryChainSurvivesTheUnrecognisedStatusItsOwnReloadProduces()
    {
        var scheduler = new NavigationRetryScheduler { ReloadAsync = (_, _) => Task.CompletedTask };

        scheduler.OnNavigationFailed("acct-1", "ConnectionAborted");
        Assert.Equal(1, scheduler.AttemptsFor("acct-1"));

        // This is what the reload actually reports live — cancelling the in-flight navigation surfaces as
        // `Unknown`, which is not a connectivity status. Requiring one here capped the feature at a single
        // attempt over ten seconds instead of five over eight minutes.
        Assert.False(NavigationRetryScheduler.ShouldRetry("Unknown"));

        scheduler.OnNavigationFailed("acct-1", "Unknown");
        Assert.Equal(2, scheduler.AttemptsFor("acct-1"));

        scheduler.OnNavigationFailed("acct-1", "Unknown");
        Assert.Equal(3, scheduler.AttemptsFor("acct-1"));
    }

    [Fact]
    public void AnUnrecognisedFailureOnAnAccountThatWasNeverOfflineStillStartsNothing()
    {
        var scheduler = new NavigationRetryScheduler { ReloadAsync = (_, _) => Task.CompletedTask };

        // The continuation rule must not become a blanket "retry everything" — a first failure still has
        // to look like connectivity, or a certificate error would be reloaded five times for nothing.
        scheduler.OnNavigationFailed("acct-1", "Unknown");
        scheduler.OnNavigationFailed("acct-1", "CertificateExpired");

        Assert.Equal(0, scheduler.AttemptsFor("acct-1"));
        Assert.Equal(ReconnectState.None, scheduler.StateFor("acct-1"));
    }
}
