# Product-hardening audit — handoff

**Branch:** `audit/product-hardening` (32 commits ahead of `main`) · **Head version:** `v4.99.23`
**Written:** end of session 1, for a cold start in a new chat. **Updated:** session 2, after the DST, offline and dialog passes
(§5.2 items 1 and 2 done, 3 half done; §2's tallies still describe session 1 only).

Read this file first. It contains facts established the hard way; re-deriving them costs hours and
several of them contradict `AGENTS.md`.

---

## 1. The mission, and the bar

End with a product that could be put on sale tomorrow: no crashes, no dead ends, **no wrong numbers**, no
half-built promises. "Sellable" means production polish, **not** commerce plumbing — do not build
licensing, trials, activation, payment or telemetry. Judge everything against *"a stranger paid for this
and is using it unsupervised."*

You are autonomous: audit, fix, test and commit without asking. Stop only if proceeding would be
destructive, irreversible, or would send data off-box.

### Non-negotiable constraints (must be in every subagent prompt — they start cold)

1. Nothing on cloud. No APIs. No recurring cost.
2. **Zero oversight data leaves the machine** — never transmit metrics, message content, customer
   identities or AI prompts off-box. No telemetry, no analytics, no crash upload, ever. This governs data
   the app *derives*; it does not prohibit a user-initiated browse tab (their own traffic, isolated
   WebView2 profile). Oversight data must never reach a browse tab.
3. The app never auto-sends. Automation is read-only scraping only.
4. All AI is on-device via Ollama. No cloud LLM.
5. No roles or permissions.
6. No unofficial protocol libraries (Baileys, whatsmeow) — ban risk. Real web clients in WebView2 only.
   Such projects may be *read* for DOM knowledge; their code may not be vendored. GPL/AGPL is
   reference-only; MIT needs attribution in `THIRD-PARTY-NOTICES.md`.
7. **Google is a reviews + Q&A channel, permanently.** Business Messages shut down July 2024 and the data
   was deleted. Never add awaiting-reply / FRT / message-count plumbing for Google.

A "fix" that violates any of these is not a fix. Log it as `WONTFIX-BY-CONSTRAINT` with reasoning.

---

## 2. Where things stand

**26 commits, 22 numbered increments, `v4.99.1` → `v4.99.20`.**
**164/164 tests green. Build clean at 0 warnings throughout.** 19 new test files, 387 source files touched.

### Severity tally

| | Closed | Open |
|---|---|---|
| **S1** | 8 | **0** |
| **S2** | 12 | 3 (see §5) |
| **S3** | 5 | several, all recorded |

### Findings documents — one per domain, in `docs/audit/findings/`

`orchestrator.md` · `crash-errors.md` · `durability.md` · `metrics.md` · `snapshot-reader.md` ·
`chat-entry-parser.md` · `install-upgrade.md` · `accessibility.md` · `performance-leak.md` ·
`state-matrix.md`

Also: `docs/audit/ASSUMPTIONS.md` (decisions made autonomously, with the cost of being wrong).

### The five most serious things found

1. **The rounding lie — 7 sites, 4 files.** `Math.Round` turned 996/1000 into **100%**, so a card showed a
   green *"100% caught up"* beside *"4 awaiting"*, and the KPI tile showed *"SLA met 100%"* beside a reply
   set containing a breach. Also ran the other way (1/1000 → 0%). Fixed by `MetricMath.HonestPercent`,
   which reserves 100 for "nothing outstanding" and 0 for "nothing done". **Consolidating after the second
   occurrence was what made sites 4–7 one-line changes.**
2. **The hero blamed the wrong branch.** `oldest 75d · Depilex DHA-2 WhatsApp` — joined with `·`, that
   reads as one sentence, but that account's own card said `Longest wait: 50d`. The 75-day customer was at
   a different branch. Two independent bugs underneath: the name came from "most awaiting" while the
   duration came from "oldest anywhere", and the two were measured over **different time windows**.
3. **The ARM64 installer shipped a binary 16 versions stale**, stamped as current, with no warning. Now
   blocked at compile time by `installer-verify-payload.iss` — which **caught a live instance on its first
   run**.
4. **Updating deleted the log and the settings-recovery file.** `LegacyInstallDir` *is* the user-data
   directory, and the stale-binary cleanup preserved only `*.json`. Since auto-update is on by default,
   users lost `app.log` and `settings.json.corrupt-….bak` without choosing to.
5. **WhatsApp's own notice account counted as a waiting customer** — found in the owner's live data as
   `{"conversationKey":"0@c.us","customerName":"WhatsApp Business","isAwaiting":true}`, 26 days old and
   impossible to clear.

---

## 3. Facts established the hard way — do not re-derive

### Corrections to `AGENTS.md` (some now fixed in that file, some still stale)

- **`PlatformDefinition.All` has NINE platforms, not six.** `HiddenFromPicker` hides `telegram`,
  `metabusinesssuite`, `instagram`. The picker offers six: whatsapp, whatsappbusiness, googlebusiness,
  messenger, discord, generic. **Telegram was already hidden** — the audit brief's §7 premise was wrong.
- **The AI layer sends customer names and message bodies.** `AGENTS.md` said "aggregate counts only"; that
  is true of `OversightInsightService` **only**. `AiInferenceQueue` → `TranscriptBuilder` sends the
  customer name and 800 chars of message body. On-box via Ollama, so permitted — but any privacy analysis
  premised on "aggregates only" is wrong. Now corrected in `AGENTS.md`.
- **`AGENTS.md`'s phase roadmap is a historical snapshot** last revised at v4.53.0. It lists shipped work
  as pending. `CHANGELOG.md` is the accurate record.

### Environment and tooling gotchas

- **Publish before you conclude anything from the live app.** I twice tested a stale binary after building
  but not publishing. Always check `(Get-Item …\publish\UnifiedMessenger.exe).VersionInfo.FileVersion`.
- **Kill the app before publishing** — it locks `UnifiedMessenger.dll` and publish fails after 10 retries.
- `dotnet-counters` is **not installed**; no GC-heap instrumentation available.
- **`python` is not available**; `perl` and `sed` are.
- The `.iss` files are read as **ANSI, not UTF-8** — a pre-existing em-dash already renders as mojibake.
  Keep any user-visible installer string pure ASCII.
- **Do not use PowerShell 5.1 to append to a UTF-8 doc.** `Get-Content` defaults to the ANSI codepage on
  a file with no BOM, so a round-trip through `Get-Content`/`Set-Content -Encoding utf8` turned every
  em-dash in `CHANGELOG.md` and `metrics.md` into `â€"` — including the *pre-existing* content, not just
  the added text. Caught and reverted, but it silently corrupts the whole file. Use the Bash tools (`cat
  >>`, or `head`/`tail` reassembly) which are byte-faithful. Bash heredocs also failed on this content;
  writing the fragment to a scratch file and `cat`-ing it is the reliable path.
- **UI Automation is the workhorse.** `Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes`, then
  find the window by `ProcessId`. This is the same tree a screen reader consumes.
- **`$HOME` is a read-only PowerShell variable** — assigning to it aborts the script mid-way with a
  confusing `VariableNotWritable`. Pick any other name.

### UI Automation pitfalls that produced false findings

- **Unrendered (virtualised) elements return `∞` bounding rectangles.** A naive `[int]` cast drops them
  silently. This made a healthy 20-account sidebar look like "only 3 of 20 accounts render" — a false S2.
  **Measure virtualised UI by scrolling, not by one snapshot.**
- **`ControlViewWalker.GetParent` often returns only the top-level window.** To identify an unnamed
  control, enumerate its **descendants** instead — that is what finally revealed the 3 mystery buttons
  were the account cards.
- **`<Button.Flyout>` matches a naive `<Button\b` regex**, and `<ToggleSwitch.Header>` as a *child element*
  is invisible to one. A static XAML sweep reported **32** unnamed controls; the live tree had **9**.
  **The live tree is the authority.**
- ContentDialogs appear as a `Window` inside the main window's descendants, and **close when your script
  takes focus** — enumerate in the same call that opens them.
- **Menu flyouts are NOT under the main window.** A `FindAll` scoped to the app window returns zero
  `MenuItem`s even with the menu visibly open. Search from `RootElement` filtered by `ProcessId`.
- **Sidebar account rows expose no `Invoke` or `SelectionItem` pattern**, so UIA cannot activate them.
  Synthetic input is required — `SetCursorPos` + `mouse_event`, with a `MOVE` event and ~400 ms settle
  before button-down or WinUI ignores it. Even then it is only about two-thirds reliable; retry.
- **Never invoke every element named "Close".** It matches the **title-bar** Close button and shuts the
  app. Scope the search to the dialog's own subtree.
- **Menu items are found by their accessible name, which differs from the visible text** — "Rename
  instance" vs "Rename instance...", "Remove instance permanently" vs "Remove instance...".

### Testing gotchas

- **`JsonElement.GetString()` throws** on a number/boolean (returns null only for `JsonValueKind.Null`).
- **`TryGetInt32` also throws** when the element is not a Number — the `Try` prefix covers only whether it
  fits in an `int`. *My own test caught this after I thought the fix was complete.*
- **`DispatcherQueue.GetForCurrentThread()` cannot activate in a plain xUnit host**
  (`COMException: ClassFactory cannot supply requested class`). Any test path reaching WinUI dispatching
  will fail for environmental reasons — extract the pure decision and test that instead.
- **A day series that walks backwards from `DateTime.Today` cannot see a future fixture date.** Two DST
  tests failed with empty results that looked exactly like product defects and were not: the 2026
  fall-back date is in the future relative to now, and `GetDailyMedians` only walks back.
  `DstTimeZones.LatestPastSpringForward()` / `LatestPastFallBack()` exist for this. Tests that inject
  their own `nowUtc` are free to use fixed dates.
- **A regression injection that catches nothing may mean your tests have a hole, not that the code is
  fine.** Injecting duration-based day arithmetic into `BuildTrend` passed everything, because every test
  had placed the short day at *today*, where the gap to the previous day is still 24 hours. The gap only
  opens when the transition day is *behind* today. Worth remembering as a general habit: when a
  deliberately broken build stays green, interrogate the test set first.
- **`AppLogger` writes to the real user-data root.** Tests exercising production logging polluted the
  owner's `app.log` with fabricated `[ERR]` lines. Now suppressed globally via
  `AppLogger.SuppressWritesForTests`, set by a `[ModuleInitializer]` in `TestAssemblyInit.cs`.

### Working with the owner's live data

The machine holds **real business data**: 11 accounts, ~380 awaiting chats, 31 WebView2 profiles, 7.2 GB.
Every destructive test in this audit followed the same protocol, and you should too:

1. Copy `%LOCALAPPDATA%\UnifiedMessenger\*` (files only) to a timestamped scratchpad folder.
2. **Verify the backup by byte count or SHA256 before touching anything.**
3. Run the test.
4. Restore and **re-verify by SHA256**.

One exception worth understanding: after the `0@c.us` fix, I deliberately did **not** restore — the app
had rewritten the snapshot correctly, and restoring would have reintroduced the defect.

---

## 4. Process that worked — keep doing this

- **Write the failing test first.** Every metric defect (F-METRICS-02/03/04/07/09) was proven by a test
  that failed with a readable message *before* the fix: `"4 customers are waiting but the card claims 100%
  caught up"`. That is what separates a real finding from a plausible one.
- **Prove the guard catches the regression.** After each fix, revert it and confirm the tests fail. Done
  for the flush isolation (4/6 failed), the contrast tokens (5/7 failed), and the platform gate (4 failed).
- **Verify against the running app, not just the build.** Several fixes looked right and were confirmed
  only by UIA capture on real data.
- **Record clean results.** Roughly half this audit's value is "checked, and here is why it is fine" —
  torn writes, the leak, keyboard focusability, producer parity. Without that, the next person re-audits.
- **Spot-check before filing.** Three near-miss false S1s were caught this way: the Ollama endpoint
  (`IsReadOnly="True"` — not UI-editable), the README's business-hours claim (`BusinessHoursCalculator`
  exists; AGENTS.md was stale), and the virtualised sidebar.
- **Commit messages carry the reasoning.** They are the durable record of *why*, including what was
  deferred and what was not verified.

### What did NOT work

**Six concurrent heavy subagents exhausted the session limit and produced zero output files.** Nothing was
written to disk before they died. If you use subagents:
- run **two at most**, and
- instruct them to **create their findings file within their first five tool calls and append as they go**,
  because whatever is on disk is all that survives.

Everything in this audit after that point was done directly, which was slower per-finding but never lost
work.

---

## 5. What is left

### 5.1 Open findings

| ID | Sev | What | Where |
|---|---|---|---|
| **F-DURA-01** | S2 | Settings reset is logged and recoverable but **the user is still not told on screen**. `RecoveredFromCorruptFile` and `CorruptFileBackupPath` already exist on `AppSettingsService` — the state a notice needs is there; only the UI is missing. | `durability.md` |
| **F-SNAP-02** (partial) | S2 | An unreadable account now says so, but the log is still the only place a *degraded* read (store bridge failed, IndexedDB succeeded) is visible. | `snapshot-reader.md` |
| **F-OFFLINE-05** | S3 | **The sidebar wording fix did not take effect.** `ComposeRowSubtitle`/`ResolveStatusSubtitle` now use `NetworkFailureDescriber` and unit-test green, but a UIA capture of the running binary still read "Connection error" — the stored `Detail` at render time is not the `ConnectionAborted` the nav hook writes. Suspects listed; none eliminated. One publish-and-launch cycle with the detail logged should settle it. | `offline.md` |
| **F-OFFLINE-06** | S3 | "this account's page is not loaded. Open the account once to finish loading" is shown when the real cause is **no network**, sending the owner to do something that cannot work. The scan knows its stage but not the connection status. | `offline.md` |
| **F-METRICS-11** | S4 | End-of-day projection skews on a 23/25-hour day. **Deliberate WONTFIX** — measured under 2%, on a figure that is explicitly a forecast. Reasoning and the bound are recorded; do not "fix" it without reading why. | `metrics.md` |
| **F-ORCH-06** | S3 | Settings speaks developer vocabulary — "instances", "Refresh all WebViews", "Export instances.json", "Enable per-instance sleep unload". These are also the **accessible names**, so screen-reader users get only the jargon. **Do not blanket-rename**: "your local Ollama **instance**" is correct English and must stay. | `orchestrator.md`, `accessibility.md` |

### 5.2 Highest-value remaining work, in the order I would do it

**1. ~~DST boundary testing (metrics).~~ DONE in session 2 — `v4.99.21`, Increment 23.** Found and fixed
**F-METRICS-10 (S2)**: nine sites across five files built a date window as
`new DateTimeOffset(nowLocal.Date, nowLocal.Offset)`, pairing a local midnight with a *different* instant's
UTC offset, so "Today" and "Last 7 days" were an hour wrong in one direction each transition day. The four
paths named here were tested and are **clean** — they subtract calendar dates, not durations.
`GetEndOfDayProjection` does skew, but under 2%, and is a recorded WONTFIX (**F-METRICS-11**).
The blocker is now removed: `UnifiedMessenger.Tests/DstTimeZones.cs` provides two synthetic zones and the
day-bucketing paths take an optional `TimeZoneInfo`. **Still untested across a transition:**
`KpiTrendStore`, `BusinessHoursCalculator`, `QuietHours`. Full write-up in `metrics.md` → "DST boundary
testing (session 2)".

**2. ~~Offline behaviour.~~ DONE in session 2 — `v4.99.22`, Increment 24.** Found **F-OFFLINE-01 (S1)**:
auto-update has *never* been able to succeed — the installer is unsigned and the verifier required
Authenticode, so with the shipped defaults it re-downloaded the whole installer at every launch, rejected
it, and threw the failure away silently. Also **F-OFFLINE-04 (S2)**: an account whose page failed to load
was never retried, because the stale-adapter net only watches accounts that reached `Ready`. Both fixed.
**Two findings left open — F-OFFLINE-05 and F-OFFLINE-06 — see §5.1.** Full write-up in `offline.md`.

> **Reuse this technique.** No elevation is needed and the machine never goes offline: launch with
> `$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--proxy-server=127.0.0.1:9"` to cut off only this app's
> web clients. A firewall rule **would not have worked** — WebView2 runs as separate `msedgewebview2.exe`
> processes, so a rule scoped to `UnifiedMessenger.exe` blocks the updater and leaves the web clients
> online. Verified live: the real `CoreWebView2WebErrorStatus` offline is **`ConnectionAborted`**.

**3. ~~Open the remaining 11 dialogs.~~ HALF DONE in session 2 — `v4.99.23`, Increment 25.** Five more
opened live: **AccountDetail, ChangeIcon, Rename, DeleteInstance, WorkspaceManagement**. Found
**F-DIALOG-01 (S2)** — the icon picker announced **25 identical "button"s**, making it usable by sight
only — and **F-DIALOG-02 (S3)**, unnamed customer rows in the drill-down. Both fixed and verified live.
Three verified clean. Full write-up in `dialogs.md`.

**Still not opened, with reasons:** `SetLocationDialog` (synthetic right-click is only ~⅔ reliable),
`EditInstanceMetadataDialog` (trigger not located), `PinToTaskbarDialog` (once per install, already
prompted), `AutoUpdateDialog` (needs a newer GitHub release to exist), `ConfirmPermanentDeleteDialog`
(**deliberately not reached** — second step of permanent deletion, on a machine holding real accounts).

> **Technique, recorded because it took several attempts.** Enumerate in the **same call** that opens the
> dialog. Menu flyouts are **not** under the main window — search from `RootElement` filtered by
> `ProcessId`. Sidebar rows expose **no `Invoke` pattern**, so synthetic `mouse_event` is required (with a
> `MOVE` event and ~400 ms settle). Menu items are found by their *accessible* name, which differs from
> the visible text. **Never invoke every element named "Close"** — that matches the title-bar button and
> shuts the app; it happened here.

**3b. Tab order inside dialogs, and the 0-account empty states, are still unchecked.** The latter cannot
be staged on this machine without deleting the owner's real accounts.

**4. F-DURA-01's on-screen notice.** The plumbing exists; this is a contained UI increment.

**5. The remaining state-matrix cells.** All-caught-up (needs a synthetic zero-awaiting state — never seen
live), AI on vs off (the heuristic-fallback path has never been observed), quiet hours, date-range
interactions.

**6. Semantic status colours contrast.** Only the brand token was measured. Success/warning/danger — used
for the awaiting pill and on-time percentages — are unmeasured in both themes, and status colour is
exactly where colour-only encoding hides. `BrandContrastTests` has a reusable WCAG calculator that parses
`Tokens.xaml`; extend it.

**7. A real multi-hour soak.** The leak check was an 11-minute accelerated proxy (160 navigations) and
found no leak, but it cannot see a slow leak. **WebView2 held 1.7–2.0 GB across 17 processes** — several
times the app's own footprint — and is where the memory actually is. Watch that specifically.

**8. Tab order and a real screen reader.** Order was never walked; no screen reader was actually run (I
read the UIA tree they consume, not the audio).

### 5.3 Known unknowns worth settling before selling

- **Is 20 s the right JS watchdog for a loaded but very busy account?** After v4.99.19, a slow-but-real
  scan is classified "not ready" rather than surfaced. Quieter false alarms, but a genuine slow-scan
  failure could now hide. Measure a ~850-chat scan on a loaded page.
- **The 20-account sidebar test passed; 50+ was not tested.**
- **Migrations from schema versions older than `instances.json` v5 / `settings.json` v19** are unexercised.
- ~~**The auto-updater itself** is untested~~ — **verification is now tested and was badly broken**
  (F-OFFLINE-01). Still untested: an actual GitHub download against a real outage. The dead-proxy trick
  only affects WebView2; `HttpClient` ignores it, so the offline update messages are unit-tested against
  the real .NET exception strings but no failed GitHub call was ever observed.
- **A network that drops while the app is running and pages are already loaded** — the commoner real-world
  case, where WhatsApp Web handles its own reconnection and no `NavigationCompleted` fires at all. The app
  may keep reporting "Connected" while the web client itself is offline. Untested, and the retry added in
  v4.99.22 does **not** cover it (it is driven by navigation failures).
- **Recovery when the network returns** was never watched — the offline test was stopped while still
  offline.
- **The uninstall data-erasure option added in v4.99.14 is unverified at runtime** — confirming it would
  have destroyed the owner's live data. This is the one change in the branch not proven by execution.
- **ARM64 has never been published or installed.** The new payload guard correctly *blocks* building an
  ARM64 installer until someone publishes ARM64 first.

### 5.4 Repository housekeeping

- **`main` carries a commit titled "Audit Files" (`954145e`) containing ~1,400 graphify cache files.** Not
  mine; `graphify-out/` is gitignored on this branch. Probably worth dropping.
- The branch has **not** been merged or pushed. No PR opened.
- `docs/audit/FINAL-REPORT.md` (required by §10 of the brief) has **not** been written yet. All the
  material for it is in `docs/audit/findings/`.

---

## 6. Commands you will need

```bash
# Build (fast check)
dotnet build UnifiedMessenger/UnifiedMessenger.csproj -c Release --nologo -v quiet

# Publish — the -p:Platform=x64 is MANDATORY, and kill the app first (it locks the dll)
dotnet publish UnifiedMessenger/UnifiedMessenger.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true --nologo -v quiet

# Tests — ALWAYS filter by exact class name; never run unfiltered (hangs headless).
# Do NOT pass -p:Platform to dotnet test.
dotnet test UnifiedMessenger.Tests/UnifiedMessenger.Tests.csproj -c Release --nologo -v quiet --filter "FullyQualifiedName~ExactClassName"
```

The full regression filter used throughout (all 164 tests):

```
FullyQualifiedName~NotLoadedIsNotUnreadableTests|FullyQualifiedName~ScanAppliesOnlyToScrapedChannelsTests|FullyQualifiedName~AccountReadHealthTests|FullyQualifiedName~ApplicationLifecycleFlushTests|FullyQualifiedName~BrandContrastTests|FullyQualifiedName~CaughtUpRoundingTests|FullyQualifiedName~SlaPercentRoundingTests|FullyQualifiedName~ChatEntryParserResilienceTests|FullyQualifiedName~NonCustomerConversationTests|FullyQualifiedName~PlatformDescriptionTests|FullyQualifiedName~EmptyScanNoFalseMetricTests|FullyQualifiedName~HeroSubtextAttributionTests|FullyQualifiedName~TornWriteRecoveryTests|FullyQualifiedName~CorruptFileRecoveryTests|FullyQualifiedName~FirstRunGreetingTests|FullyQualifiedName~ReviewReplyRateTests|FullyQualifiedName~BusinessReportSharePercentTests|FullyQualifiedName~TrendDayKeyingTests|FullyQualifiedName~DstGroundTruthTests|FullyQualifiedName~LocalDayBoundaryTests|FullyQualifiedName~DstMetricBucketingTests|FullyQualifiedName~EndOfDayProjectionTests
```

Append the offline suites too:

```
|FullyQualifiedName~OfflineBehaviourTests|FullyQualifiedName~NavigationRetryTests|FullyQualifiedName~DialogAccessibilityTests
```

That is **271 tests** as of `v4.99.23` (164 from session 1 + 50 DST + 52 offline + 5 dialog). Two of the retry tests
wait on real timers, so the run takes ~23s rather than under a second. When a change touches day
bucketing, also run the suites that cover the modified classes — the sweep used for Increment 23 was:

```
FullyQualifiedName~MessageAnalyticsServiceTests|FullyQualifiedName~MessageAnalyticsServiceBranchFilterTests|FullyQualifiedName~ActivityPatternsTests|FullyQualifiedName~ResponseTimeTrackerTests|FullyQualifiedName~OversightRollupBuilderTests|FullyQualifiedName~OversightRollupCapabilityTests|FullyQualifiedName~BusinessReportTests|FullyQualifiedName~KpiTrendStoreTests|FullyQualifiedName~InstallerScriptTests
```

Installer (ISCC is a **per-user** install here, not Program Files):

```
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" "D:\Projects\Unified Messenger\installer.iss"
```

**Version sync — six files, always in lockstep:** `UnifiedMessenger.csproj` (3 fields), `app.manifest`,
`installer-shared.iss`, `README.md` ("Current release"), `docs/phase-status.md` (date + baseline),
`AGENTS.md` (roadmap header), plus a new `CHANGELOG.md` section.

**Commit convention:** `vX.Y.Z: short description (Phase N — what slice) (Increment NN)`.
No `Co-Authored-By` or tool-attribution trailers. Use `git commit -F -` with a heredoc — backticks in a
`-m` string get executed by the shell (this happened once and needed an amend).

---

## 7. The one rule that matters most

**Never report a fix as verified unless you executed the verification and saw it pass. Paste the
evidence.** If a test fails, say so with the output. If a step was skipped, say it was skipped and why. A
finding inferred from reading is `likely`; only reproduction earns `confirmed`.

This audit twice reported something as done when it was not — the `FlushStoresAsync` log fix (which missed
three other suites) and the "flush permanently destroys the file" claim (never actually demonstrated). Both
are corrected in the findings docs and their commits. Keep that habit: when you find you were wrong,
correct it in the record rather than quietly moving on.
