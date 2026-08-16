using System.Collections.Concurrent;

namespace UnifiedMessenger.Services;

/// <summary>
/// What the retry scheduler believes about an account's network, for surfaces that have to word it.
/// Three states, not two, because "we are retrying" and "we stopped retrying" are different promises.
/// </summary>
public enum ReconnectState
{
    /// <summary>No connectivity failure recorded — say nothing about the network.</summary>
    None,

    /// <summary>Offline, with another reconnect attempt still scheduled.</summary>
    Retrying,

    /// <summary>Offline, backoff exhausted — it will only retry when the account is reopened.</summary>
    GaveUp
}

/// <summary>
/// Retries a page load that failed because the machine had no network, so an account does not stay dead
/// after a wifi blip.
///
/// <para>
/// <b>The gap this closes.</b> A failed navigation set the account to <c>Error</c> and returned. Nothing
/// retried it. The stale-adapter monitor looks like it would, but it cannot:
/// <c>AdapterHealthMonitor.EvaluateIsStale</c> returns <see langword="false"/> for the
/// <c>Unknown</c> and <c>NoAdapter</c> states, and an account whose page never loaded never reached
/// <c>Ready</c> — there is no adapter to go stale. So an owner who opened the app while the router was
/// rebooting had an account showing "Connection error" until they noticed and refreshed it by hand, no
/// matter how long the network had been back.
/// </para>
/// <para>
/// <b>Deliberately narrow.</b> Only connectivity-class failures are retried — a certificate error or a
/// proxy demanding credentials will not fix itself, and hammering them would be noise. Attempts are
/// capped and backed off, and a success resets the count, so a genuinely unreachable host costs five
/// reloads over about eight minutes and then stops rather than looping forever.
/// </para>
/// </summary>
public sealed class NavigationRetryScheduler
{
    /// <summary>
    /// Backoff schedule. Short enough that a brief drop recovers while the owner is still looking at the
    /// screen, long enough that a real outage is not a reload loop.
    /// </summary>
    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5)
    ];

    public static int MaxAttempts => Delays.Length;

    private static readonly Lazy<NavigationRetryScheduler> LazyInstance = new(() => new NavigationRetryScheduler());

    public static NavigationRetryScheduler Instance => LazyInstance.Value;

    // Keyed by instance id — a plain string — and never by CoreWebView2. Keying per-WebView state on the
    // projection is the documented trap in AGENTS.md: the managed wrapper can be collected and recreated
    // for the same native object, and the entry silently disappears.
    private readonly ConcurrentDictionary<string, int> _attempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pending =
        new(StringComparer.OrdinalIgnoreCase);

    // Accounts whose backoff schedule ran out. Kept separate from _attempts because the two answer
    // different questions: _attempts says "we know this account's problem is the network", _exhausted says
    // "and we have stopped trying". Collapsing them would make the rail promise a reconnect that will
    // never come — the same class of false statement this pass exists to remove.
    private readonly ConcurrentDictionary<string, byte> _exhausted = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reload hook. Overridable so the scheduling contract is testable without a WebView2.</summary>
    internal Func<string, CancellationToken, Task> ReloadAsync { get; set; } =
        (instanceId, token) => InstanceSessionManager.Instance.ReloadSessionAsync(instanceId, token);

    /// <summary>Whether a given WebView2 error status is worth retrying at all.</summary>
    public static bool ShouldRetry(string? webErrorStatus) =>
        NetworkFailureDescriber.DescribeWebViewStatus(webErrorStatus) == NetworkFailureDescriber.AccountOffline;

    /// <summary>
    /// The wait before attempt number <paramref name="attemptsSoFar"/> + 1, or <see langword="null"/> once
    /// the cap is reached. Pure, so the whole backoff contract is testable.
    /// </summary>
    public static TimeSpan? NextDelay(int attemptsSoFar) =>
        attemptsSoFar >= 0 && attemptsSoFar < Delays.Length ? Delays[attemptsSoFar] : null;

    /// <summary>Total wait before giving up — quoted in the findings doc, so derive it rather than repeat it.</summary>
    public static TimeSpan TotalBackoff() => Delays.Aggregate(TimeSpan.Zero, (sum, delay) => sum + delay);

    /// <summary>Called when a navigation fails. Schedules a reload when the failure looks transient.</summary>
    public void OnNavigationFailed(string instanceId, string? webErrorStatus)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var id = instanceId.Trim();

        // The status alone is not enough to keep a retry chain alive. Reloading an account cancels its
        // in-flight navigation, and the cancellation surfaces as `Unknown` — which is not a connectivity
        // status, so the chain stopped dead after the first attempt. Observed live behind a dead proxy:
        //
        //   19:03:07  'b87bf7cd…' could not load (ConnectionAborted); retrying in 10s (attempt 1 of 5).
        //   19:03:17  Reconnect attempt firing for 'b87bf7cd…'.
        //   19:03:19  'b87bf7cd…' navigation failed (Unknown).
        //   (nothing further — attempts 2 to 5 never happened)
        //
        // Five attempts over eight minutes was the whole point; what shipped was one attempt over ten
        // seconds. So once this account is already known to be offline, an unrecognised failure is treated
        // as a continuation of the same outage rather than a new, unrelated fault. The attempt cap still
        // bounds it, and a success still clears it — the cost is that a genuinely different failure
        // arriving mid-outage gets folded into the outage until the chain ends.
        if (!ShouldRetry(webErrorStatus) && !BelievesOffline(id))
        {
            return;
        }
        var attemptsSoFar = _attempts.GetOrAdd(id, 0);
        if (NextDelay(attemptsSoFar) is not { } delay)
        {
            _exhausted[id] = 1;
            AppLogger.LogWarning(
                "WebView.Nav",
                $"Giving up on reconnecting '{id}' after {MaxAttempts} attempts; it will retry when reopened.");
            return;
        }

        _attempts[id] = attemptsSoFar + 1;
        CancelPending(id);

        var cts = new CancellationTokenSource();
        _pending[id] = cts;

        AppLogger.LogInfo(
            "WebView.Nav",
            $"'{id}' could not load ({webErrorStatus}); retrying in {delay.TotalSeconds:0}s " +
            $"(attempt {attemptsSoFar + 1} of {MaxAttempts}).");

        _ = RunAfterDelayAsync(id, delay, cts.Token);
    }

    /// <summary>Called on a successful navigation. Clears the backoff so the next outage starts fresh.</summary>
    public void OnNavigationSucceeded(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var id = instanceId.Trim();
        _attempts.TryRemove(id, out _);
        _exhausted.TryRemove(id, out _);
        CancelPending(id);
    }

    /// <summary>Drops any scheduled retry for an account being removed or shut down.</summary>
    public void Forget(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        var id = instanceId.Trim();
        _attempts.TryRemove(id, out _);
        _exhausted.TryRemove(id, out _);
        CancelPending(id);
    }

    internal int AttemptsFor(string instanceId) =>
        _attempts.TryGetValue(instanceId.Trim(), out var count) ? count : 0;

    /// <summary>
    /// True while this account is being reconnected after a connectivity failure.
    ///
    /// <para>
    /// This is the <b>authoritative</b> "we believe the network is down for this account" signal, and it
    /// exists because the raw WebView2 error status is not stable enough to carry that meaning. Reloading
    /// an account cancels its in-flight navigation, and the cancellation reports a <i>different</i> status
    /// than the original failure — so an account that correctly read "No internet — reconnecting…" reverted
    /// to a generic "Connection error" the moment the first retry fired, and the scan went back to telling
    /// the owner to open a page that could not load. The retry was masking its own signal.
    /// </para>
    /// <para>
    /// A retry is only ever scheduled for a connectivity-class failure (see <see cref="ShouldRetry"/>), so
    /// a pending one is a precise statement of cause, not a guess — and it is cleared the moment a
    /// navigation succeeds.
    /// </para>
    /// </summary>
    public bool BelievesOffline(string? instanceId) =>
        !string.IsNullOrWhiteSpace(instanceId) &&
        (_attempts.ContainsKey(instanceId.Trim()) || _exhausted.ContainsKey(instanceId.Trim()));

    /// <summary>
    /// True while this account is offline <i>and</i> a further reconnect is still coming. Once the backoff
    /// schedule runs out this goes false while <see cref="BelievesOffline"/> stays true — the cause is
    /// still known, but the app must stop saying "reconnecting…" for something it has stopped doing.
    /// </summary>
    public bool IsReconnecting(string? instanceId) =>
        BelievesOffline(instanceId) && !_exhausted.ContainsKey(instanceId!.Trim());

    /// <summary>The single call a UI surface should make: what to say about this account's network.</summary>
    public ReconnectState StateFor(string? instanceId)
    {
        if (!BelievesOffline(instanceId))
        {
            return ReconnectState.None;
        }

        return _exhausted.ContainsKey(instanceId!.Trim()) ? ReconnectState.GaveUp : ReconnectState.Retrying;
    }

    private void CancelPending(string id)
    {
        if (_pending.TryRemove(id, out var existing))
        {
            existing.Cancel();
            existing.Dispose();
        }
    }

    private async Task RunAfterDelayAsync(string id, TimeSpan delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token).ConfigureAwait(false);

            // Logged before the call, not after. During the live offline test the scheduled retry left no
            // trace at all — the "retrying in 10s" line appeared and then nothing, with no way to tell
            // whether the reload had run and failed again, or had never been reached. A retry you cannot
            // observe is a retry you cannot claim works.
            AppLogger.LogInfo("WebView.Nav", $"Reconnect attempt firing for '{id}'.");
            await ReloadAsync(id, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer failure, or the account went away — most often an LRU eviction
            // disposing the session while the retry was pending. Worth a line: silently dropping the
            // reconnect is exactly the kind of invisible behaviour this whole pass keeps turning up.
            AppLogger.LogInfo("WebView.Nav", $"Pending reconnect for '{id}' was cancelled.");
        }
        catch (Exception ex)
        {
            // A reload that throws must not take down a background task unobserved — that is the same
            // failure mode the auto-updater had.
            AppLogger.LogWarning("WebView.Nav", $"Retry reload for '{id}' failed: {ex.Message}");
        }
    }
}
