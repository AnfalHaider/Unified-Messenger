# Findings — Memory, handles, and leak behaviour

## Method, and its honest limits

The brief asks for a multi-hour soak. **I did not run one.** Instead I ran an *accelerated* proxy, because
navigation is the classic leak vector for imperatively-built UI and surfaces in minutes what idle
accumulation takes hours to show:

1. Fresh launch against the owner's real data (11 accounts, 3 WhatsApp + Google + embeds).
2. A background sampler recording main-process working set, private bytes, handles, threads, WebView2
   process count and combined WebView2 memory every 60 seconds.
3. **160 page navigations** (40 cycles × Settings → Analytics → Reports → Reviews), driven through UI
   Automation, sampling process counters *and the live UI Automation element count* after each cycle.
4. Five minutes of post-stress idle to see whether memory is reclaimed.

Total observed window: **11 minutes**, not hours. That is stated plainly rather than dressed up.

## Result: no evidence of a leak

| Measure | Across 160 navigations | Verdict |
|---|---|---|
| UI Automation element count | 336 → 331 | **flat** — the visual tree is not accumulating |
| Handles | 1854 → 1741 | **down 113** — no handle leak |
| Threads | 79 → 65 | **down 14** — no thread leak |
| WebView2 processes | 17 → 17 | **stable** — sessions are not multiplying |
| Main working set | 336.6 → 464.8 MB | up 128 MB **during** stress |

Memory did climb during the stress — roughly 0.8 MB per navigation cycle. The decisive question is whether
that is retained or merely uncollected, and the idle window answers it:

```
elapsed  workingSet  private
  6 min    441.1 MB   348.6 MB   <- stress ends
  7 min    409.1 MB   320.5 MB
  8 min    399.0 MB   304.8 MB
  9 min    383.0 MB   287.0 MB
 11 min    366.6 MB   271.0 MB   <- still falling
```

**−74.5 MB working set / −77.6 MB private reclaimed in five idle minutes, and still declining when
sampling ended.** Combined with a flat UI element count and *falling* handles and threads, the growth
during stress was garbage from the imperative card builder that the GC had not yet had reason to collect —
not retained objects.

**Verdict: no leak found.** Stated as "no evidence of a leak in an 11-minute accelerated window", not as
"there is no leak".

## Static corroboration

Checked alongside the measurement, since a leak usually has a visible mechanism:

| Pattern | Result |
|---|---|
| Event subscriptions to long-lived publishers | 8 sites, all singleton→singleton (`DashboardRefreshCoordinator`, `TriagePersistenceService`), **all with matching `-=`** |
| Page/control subscriptions | all self-scoped (`Loaded`/`Unloaded` on self, handlers on locally-created buttons that die with them) |
| `DispatcherTimer` handlers | `CommandCenterPanel` pairs subscribe/unsubscribe/stop/null; `ReviewHealthPanel` defensively does `-=` before `+=` — the correct guard against double-subscription |
| `OversightInsightService` cache | keyed by `entityKey` and overwritten, **not** by the time-varying render signature — bounded by account count |

No unbounded-growth mechanism was found to match the (absent) symptom.

## Caveats — what this does not prove

- **11 minutes is not a multi-hour soak.** A slow leak of a few hundred KB/hour would be invisible here.
  The accelerated navigation test covers the UI-churn vector specifically; it does **not** cover
  long-running background timers, the idle-session reaper, or overnight WebView2 drift.
- **No GC-heap instrumentation.** `dotnet-counters` is not installed, so managed-vs-native attribution is
  *inferred* from the post-stress recovery rather than measured. I could not force a collection or read
  gen0/1/2 sizes.
- **WebView2 dominates and is largely outside the app's control.** 17 processes held **1.7–2.0 GB**
  throughout — several times the app's own footprint. It oscillated (1788 → 1657 → 1997 MB) with no clear
  trend in 11 minutes, but a real soak would need to watch this specifically, since it is where the memory
  actually is.
- **Only one workload was exercised** (page navigation). Adding/removing accounts, long Re-sync runs, and
  the AI inference queue were not stress-tested.

---

### F-PERF-01 — Three accounts are silently contributing no oversight data on this machine right now

- **Severity:** S2
- **Confidence:** confirmed (observed in the live log during the run)
- **Where:** `app.log`, repeated across the session
- **Status:** **OPEN** — surfaced, not fixed.
- **What the log shows:**
  ```
  [WRN] [IndexedDbScan.1e3697ce…] Conversation scan function is not injected on this page; no oversight data was read.
  [WRN] [IndexedDbScan.f13adf59…] Conversation scan function is not injected on this page; no oversight data was read.
  [WRN] [IndexedDbScan.efd5aa0b…] Conversation scan function is not injected on this page; no oversight data was read.
  [WRN] [IndexedDbScan.de1b5592…] Conversation scan settled at stage 'watchdog-timeout' instead of 'done'; no oversight data was read.
  ```
  Three distinct instances repeatedly report the scraper is **not injected**, and a fourth times out.
- **Why this matters:** these are exactly the diagnostics added in `v4.99.6` (F-SNAP-01), which previously
  returned `null` in silence. They are doing their job — this degradation was invisible before. The
  `v4.99.8` work should now render those accounts as "can't read this account — click Re-sync" rather than
  as quiet.
- **TRIAGED AND FIXED** in `v4.99.18` — see resolution below.

#### F-PERF-01 resolution

Mapping the instance ids against `instances.json` settled it immediately:

| id | platform | account |
|---|---|---|
| `1e3697ce` | **googlebusiness** | Google Depilex Men DHA-2 |
| `f13adf59` | **googlebusiness** | Google Depilex F-11 |
| `efd5aa0b` | **googlebusiness** | Google Depilex DHA-2 |
| `de1b5592` | whatsapp | Depilex F-11 WhatsApp (the `watchdog-timeout`, a genuinely different case) |

**All three "not injected" warnings were Google Business accounts** — a reviews + Q&A channel that has no
conversation scraper and, per the permanent product decision, never will. They were behaving exactly as
designed. `OversightAlertMonitor` selects instances on `IsProfessional` and connection status alone
(`OversightAlertMonitor.cs:83-90`), with no check for whether the platform participates in the WhatsApp
pipeline — so the conversation scan ran against all three every cycle, forever.

**This also exposed a regression introduced by my own `v4.99.8` change.** Because the scan reached
`RefreshAsync`'s failure path, `AccountReadHealth.RecordFailure` fired for each Google account — which
would render **"can't read this account — click Re-sync"** on three perfectly healthy accounts. That is the
precise false positive `AccountReadHealth` was written to avoid, and I wrote the warning about it into the
F-SNAP-02 finding before creating it here.

**Confirmed, not assumed:** removing the new gate makes 4 tests fail, including
`NoEmbedOnlyChannelIsEverMarkedUnreadable` for messenger, discord and generic. The false positive was live
and reproducible.

**Fix applied.** The gate goes in `OversightSnapshotReader.RefreshAsync` rather than at one call site, so
both callers (the background monitor and the manual Re-sync probe) are covered. It returns `null`
**without recording a read failure** — the distinction that matters: these accounts are not failing, the
scan does not apply to them. `PlatformModuleSettingsHelper.IsPlatformModuleEnabled` was already the correct
gate and already documented as "WhatsApp family only"; it simply was not being consulted here.

A second, milder instance of the same category error was fixed alongside it: the manual Re-sync probe
reported Google accounts as *"still loading — open this account once to finish loading"*, sending the owner
to open a tab that could not change the outcome. It now says *"no conversation metrics for this channel"*.

**Verified live:** 90 seconds of running produced **zero new log lines** (previously three warnings per
scan cycle), and the UI shows **zero** "can't read this account" warnings with all three Google accounts
present normally.

**Still open:** the `watchdog-timeout` on `de1b5592` (a real WhatsApp account) is a genuine, separate
condition and was **not** investigated. It means a scan started and did not settle — worth understanding,
but it is a legitimate warning rather than noise, which is exactly the signal the fix above stops drowning.

---

### F-PERF-02 — The test suite writes fake `[ERR]` entries into the user's real diagnostic log

- **Severity:** S3
- **Confidence:** confirmed (found in the owner's `app.log`; fixed and re-verified)
- **Where:** `UnifiedMessenger/Services/ApplicationLifecycleService.cs` (`FlushStoresAsync`), `UnifiedMessenger.Tests/ApplicationLifecycleFlushTests.cs`
- **Status:** **FIXED** in `v4.99.17`.
- **What was happening:** `ApplicationLifecycleFlushTests` exercises the real `FlushStoresAsync` with
  deliberately-throwing fake stores. `AppLogger` writes to a fixed path under the real user-data root, so
  every test run appended genuine-looking error lines to `%LOCALAPPDATA%\UnifiedMessenger\app.log`:
  ```
  2026-08-11 14:49:19Z [ERR] [Lifecycle.Flush.third] System.IO.IOException: third could not be written
  2026-08-11 14:49:19Z [ERR] [Lifecycle.Flush.a]     System.IO.IOException: a could not be written
  2026-08-12 12:46:29Z [ERR] [Lifecycle.Flush.AwaitingOverrides] ...
  ```
  `first`, `third` and `a` are test fixture names — those failures never happened to any user.
- **Why it matters more than it looks:** `app.log` is the product's *only* diagnostic surface, and several
  increments of this audit (v4.99.4, .5, .6, .7) were spent deliberately routing real failures into it so
  support and the owner would have something to read. Seeding it with fabricated `[ERR]` lines undermines
  exactly that. On a developer or support machine, `Lifecycle.Flush.AwaitingOverrides` is indistinguishable
  from a genuine failure of the awaiting-overrides store.
- **Fix applied:** `FlushStoresAsync` takes an optional `logFailure` callback defaulting to `AppLogger`;
  the tests pass a no-op. The production path is unchanged.
- **Found by accident** — reading `app.log` to check whether 160 navigations had produced errors, and
  noticing test fixture names in it. Worth recording as a reminder that the diagnostic surface itself
  needs auditing, not just the code that writes to it.
- **Residual:** the owner's existing `app.log` still contains the historical fake entries from previous
  test runs. Not scrubbed — editing a user's log to hide evidence of our own defect would be worse than
  leaving it. It will rotate out.

---

### F-PERF-03 — An account that has simply not loaded yet was reported as unreadable

- **Severity:** S2
- **Confidence:** confirmed (root cause traced from the live log; classification pinned by test)
- **Where:** `UnifiedMessenger/Services/Oversight/OversightSnapshotReader.cs` (`RunScanAsync` / `RefreshAsync`)
- **Where:** `UnifiedMessenger/Assets/Scripts/whatsapp-adapter.js:1445` (the 20-second JS watchdog)
- **Status:** **FIXED** in `v4.99.19`.
- **What was happening.** `enableLazyWebViewLoading` is **on by default**, so a background account's page
  has never navigated to WhatsApp Web. The injected adapter is therefore absent and `indexedDB.open`
  blocks, so the JS watchdog settles the scan at `watchdog-timeout` after 20 seconds. The host treated any
  stage other than `done` as a failure, so `AccountReadHealth.RecordFailure` fired.
  Result: an account that had merely never been opened would render **"can't read this account — click
  Re-sync"**. That advice is wrong twice over — nothing is broken, and **Re-sync cannot load a page that
  lazy loading deliberately left unloaded**. The account just needs opening once, which is what the
  Re-sync probe's own separate message ("open this account once to finish loading") already said.
- **This is the third instance of one pattern.** Google Business (v4.99.18), the not-injected case, and
  now the watchdog — all "the scan did not produce data" being conflated with "this account is faulty".
  The lesson recorded for whoever maintains this: `AccountReadHealth` must only ever be told about a read
  that genuinely *attempted and failed*, never about one that could not apply or could not start.
- **Fix applied.** `RunScanAsync` now returns `(RefreshResult?, bool PageNotReady)`, and `RefreshAsync`
  records **neither success nor failure** when the page was not ready — leaving the account reading
  "syncing…" until it genuinely loads. Not recording success matters as much as not recording failure: a
  success would wrongly clear a real prior failure, and a test pins that.
  The stage classification is extracted to `IsPageNotReadyStage` and lists only stages that mean the page
  never loaded (`watchdog-timeout`, `no-model-storage`, `no-indexeddb`, `no-databases-api`,
  `databases-rejected`). Stages meaning the page *was* reachable and the read still failed
  (`no-chat-store`, `getall-chat-error`, `chat-exception`, `promise-error`) still flag — a test pins that
  too, so this guard cannot quietly re-create the silence fixed in v4.99.6. Unknown stages default to
  "counts", so a stage added by a future scraper surfaces rather than being ignored.
- **Test scope, stated honestly.** The classifier is tested directly, **not** end-to-end through
  `RefreshAsync`. An attempt to test it end-to-end failed with
  `COMException: ClassFactory cannot supply requested class` — `DispatcherQueue.GetForCurrentThread()`
  cannot activate in a plain xUnit host. That is an environment limit, not a product behaviour, so those
  tests were deleted rather than left failing or weakened into passing vacuously. The wiring between the
  classifier and `AccountReadHealth` is therefore **verified by reading, not by execution**.
- **Not investigated:** whether 20 seconds is the right watchdog for a *loaded but very busy* account.
  AGENTS.md notes background webviews throttle timers, so on a slow machine a legitimate scan of ~850
  chats could conceivably exceed it and now be silently classified "not ready" rather than surfaced. That
  trade — quieter false alarms, but a real slow-scan failure now hidden — is worth measuring.

---

### F-PERF-02 — REVISED: the v4.99.17 fix was incomplete

The original fix threaded a log callback through `ApplicationLifecycleService.FlushStoresAsync` and I
reported the finding closed. **It was not.** Reading the live log again during this investigation showed
three more fabricated entries from suites that fix never touched:

```
[ERR] [Settings.Load.Corrupt]          JsonException: x
[ERR] [AwaitingOverrides.Load.Corrupt] JsonException: truncated
[WRN] [ChatEntryParser]                Skipped 1 of 2 conversation rows as unparseable.
```

`x` and `truncated` are test fixtures; "1 of 2 rows" is a constructed input. Patching one call site was
the wrong shape of fix for a problem that lives in `AppLogger`'s fixed path.

**Proper fix (`v4.99.19`):** `AppLogger.SuppressWritesForTests`, set once by a `[ModuleInitializer]` in the
test assembly. No per-suite opt-in, so a suite added later cannot forget it.

**Verified:** running `CorruptFileRecoveryTests`, `ChatEntryParserResilienceTests` and
`ApplicationLifecycleFlushTests` (32 tests) added **0 lines** to `app.log`, which stayed at 183. The same
suites previously appended to it every run.
