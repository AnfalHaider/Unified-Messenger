# Findings — Offline behaviour

Session 2, `v4.99.22`. The handoff ranked this second and called it "the most conspicuous gap in the
state matrix — for an app whose entire input is web clients", with four questions:

| Question | Answer |
|---|---|
| Does the app stay responsive? | **Yes.** Confirmed live — `Responding=True`, 225 MB, UI enumerable throughout, with every web client failing to load. |
| Do accounts report "can't read" (correct) or something alarming? | **Neither, and that is the problem.** They said `HostNameNotResolved` on one surface and a bare "Connection error" on another. |
| Does the GitHub auto-updater fail quietly? | **Far too quietly — and it fails every single time.** See F-OFFLINE-01. |
| Does anything hang on a network timeout? | **One place could.** The installer download had no read timeout. Fixed. |

## How this was tested without a network and without elevation

The session had no administrator rights, so a firewall rule and disabling the adapter were both
unavailable — and both would have taken the whole machine offline, including the owner's other work.

Instead the app's web clients alone were cut off:

```powershell
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--proxy-server=127.0.0.1:9"
```

Port 9 is discard; nothing listens. Every WebView2 navigation fails at connect while the machine's own
networking is untouched. `AGENTS.md` already documents this environment variable for remote debugging, so
it is a supported lever, it needs no elevation, it affects nothing but this app, and it reverts by simply
launching without it. **This is the technique to reuse** — it is strictly better than a firewall rule,
which would not even have worked: WebView2 runs in separate `msedgewebview2.exe` processes, so a rule
scoped to `UnifiedMessenger.exe` would have blocked the updater and left the web clients online.

**Verified live:** the real `CoreWebView2WebErrorStatus` in this state is **`ConnectionAborted`**.

## F-OFFLINE-01 — Auto-update has never been able to succeed, and fails invisibly

- **Severity:** S1
- **Confidence:** confirmed by execution
- **Where:** `Services/Distribution/InstallerIntegrityVerifier.cs`
- **Status:** **FIXED** in `v4.99.22`. Guards: `OfflineBehaviourTests` (21, green).

`TryVerifyDownloadedInstaller` treated the SHA-256 digest as optional and Authenticode as mandatory. But
**nothing in this repository signs anything** — no `SignTool` directive in `installer.iss` or
`installer-arm64.iss`, no signing step in `.github/workflows/`, and `Get-AuthenticodeSignature` on the
built installer reports `Status: NotSigned`. So `WinVerifyTrust` returned `TRUST_E_NOSIGNATURE` for every
update ever offered.

Proven rather than reasoned — supplying a *correct* digest to the old code still produced:

```
Downloaded installer is not Authenticode-signed.
```

What that meant in practice, with the shipped defaults (`EnableAutoUpdate = true`,
`PromptBeforeAutoUpdate = false`):

1. Every launch, the app checks GitHub.
2. If the user is behind, it downloads the **entire installer**.
3. Verification rejects it. The file is deleted.
4. `ApplyUpdateAsync` throws inside `_ = services.GitHubUpdate.CheckForUpdatesAsync()` — a discarded
   task. Nothing is logged, nothing is shown.
5. Repeat, forever, on every launch.

So the product's whole update mechanism was dead, silently, while re-downloading a multi-megabyte
installer at every start. "Does the updater fail quietly?" — yes, completely, and always.

**Fix and its tradeoff, stated plainly.** An installer is now admitted on **either** a verified SHA-256
digest **or** a trusted Authenticode signature, and never on neither. The digest is published as a sidecar
asset and fetched over HTTPS from the same GitHub origin, so it is a real check against a truncated or
corrupted download — exactly what a dropped connection produces. It is **not** equivalent to Authenticode:
it does not prove who built the file, so it does not defend against a compromised GitHub release.
Authenticode remains the stronger control; `ExpectedPublisherSubstring` is already wired for the pin, and
the requirement should be made mandatory again once a signing certificate exists. A signature that is
present but *bad* (tampered, expired, revoked) is still rejected outright and is never excused by a
matching digest — only a wholly absent signature is.

### The fix cannot deliver itself — added after shipping v4.99.27

**This is the part the original write-up got wrong, and it matters more than anything else here.**

The section above reads as though publishing a fixed release repairs auto-update for everyone. It does
not. The broken verifier lives in **the client**, so every installation older than `v4.99.22` still
contains it — and rejects the very release that fixes it.

Observed on the owner's own machine, which was on `v4.99.13`. Publishing `v4.99.27` changed nothing for
it; clicking *Check for updates* produced:

> **Update failed** — Could not install the update: Downloaded installer is not Authenticode-signed.

That string no longer exists on any code path in `v4.99.22+` (it was replaced by "The downloaded update
could not be verified…"), which is what identifies the dialog as coming from the old client rather than
from a regression in the fix.

**So: `v4.99.27` must be installed manually, once, on every existing installation.** After that, updates
work normally. There is no way to fix this remotely — the code that has to change is the code doing the
rejecting.

**Verified against the real published artifact**, not reasoned about. The released
`UnifiedMessengerSetup.exe` reports `NotSigned`, and `v4.99.27`'s verifier:

| Input | Result |
|---|---|
| Real installer + its published `.sha256` | **accepted** |
| Real installer, no digest | rejected, and the message contains no "Authenticode" jargon |

A manual install of `v4.99.27` was then performed end to end — download, checksum-match against the
published sidecar, silent install, relaunch — and all user data survived byte-for-byte (3,375 chats, 454
awaiting, 11 accounts, 15 WebView2 profiles, `settings.json` hash unchanged).

**Consequence for the release notes.** Anyone reading the `v4.99.27` release who is on an older build
needs telling that their app will not update itself. That is a property of the *previous* release, so no
future change can communicate it — it has to be said on the release page.

## F-OFFLINE-02 — Raw error codes and developer instructions shown to the customer

- **Severity:** S3 · **Confidence:** confirmed · **Status:** **FIXED**, `NetworkFailureDescriber`

Three separate leaks, all reproduced by test before the fix:

| Surface | Was | Now |
|---|---|---|
| Personal dashboard tile | `HostNameNotResolved` | `No internet connection` |
| Update check dialog | `No such host is known. (api.github.com:443)` | `Could not reach the update server. Check your internet connection and try again.` |
| No-release dialog | `Publish a GitHub release with asset 'UnifiedMessengerSetup.exe', or verify the token in UNIFIED_MESSENGER_GITHUB_TOKEN.` | `No updates are published yet. You are running the newest version available.` (diagnostic moved to the log) |

The third is the worst of them: the developer's to-do list, rendered in a dialog to the person who bought
the product, naming an environment variable they have never heard of and could not act on.

Connectivity failures are classified in `GitHubUpdateService` by **exception type**, which is reliable,
with a string-matching backstop in the presenter. Certificate and proxy failures get their own wording
rather than being folded into "no internet", because those need a different person to fix them. A
`JsonException` — a reachable server returning nonsense — is deliberately *not* reported as a connectivity
problem.

## F-OFFLINE-03 — The installer download could hang forever

- **Severity:** S3 · **Confidence:** likely (reasoned from the API contract, not reproduced) · **Status:** **FIXED**

`HttpClient.Timeout` does not cover the response body when the response is read with
`HttpCompletionOption.ResponseHeadersRead`, and `CopyToAsync` was passed the caller's token — which on the
automatic path is `CancellationToken.None`. A connection dying mid-transfer would leave the copy waiting on
a socket that never delivers, in a task nobody awaits. Now bounded by a 10-minute linked CTS, and a partial
download is deleted rather than left in `%TEMP%`.

Also fixed alongside: the automatic update path now logs when a download runs and then fails, instead of
discarding the exception. Silent is right for the *check* (the user did not ask); silent is wrong for work
that ran and failed.

## F-OFFLINE-04 — An account whose page failed to load was never retried

- **Severity:** S2
- **Confidence:** confirmed — the gap by test, the fix observed working in the live app
- **Status:** **FIXED** in `v4.99.22`. Guards: `NavigationRetryTests` (31, green).

A failed navigation set the account to `Error` and returned. Nothing ever tried again. So an owner who
opened the app while the router was rebooting had a dead account until they noticed and refreshed it by
hand — no matter how long the network had been back.

**Why the existing safety net missed it, which is the interesting part.** There *is* a recovery path:
`AdapterHealthMonitor` → `AdapterStaleDetected` → `RecoverStaleAdapterAsync`, which reloads the WebView.
It simply never fires for this case. Staleness is measured from an adapter's heartbeat, and an account
whose page never loaded never got an adapter — it sits in `Unknown`/`NoAdapter`, which
`EvaluateIsStale` explicitly excludes:

```csharp
if (status.State is AdapterHealthState.NoAdapter or AdapterHealthState.Unknown) return false;
```

The net is real and has a hole exactly where the offline case lands. `NavigationRetryTests` pins that
exclusion so the hole cannot quietly close and leave the new retry looking redundant.

**The fix.** `NavigationRetryScheduler` — only connectivity-class failures (a bad certificate or a proxy
demanding credentials will not fix itself), five attempts backing off 10s → 30s → 60s → 2m → 5m, reset on
success, keyed by **instance id** and never by `CoreWebView2` (the projection trap in `AGENTS.md`).
`InstanceSessionManager` calls `Forget` before disposing a session so a pending retry cannot resurrect a
reaped or deleted account.

**Live evidence** — two accounts, real failures, through the dead proxy:

```
15:11:58Z '6c604e13…' could not load (ConnectionAborted); retrying in 10s (attempt 1 of 5).
15:12:08Z Reconnect attempt firing for '6c604e13…'.
15:12:08Z '6c604e13…' could not load (ConnectionAborted); retrying in 30s (attempt 2 of 5).
15:12:38Z Reconnect attempt firing for '6c604e13…'.
15:19:49Z '29b140ff…' could not load (ConnectionAborted); retrying in 10s (attempt 1 of 5).
15:19:59Z Reconnect attempt firing for '29b140ff…'.
```

The retry fires on schedule and the backoff lengthens. **Not observed live:** the give-up at attempt 5
(the test was stopped before the ~8.5-minute sequence completed). That path is unit-tested only.

A diagnostic line was added because of this test: the first run produced "retrying in 10s" and then
*nothing*, with no way to tell whether the reload had run and failed again or had never been reached.
`OperationCanceledException` was being swallowed silently. Both the firing and the cancellation are now
logged — a retry you cannot observe is a retry you cannot claim works.

## F-OFFLINE-05 — OPEN: the sidebar still reads "Connection error" offline

- **Severity:** S3 · **Confidence:** confirmed · **Status:** **NOT FIXED — the fix did not take effect**

`ComposeRowSubtitle` (the visible row text) took no detail parameter at all, and `ResolveStatusSubtitle`
(the tooltip) accepted one and never read it — so every failure collapsed to the same three words, with
no action, right beside a signed-out row that reads "Signed out — tap to reconnect".

Both were changed to use `NetworkFailureDescriber`, and the unit tests pass. **But a UI Automation
capture of the running app, on the freshly published binary (`4.99.21.0`, built 18:47, launched 20:11),
still read:**

```
group name:  Depilex DHA-2 WhatsApp, Connection error
visible text: 'Depilex DHA-2 WhatsApp'
              'Connection error'
```

So the stored `Detail` at render time is not the `ConnectionAborted` string the navigation hook writes.
**This is reported as unfixed, because it was not observed working.** Suspects, neither yet eliminated:

- `MainWindow.OnSessionFailed` writes `e.Error.Message` (would not match the describer) — but it also
  calls `AppLogger.LogError`, and no `[ERR]` line appeared, which argues against it.
- `PlatformAdapters` line 268 writes `ex.Message` — but it sits in the `OnNavigationCompletedAsync` catch,
  which only runs on a *successful* navigation, so it should be unreachable here.
- A row rebuild racing the detail write, or the label refresh path not being reached at all.

**Next step:** log the stored detail at the point the sidebar reads it, then re-run the dead-proxy test.
One publish-and-launch cycle should settle it.

## F-OFFLINE-06 — OPEN: the "not loaded" message is wrong when the cause is no network

- **Severity:** S3 · **Confidence:** confirmed (observed live) · **Status:** **NOT FIXED**

Captured during the offline test:

```
[WRN] [IndexedDbScan.b87bf7cd…] Conversation scan could not run yet (stage 'databases-rejected')
      — this account's page is not loaded. Open the account once to finish loading.
```

The wording is from `v4.99.19`, which correctly stopped reporting a sleeping account as broken. But it
now covers a second, different cause: the page is not unloaded, it *failed to load because there is no
network*. Telling the owner to "open the account once to finish loading" sends them to do something that
cannot work until the connection is back. The scan knows the stage; it does not know the connection
status, and the two need joining.

## Verified CLEAN

| Area | Verdict |
|---|---|
| App responsiveness with every web client failing | **Clean** — `Responding=True` throughout, UI Automation tree fully enumerable, 225 MB working set |
| Ollama / local AI | **Unaffected by design** — localhost, so internet loss is irrelevant. States (`Running`/`NotRunning`/`Starting`/`Error`) already have plain-English copy in `AiSettingsSectionHelper`, and `AiInferenceQueue` gates on `Running` |
| Update check timeouts | **Clean** — 30s `HttpClient.Timeout` plus a 45s linked CTS around the whole check |
| SHA-256 sidecar fetch | **Clean** — already wrapped in its own try/catch returning null, so a failed sidecar never throws |
| Oversight data leaving the box | **Clean** — nothing added here transmits anything; the new code only reads local state and writes to `app.log` |

## What was NOT covered

- **The updater's own network path was never exercised against a real outage.** The dead-proxy technique
  only affects WebView2; `HttpClient` ignores it. The offline update messages are unit-tested against the
  real .NET exception strings, but no actual failed GitHub call was observed.
- **The give-up-at-5-attempts path** was not observed live (see F-OFFLINE-04).
- **Recovery when the network returns** was not tested — the app was stopped while still offline. The
  retry should reload into a successful navigation, but that has not been watched happening.
- **A network that drops *while* the app is running and pages are already loaded** — the case where
  WhatsApp Web handles its own reconnection and no `NavigationCompleted` fires at all. That is the more
  common real-world scenario and it is untested; the app may keep reporting "Connected" while the web
  client itself is offline.
- **Google review scraping offline** was not separately exercised.
