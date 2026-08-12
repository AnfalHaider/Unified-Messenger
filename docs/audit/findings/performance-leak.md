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
- **Not diagnosed.** Whether the injection failure is a WebView2 lifecycle issue, a navigation race, or
  those accounts being embed-only channels (Discord/Messenger/generic, which legitimately have no scraper)
  was **not** determined — I did not map the instance ids back to accounts. If they are embed-only, these
  warnings are expected noise and the log message should say so rather than implying a fault. **That
  distinction should be settled before shipping**, because a log full of routine warnings trains people to
  ignore it.

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
