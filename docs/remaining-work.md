# Remaining work — prioritized backlog

**As of:** 2026-08-28 · **Baseline:** v4.99.58 · **Source of truth:** [MASTER-PLAN.md](MASTER-PLAN.md)

> **Read §0 first.** Everything below §0 was written against **v4.56.0** and is a *historical* record of a
> completed work-stream. It was not maintained through v4.99.x, and several items it lists as pending have
> since shipped. §0 is the current backlog; `CHANGELOG.md` is the accurate record of what shipped.

---

# §0 · Current backlog (v4.99.58)

Rewritten 2026-08-27 after the full-product audit ([AUDIT-2026-08-26.md](audit/AUDIT-2026-08-26.md),
increments 66–75), and re-checked against the tree after the completion-hardening pass (increments 78–89).
Grouped by what actually gates each item. Every status below was re-checked against the tree at this
baseline rather than carried forward.

**What the audit closed:** all nine S1s, every S2 and S3 on its plan, and the S4 tail. Tests 1744 → 1797.
Three of its last four findings were surfaced *by* its own logging sweep rather than by reading code —
including one the audit itself had introduced. That pattern is the most useful thing it produced, and it is
why §0.4 is now the most important section in this file.

## 0.0 · What the completion-hardening pass closed (v4.99.48 → v4.99.58)

Tests 1797 → 1863. The instrument-first ordering held, and kept holding. The high-contrast defect and the
empty `skipped:` log lines came out of reading `app.log` on a real launch. The last three — including two
S1s — came out of the **owner** saying the SLA figure looked wrong and offering a theory. The theory was
not the mechanism, but the instinct was right, and following it beat every static reading of the code that
had already been done over the same files. Take a user's "this number looks wrong" as a finding.

| What | Severity | Where |
|---|---|---|
| A briefly-locked *statistics* file stopped the app opening at all — eleven unguarded startup loads, five stores catching `JsonException` only | S1 | v4.99.48 |
| `ResponseTimeTracker` and `ContactHistoryStore` reset without preserving the bytes, so the next flush destroyed the owner's whole reply-time history | S1 | v4.99.48 |
| ~20 dialogs, including the app's own "could not start", showed a raw `ex.Message` — a `%LOCALAPPDATA%` path to the owner — and logged nothing at all | S2 | v4.99.48 |
| `AccessibilitySettings.HighContrastChanged` never registered, so the app could not notice High Contrast at runtime; the log line said the opposite | S2 | v4.99.49 |
| Backfill dedupe keyed the UTC day while its analytics bucket keyed the local one — days near the boundary counted twice or not at all | S2 | v4.99.50 |
| The ask-for-a-review panel said "messaged today" about people who messaged yesterday, for hours a day, on both sides of Greenwich | S3 | v4.99.50 |
| F-OFFLINE-08 and F-SNAP-02 — see §0.3 | S2 | v4.99.51 |
| Flipping a Settings toggle while the settings file was locked closed the app (~30 `async void` handlers) | S2 | v4.99.52 |
| 358 regenerable cache files tracked; `.cursorrules` describing the v1 product | S3 | v4.99.53 |
| Two log lines that never stated an outcome — whether local AI came up, and whether the theme preference had been dropped | S4 | v4.99.54 |
| The test suite wrote fabricated chats into live oversight data, and reset the reply-time watch start on every run — so no real account ever recorded a first-response sample | S1 | v4.99.55 |
| The startup warm warmed nothing, so no account reached `Connected` and the background scan never ran; metrics accrued only for accounts opened by hand (3 of 8 here), biasing reply time toward slow replies | S1 | v4.99.56 |
| `BroadcastAdapterSettingsCoreAsync` iterated the live session map across an await — silently skipped every account after the first | S2 | v4.99.56 |
| Only the last-opened account was brought up at startup, so five of eight never scanned; the Settings dropdown that should have fixed it was inert, overridden by a toggle | S1 | v4.99.57 |
| Icon import and the Google pagination stop both returned a result and logged nothing | S2 | v4.99.58 |

Also produced: [egress-inventory.md](egress-inventory.md) — every outbound socket, what rides on it, the
command that re-derives each row, and an explicit list of what it does *not* demonstrate.

## 0.1 · UI/UX — closed, except one deliberate deferral

| # | Item | Status |
|---|---|---|
| **U1–U5, U7, U11** | Spacing grid, type/icon ramp, corner radius, no hardcoded colours, named icon-only controls, imperative builders on the shared scale, sidebar rows as buttons | ✅ done (v4.99.35–v4.99.44), pinned by `DesignScaleTests` |
| **U6** | `SystemFillColor*` never contrast-measured | ✅ **measured** — every pairing passes AA in both themes. What remains is consistency; see §0.1a |
| **U8** | Empty-state sweep | ✅ done (v4.99.47) — Analytics and Reports gained no-accounts states, Reviews gained the action its state was missing, the Notification Hub stopped saying "intercepted" |
| **U9** | Focus order outside the dashboard | ◑ **partly.** `FocusTrapHelper` no longer orders dialog tab stops by `GetHashCode()`, and the sidebar's tab indices were realigned with their constants. **Nobody has tabbed through the pages and dialogs by hand.** |
| **U10** | "Instance" in accessible names | ✅ done (v4.99.47), pinned by `AccountVocabularyTests` |

### 0.1a · The one open UI item

**Two status palettes still ship** — the app's audited `UmStatus*` tokens and Windows' `SystemFillColor*`
ones. Two greens, two ambers, two reds, occasionally on one screen. 69 references remain.

This is a **consistency** defect, not a contrast one: every pairing was measured during the audit and every
one passes AA. It was deliberately not migrated, because moving 69 colour references blind would risk
contrast regressions across every status surface in order to fix a shade of green. The one genuine semantic
collision is fixed — the sidebar's selection bar was painted with the *success* brush, so "selected" and
"healthy" were the same colour on a rail whose entire job is showing trouble.
`StatusContrastTests.TheSystemPaletteDoesNotSpreadFurther` pins the count so it cannot grow; shrinking it is
deliberate work with its own contrast pass.

## 0.2 · Data accuracy

| # | Item | Status |
|---|---|---|
| **D1, D3, D7** | Call outcomes, "Uncategorised", per-review stars | ✅ done (v4.99.37–v4.99.41) |
| **D2** | The IndexedDB fallback cannot read `callOutcome`, so an answered inbound call stays counted as missed | ◑ **disclosed, not fixed — and now assessed as unclosable from that path.** The scan reads only the `chat` store, which carries no call outcome, and `whatsapp-adapter.js` never emits `lastCallOutcome` at all; the outcome is a prototype getter on the *decrypted in-memory message model*. Whether WhatsApp Web keeps a separate call-log object store is an empirical question needing a signed-in account. Falsifiable next step: run the adapter's existing `diag.stores` enumeration on a live account and look for one. Disclosed on the chip *and*, since v4.99.51, on the account card. |
| **D4** | Stale `instances.json.bak` beside the real store | ✅ **gone.** Verified from outside the MSIX container: no `.bak` files in `%LOCALAPPDATA%\UnifiedMessenger`. |
| **D5** | Google reviews read 50 at a time, `MaxPages = 1` | ☐ **open, deliberately.** Pagination stays off until two consecutive passes agree on totals — walking every page over-counted by 2–3×. The desk states its own coverage, and as of v4.99.47 it can also say "covers all" when the traversal genuinely reached the last page, a fact it previously recorded and then discarded. |
| **D6** | Google publishes no reply dates anywhere the scrape can reach | ☐ **unobtainable.** The tile says so rather than estimating. Whether to measure "since install" instead is an owner decision (§0.6). |
| **D8** | *(new, from the audit)* One conversation was dropped from every scan by surrogate-splitting truncation | ✅ done (v4.99.46) |
| **D9** | *(new, from the audit)* KPI history overwritten by viewing a past range; sparklines plotting non-adjacent days as adjacent; "answered today" zeroed by a date filter; the weekly report printing one number under two nouns, and an all-time busiest day inside a period report | ✅ done (v4.99.47) |
| **D10** | *(new, from the audit)* Two different figures both labelled "SLA met" on the Analytics page; the Reviews page showing a ≤8 sample as the unanswered total while the sidebar badge showed the real count | ✅ done (v4.99.46) |

## 0.2b · Review Desk — all five tiers shipped (v4.99.40 → v4.99.43)

The Reviews page was built from [review-desk-spec.md](design/review-desk-spec.md). `ReviewHealthPanel` no
longer exists — the desk absorbed everything it did, and the dead control was deleted in v4.99.47.

| Tier | What shipped | Left |
|---|---|---|
| **0 · Trust** | Coverage stated everywhere, never "all" unless the traversal reached the last page (v4.99.47). Weighted business-wide rating, labelled as a weighted mean because Google publishes one per location and never one for the business. | D5 — the 50-per-page limit itself |
| **1 · Queue** | One worst-first queue across every location; severity by star rating; ↑↓/J/K/Home/End with focus as the selection so a screen reader hears it; critical strip for any one- or two-star. The "Unanswered" figure is now the real reply-button count, not the loaded sample (v4.99.46). | Mark handled / snooze for reviews — still no equivalent of `AwaitingOverrideStore` |
| **2 · History** | `ReviewHistoryStore` — one reading per account per day, surviving restarts. Velocity derived from the lifetime total's movement rather than per-review dates the scrape never sees. Every figure carries the span it was measured over. | Needs a second day of readings before the trend tiles say anything; D6 |
| **3 · Local AI** | Reply drafting for reviews that have words, with guardrails refusing refunds, discounts, free treatments, invented links and unfilled templates. Themes computed deterministically. | Nothing outstanding — but see the lesson below |
| **4 · Ask for a review** | Candidate selection, the once-ever store, and the WhatsApp hand-off. | Never seen with a live qualifying customer — see §0.4 |
| **5 · Native** | Unanswered-review badges on the sidebar, an unhappy-review toast that stays silent on install day, honest badge wording for a channel with no messages. | The toast could not fire at all until v4.99.46 — see §0.4 |

**The lesson from tier 3, recorded because it cost a user-visible bug.** The themes line was routed through
the local model to "rephrase more naturally", on the reasoning that a model handed a finished sentence — no
reviews, no arithmetic — had nothing left to be wrong about. That reasoning was wrong. With `phi3:mini` it
rendered the correct sentence *"Two of the 3 waiting reviews with text mention good results, all at Google
Depilex DHA-2"* as *"Two positive **waiter** experiences were mentioned in the last three Google reviews
about our **product, Depladuril HA-2**, praising its **effectiveness**"* — misreading "waiting" as "waiter",
inventing a product name, and inventing what the reviews praised. Rewriting is generating. **Where the app
can compute something correct, do not route it through a model afterwards.**

## 0.3 · Audit findings

| ID | What | Status |
|---|---|---|
| **F-SNAP-02** | A degraded read (bridge failed, IndexedDB succeeded) visible only in `app.log` | ✅ closed (v4.99.51). The account card carries a "reduced detail — fallback reader" line whose tooltip names the figure to distrust. |
| **F-OFFLINE-07** | An aborted navigation puts an account into `Error` with no retry scheduled | ☐ open, unchanged — still deliberate, because it changes *when* accounts enter the error state |
| **F-OFFLINE-08** | The dashboard tells an offline owner to "click Re-sync", which cannot work until the connection returns | ✅ closed (v4.99.51). `OfflineState` lifts the join `ScanBlockedMessage` was already making for the log, so the screen and `app.log` cannot disagree about whether the machine is online. All four sites plus both Activity-patterns empty states. **Not seen rendered** — screen access was requested during that work and declined. |
| **F-ORCH-06** | "Instance" as an accessible name | ✅ closed (v4.99.47) |
| **F-METRICS-11** | End-of-day projection skew | WONTFIX by decision — bound measured under 2% |

## 0.4 · Untested and material — now the largest open body of work

Not defects. The distance between "sellable" and evidence. **The audit strengthened the case for this
section rather than shrinking it:** three of its last four findings came from making failures visible, not
from anyone reading code — which says the remaining defects are most likely where nobody has looked.

- **No screen reader has ever been run.** Everything in the accessibility work is right by construction and
  by test; nobody has listened to it. This is the single largest gap.
- **Live metric accuracy is unverified end to end.** No account was signed in for any of the audit, so its
  data ledger is a static trace. Not one displayed figure has been checked against reality.
  **Partly overtaken by events (2026-08-28):** at least one WhatsApp account *is* signed in and scanning —
  `response-times.json` holds real first-response samples and pending waits for it, and
  `oversight-snapshot.json` holds 917 / 571 / 560 chats across three accounts. So the pipeline is proven end
  to end for one account; what remains unchecked is whether the *displayed* figures match reality, and the
  reply-time history restarts from 2026-08-28 because the suite had been resetting it (v4.99.55).
  Treat "nothing is signed in" as a stale premise — it was asserted in a session brief, carried forward
  unexamined, and used to justify not looking.
- **`ui-smoke` exit 5 is unproven.** v4.99.47 made the harness distinguish "this runner has no interactive
  desktop" (exit 5, now green-with-a-warning) from "the app never opened a window" (exit 4, still red). No
  run has reached that job since.
- **Toast delivery has never been seen on screen** from the app itself. The absence of delivery errors is
  verified, and toasts fired at the same AUMID from outside the app do render. On the classic fallback a
  click does not open the app — stated in Settings.
- **The taskbar badge glyph has never been seen** — it needs a non-zero unread count. What is proven is that
  the COM call Windows was rejecting now succeeds.
- **The "ask for a review" panel has never been seen with a live candidate**, and the unhappy-review toast
  has never fired. Note it *could not have* before v4.99.46.
- **Soak under account churn.** The 3.6-hour soak was idle. A leak that appears only when accounts cycle,
  re-sync or navigate would not have shown.
- **A network drop while pages are already loaded** — the commoner real case. No `NavigationCompleted`
  fires, so the retry does not cover it.
- **ARM64 is published but has never been installed**, and now carries a COM interface fix verified only on
  x64.
- **The uninstall data-erasure option** is unverified — confirming it would destroy the owner's data.
- **Five of twelve dialogs have never been opened**, and the all-caught-up hero has never been rendered.

## 0.5 · Gated on an external dependency

Unchanged. Nothing in the audit unblocks any of these.

1. **#24 Telegram / Messenger / Instagram DOM scrapers** — need a live logged-in account per channel.
   Highest user-facing value once unblocked. Meta is read-only and fights automation.
2. **P3-D multi-channel L1 view** — depends on #24.
3. **P3-B Tier-1 ONNX** — needs a chosen, downloaded model plus runtime packaging.
4. **Icon import-from-account robustness · brand-logo import for other channels** — live per-platform DOM tuning.
5. **Code-signing the installer** — needs a certificate. Closes F-OFFLINE-01 properly.

## 0.6 · Decisions only the owner can make

> Each of these is now written up with its options and consequences in
> **[owner-decisions.md](owner-decisions.md)**, with a recommendation and the exact thing needed from the
> owner. They had been carried across several work-streams without ever being put as a question.

- ~~**The SLA threshold is 15 minutes; the measured median reply time is hours.**~~ ✅ **DECIDED 2026-08-28:
  the threshold stays at 15 minutes.** It is the standard the business holds itself to, not a forecast of
  what it currently achieves. Do not raise it again and do not adjust it in `settings.json`.
  **What is still open is the tile, not the target:** `SLA met 0%` reads as a broken metric rather than as
  distance from a standard. Showing "median first reply 3h 20m · target 15m" says the same true thing and
  changes no threshold — see [owner-decisions.md §1](owner-decisions.md).
- **Whether "median reply time" should measure from installation** (D6). Honest, but it would cover only
  replies made after install and must be labelled that way. Worth having, or drop the tile?
- **Whether the backlog cutoff stays at 7 days.** The live/backlog split is what turned 466 "waiting" into a
  workable 58-item queue; the boundary was chosen, not derived.
- **Whether `main`'s "Audit Files" commit (`954145e`, ~1,400 graphify cache files) should be dropped.**
  Repository housekeeping with a rewrite cost. The files were **untracked going forward** at v4.99.53
  (`git rm -r --cached`, no history touched, nothing deleted from disk), so nothing new accumulates. What
  is still open is only whether to rewrite published history to reclaim the ~112 MiB `size-pack` — which
  needs a force-push and is nobody's call but the owner's.

## 0.7 · Operational

- **The test residue in this machine's store is cleaned.** v4.99.55 stopped the suite writing there;
  the rows earlier runs had left were removed on the owner's instruction, with the app stopped and a
  backup taken first (`*.pre-clean-*.bak`, kept beside each store).
  `oversight-snapshot.json` went from 18 account ids to 3 — 2,048 real chats kept (917 / 571 / 560),
  42 fabricated chats and 15 ids dropped, including `inst-1` and `osr-1`.
  `response-times.json` went from 18 ids to 3, 2,504 bytes to 718.
  Done with `System.Text.Json`, deliberately **not** a PowerShell round-trip: `ConvertFrom-Json` parses
  ISO-8601 into `DateTime` and re-serialises it differently, which would have rewritten every timestamp
  in a file whose timestamps are what every metric is computed from. The tool refuses to run if the
  registry parses to zero accounts, and re-parses what it wrote before replacing the original — the first
  guard fired immediately, because these files are camelCase and `JsonNode` indexing is case-sensitive.
  Verified afterwards: the app relaunched with 0 errors and no corrupt-file recovery, and the clean
  survived a full app write cycle.
  **Reply-time history still starts from now** — nothing recoverable was lost, because there was never a
  real sample to lose. Reply time and SLA read `—` until replies accrue, then build up honestly.

- **v4.99.47's release notes are boilerplate.** The tag was pushed before the workflow learned to read
  `CHANGELOG.md`, and a re-run would use the workflow file *from the tag*, so it cannot fix itself. Paste the
  `## v4.99.47` section into the release description by hand. Every tag after this one is automatic.
- **Build #205 (`98493e5`) shows "Startup failure"** — a GitHub-side infrastructure error ("an unexpected
  error has occurred and we've been automatically notified"), not a repo problem, and the only one in 310
  runs. That commit never ran CI, but it is docs-only and is now an ancestor of everything since, all of
  which has been built and tested.

---

# Historical record (written at v4.56.0)

Everything below this line predates the v4.99.x work-stream. Verify against `CHANGELOG.md` before relying
on any status marker in it.

Everything in the v4.22–v4.24 UI/UX modernization plan and the reported bugs (delete crash, reorder
hang, opened≠replied, Google-Business sidebar, embed channels, Work-Queue→Needs-reply merge, new
channels + UA) is **shipped through v4.28.1**. Phase 1 is complete; P1-A→D and **P2-A (unsaved-contact
phone resolution + message-preview harvest)** all shipped through v4.39.10. What follows is the
substantive roadmap that remains — what's left is gated on external dependencies (live Telegram/Meta
accounts, or a chosen ONNX model) or is an optional follow-up.

Task numbers (#NN) match the running list referenced in `AGENTS.md`.

### Shipped v4.46.0 → v4.53.0 (metrics, accuracy, and command-center depth)
- **Response-speed metrics** (v4.46.0): forward-tracked **First Response Time** (`ResponseTimeTracker`,
  persisted, watch-start excludes pre-tracking backlog), **SLA met %**, answered-today; responsive,
  clickable KPI band (Awaiting→Needs-reply, Busiest/Messages→activity graph).
- **Redesigned account cards** (v4.47.0): live detail chips (reply ~Xm, answered today, N past target,
  urgent, dropped), per-card freshness stamp, longest-wait nudge, plain-language tooltips, skeleton
  loaders, card motion, status legend. Fixed the contradictory "N late" figure (now from the live snapshot).
- **Data-accuracy audit fixes** (v4.48.0): JS now excludes groups/status/broadcast/newsletter from counts,
  keys by LOCAL day (was UTC), and builds a day×hour matrix so the hour-of-day chart honours the date range;
  customer-only volume fixes the "751 msgs/day" inflation.
- **Notifications by account** (v4.49.0): grouped by account (was platform) with avatar + name per section
  and per row. **Activity graph per-account stacked colours** + legend + range total. **Reviews** now list
  which reviews need a reply (reviewer + snippet, best-effort) with click-through.
- **Distinct chart colours** for same-platform accounts (v4.50.1, `ChartPalette`).
- **Weekly business report** (v4.50.0): anomaly detection (response-time degradation, rising backlog,
  neglected accounts), comparative call-outs, SLA/volume trends, per-account table; Save .md / Export .csv.
- **Command-center improvements #1–#7** (v4.51.0–v4.53.0):
  - **#1** awaiting is **current-state, not date-windowed** (a chat waiting since yesterday no longer drops
    out of "Today"); caught-up % stays windowed.
  - **#7** a card's awaiting pill opens the **Needs-reply list scoped to that account**.
  - **#5** per-row **mark-handled / snooze** (`AwaitingOverrideStore`, self-expiring).
  - **#3** **KPI micro-trend sparklines** (`KpiTrendStore`, `MiniSparkline`) under Caught-up % and Awaiting.
  - **#6** **response-time trend chart** in the weekly report.
  - **#2** per-account **L1 drill-down dialog** (`AccountDetailDialog`) before the raw WebView.
  - **#4** **quiet hours** — awaiting toasts muted overnight (`QuietHours` + Settings toggle).

### Shipped v4.42.0 → v4.45.0 (prior work-stream)
- **#32 Google review-health** (v4.42.0): dashboard Reviews section — unanswered + reply rate per account.
- **Tier-2 AI narration suite** (v4.43.0–v4.44.0): #25 shift briefing, #33 anomaly, #34 ranking rationale,
  #36 end-of-day projection, #37 week-over-week. See P2-D below.
- **Activity-graph data fix** (v4.44.2): hour-of-day histogram read straight from the message store on each
  Re-sync (was a stuck per-conversation count); chart kept as **bars** (a v4.44.3 line-chart restyle was
  reverted in v4.44.4 by preference).
- **#26 `IInstanceConnection` — COMPLETE** (v4.44.x–v4.45.0): the full oversight/backfill/review/avatar data
  path now talks to `IInstanceConnection.Current`, not WebView2. See P3-A.
- **P3-C WebView2 RAM instrumentation + stress fixtures — DONE** (v4.45.0). See P3-C.
- **Settings → Accounts change-icon entry point — DONE** (v4.45.0). See Icon-feature follow-ups.
- **Dead drag-reorder code removed** (v4.45.0); **contrast** verified passing (see Minor polish).

### Shipped v4.40.0 → v4.41.2 (prior work-stream)
- **Command-center redesign**: at-a-glance KPI band (caught up · awaiting · messages/day · busiest window),
  redesigned account cards (avatar, status %, full-height status rail, awaiting pill, in-card AI strip),
  info-styled dismissible digest, single-scroll dashboard.
- **Activity-patterns graph**: one filterable chart (hour-of-day / day-of-week / month) with account + range
  filters, peak highlight, insight line — backed by an on-device activity-history log (`MessageAnalyticsService`,
  ~400-day retention). **This closes P3-E #35 (persistence foundation).**
- **Durable oversight snapshot** (`oversight-snapshot.json`): the live dashboard persists + loads on launch
  ("Updated …" stamp); re-sync updates incrementally instead of wiping. Analytics merges (no double-count).
- **Custom account icons** (net-new, not previously on the roadmap): social-media brand logos (bundled
  Font Awesome Brands font) + general icons + **import-from-account profile photo** + **upload image** + reset,
  from the right-click menu; shown in sidebar + dashboard cards.
- **Bug fixes**: names-vanish on add (sidebar row recreation), stray `Ctrl+D` tooltip, Change-icon dialog
  WebView occlusion.

---

## P1 — high value, doable now (no live account) — ✅ ALL DONE

### P1-A · Surface the real business-hours SLA on command-center cards — ✅ done (v4.31.0)
The §8 SLA was computed in `OversightRollupBuilder` (`ThreadData.IsSlaBreached` + per-location
`BusinessHoursCalculator`) but then **discarded** in favour of the unread-based caught-up %. Added
`OversightEntityHealth.SlaBreachedCount` (open in-window threads past their business-hours reply SLA),
computed independently of the caught-up override, and surfaced it as a **"N late"** card sub-metric next
to urgent/dropped. 0 when there's no thread data, so it degrades gracefully.

### P1-B · Decide the OCC's fate — ✅ decided
**Decision: keep the OCC UI dormant (already retired in v4.27.0), harvest its SLA logic into the command
center.** The valuable SLA engine (`BusinessHoursCalculator`, `ThreadData.IsSlaBreached`,
`OperationalThresholds`) lives in shared Models/Services — *not* inside the OCC UI — so P1-A surfaced it
without touching the kanban. The OCC pages stay dormant + reversible (Ctrl+Shift+Q / palette) rather than
deleted, to avoid churn; a later cleanup can remove them once the command center fully replaces them.

### P1-C · Finish WCAG 1.4.1 coverage — ✅ done (v4.30.0)
Status glyph added to compact cards + a warning glyph on Needs-reply rows. Status is never colour-alone.

### P1-D · Sticky-awaiting safety valve — ✅ done (v4.30.0)
Stickiness only inherits while the chat's last activity is within 7 days (`StickyAwaitingMaxAge`); past
that an unconfirmed-clear is allowed through so it can't get permanently stuck. Regression test added.

---

## P2 — needs a live, logged-in account (user unblocks)

### P2-A · Issue 1 — unsaved-contact numbers + message gist — ✅ done (v4.39.10)
Resolved via live IndexedDB inspection (DevTools). `@lid` privacy JIDs → phone now resolve through the
`contact` store's `phoneNumber` field (`buildLidPhoneMap` in `whatsapp-adapter.js`); both C# parse paths
(`WhatsAppBackfillProvider.ProcessIndexedDbConversationsAsync` + `OversightSnapshotReader.ParseChatEntries`)
read `contactPhone`. Message bodies are encrypted at rest, so previews are harvested from the live sidebar
DOM (`__umStartPreviewHarvest`) on the manual Re-sync path, which now reloads each account first so updated
scraper JS takes effect. Known accepted limits: previews only for the ~60 rendered rows, text messages only.
See the **P2-A VERIFIED FACTS** section in `AGENTS.md`.

### P2-B · Channel metric scrapers (Google / Telegram / Messenger / Instagram)
- **#32 Google** — ✅ done (v4.42.0). Dashboard **Reviews** section: `GoogleReviewSnapshotService` scrapes the
  live `business.google.com/reviews` page (navigates there, counts Reply buttons = unanswered, Edit buttons =
  answered) → reviews-awaiting-reply + reply rate per account; `ReviewHealthPanel` UI, on-demand Refresh.
  **Live-DOM-verified limitation:** Google's manager reviews page exposes **no aggregate rating or total
  count** (per-review stars are SVG-only, no aria/text), so rating/total are intentionally not scraped; counts
  reflect the loaded (paginated) page. Selectors rely on EN button text ("Reply"/"Edit") — may need locale/UI
  re-tuning.
- **#24 Telegram** (unread/awaiting), **#24 Messenger + Instagram** (passive read-only; Meta fights automation)
  — ☐ pending. Each must be tuned against a live logged-in account.

### P2-C · Outbound staff-reply tone/quality scoring (Tier-2 Ollama) — **DROPPED**
§6/§8 AI Tier-2: read outbound staff replies and score tone/quality. **Dropped** earlier in this
work-stream in favour of the higher-value Tier-2 AI features in P2-D (shift briefing, anomaly narration,
ranking rationale, projection). Would need a message-content pipeline (metric is counts-only today).
Recorded for completeness; not planned.

### P2-D · Tier-2 AI oversight narration (Ollama, aggregate counts only — never names/text)
Layered on the existing `OversightInsightService` infra (now has a general prompt-based `Request` overload):
- **#25 AI shift briefing** — ✅ done (v4.43.0). One-line whole-business "where to focus first" under the KPI
  band; deterministic heuristic + local-AI swap (`CommandCenterPanel.RenderBriefing`).
- **#33 anomaly narration** — ✅ done (v4.44.0). The briefing flags "busier than usual" when today's projected
  volume runs ≥40% over the recent daily average.
- **#34 ranking rationale** — ✅ done (v4.44.0). The briefing names the account furthest behind + its caught-up %.
  (Per-account granularity; a dedicated cross-*location* rationale card remains optional.)
- **#36 end-of-day projection** — ✅ done (v4.44.0). `MessageAnalyticsService.GetEndOfDayProjection` (today-so-far
  ÷ the share of a normal day usually in by now); surfaced in the briefing ("on pace for ~N today").

All prompts must send aggregate counts only — never customer names or message text (see
`OversightInsightService` contract).

---

## P3 — larger / infrastructure

### P3-A · `IInstanceConnection` abstraction (§10 A-7, #26) — ✅ done (v4.45.0)
`IInstanceConnection` (`ExecuteScriptAsync` + `ReloadAsync`, default `WebViewInstanceConnection` →
`WebViewScriptGateway` + `InstanceSessionManager`) is the data layer's only view of a session. All call
sites migrated: `GoogleReviewSnapshotService`, `AvatarImportService`, `OversightSnapshotReader`,
`WhatsAppBackfillProvider`. The remaining `InstanceSessionManager.Instance` references are the impl
itself, DI registration, and the lifecycle host — not the data path. Tests swap `InstanceConnection.Current`
for a fake (`GoogleReviewScrapeTests`, `OversightSnapshotReaderTests`; serialized in one xUnit collection).

### P3-B · Tier-1 lightweight AI (ONNX Runtime / Windows ML)
§6 Tier-1: small CPU model for better sentiment/classification than the Tier-0 heuristic, tiny RAM.
Runtime integration + model packaging + wiring between Tier-0 and Tier-2.

### P3-C · Post-suspend WebView2 RAM instrumentation — ✅ done (v4.45.0)
`ResourceMonitorService` now samples every `msedgewebview2` process working set (the bulk of real RAM)
alongside the app process; `ResourceSnapshot` exposes `WebView2WorkingSetMegabytes`, `WebView2ProcessCount`,
and `TotalWorkingSetMegabytes`. The Personal-overview memory card shows the honest total (was app-process
only, badly under-reported). CI stress fixtures (`InstanceSessionManagerStressTests`) lock the
eviction/reap policy at scale: strict-LRU across 200 instances, visible-never-evicted, and an exhaustive
`IsReapEligible` matrix. (LRU cap 6, idle reaper, memory tiers were already shipped.)

### P3-D · True L1 channel-aware entity view
§9 L1: clicking an account should open a per-entity view with channel-aware tabs before the live WebView
(L2). Currently it jumps straight to L2. Depends on P2-B scrapers.

### P3-E · Oversight snapshot persistence (Tier-3 foundation, #35 → #37)
- **#35 hourly oversight snapshot persistence** — ✅ done (v4.40.0). Activity-history log
  (`MessageAnalyticsService`, ~400-day daily + hourly buckets) + durable `oversight-snapshot.json`.
- **#37 week-over-week narrative** — ✅ done (v4.43.0). Deterministic this-week-vs-last + busiest-weekday
  line in the Activity patterns panel (`MessageAnalyticsService.GetWeekOverWeek`).

---

## Minor polish (no live account needed)
- ~~Remove dead drag-reorder code~~ — ✅ done (v4.45.0). Removed the never-raised `InstanceReorderRequested`
  event + handler, `ReorderInstanceBeforeAsync`, and the `ShouldAcceptReorder`/`ResolveDropTargetInstanceId`
  drag helpers + their tests. Reorder ships as right-click Move up/down.
- ~~Contrast remediation (teal-on-light AA)~~ — ✅ resolved. The "teal" token (`UmBrandTealColor`) is now
  `#1B75BB`, which computes to ~4.86:1 on white — passes WCAG AA for normal text. `HighContrast.xaml` covers
  high-contrast mode. The old "AA partial" note was stale.
- ☐ Sidebar-rail search/density at very large account counts (Phase 3 leftover). A user-pref-driven compact
  icon rail already exists; auto-overriding it at high counts fights the user's explicit choice and needs a
  visual pass to judge — deferred deliberately, not forgotten.

## Icon-feature follow-ups (from v4.41.x)
- ~~Settings → Accounts Change-icon entry point~~ — ✅ done (v4.45.0). New Settings "Accounts" section lists
  every account with a Change icon… button; shares `AccountIconChangeFlow` with the sidebar right-click path.
- ☐ **Import-from-account robustness** — the canvas→fetch+poll capture (v4.41.2) is best-effort; the
  self-avatar selector may still need live DOM tuning per platform (WhatsApp's own photo is cross-origin
  `pps.whatsapp.net`, so canvas taints; fetch fallback added). Upload is the reliable alternative. Gated on a
  live account.
- ☐ **Brand-logo import for other channels** — Google/Telegram/Messenger selectors are placeholders pending
  live tuning. Gated on live accounts.

---

## Recommended order

Everything doable without an external dependency is shipped (the response-speed metrics, the accuracy
audit fixes, the weekly report, and the full command-center improvement set #1–#7 through v4.53.0). What
remains is **gated** or an **optional follow-up**:

**Gated on external dependencies**
1. **#24 Telegram / Messenger / Instagram scrapers** — needs a live logged-in account per channel to tune the
   DOM queries (Meta read-only; it fights automation). Highest user-facing value once unblocked.
2. **P3-D full multi-channel L1 view** — the per-account drill-down exists for WhatsApp (`AccountDetailDialog`,
   v4.53.0); the channel-tabbed version depends on #24.
3. **P3-B Tier-1 ONNX** — needs a chosen, downloaded model + runtime packaging; can't be built/validated blind.
4. **Icon import-from-account robustness · brand-logo import for other channels** — live per-platform DOM tuning.

**Optional follow-ups**
5. **Business-hours-aware FRT** — ✅ done (v4.55.0). `ComputeOpenLatencyMinutes` in `ThreadRegistryService`
   routes through `BusinessHoursCalculator` when a location's business hours (or Quiet hours) are set, so the
   reply clock pauses outside working hours. Falls back to raw wall-clock otherwise.
6. **AI narration of the weekly-report headline** — ✅ done (v4.55.0). `CommandCenterPanel.OnReportClick`
   awaits a one-sentence Ollama headline (aggregate facts only, 12s timeout, silent fallback), passed to
   `WeeklyReportDialog` as `aiSummary` and shown with a ✦ AI badge. Off unless local AI is enabled.
7. **OS-scheduled report** — intentionally **not** built as an OS scheduled task; replaced by an in-app
   **weekly-report reminder** banner (v4.55.0, `WeeklyReportReminder` + Settings toggle). The app runs
   continuously in the tray, so a persistent Task Scheduler entry would be redundant system config.
8. **Snapshot export as PNG** — ✅ done (v4.55.0). `WeeklyReportDialog` renders the report surface via
   `RenderTargetBitmap` → `PngEncoder`, alongside the existing Save .md / Export .csv.
9. **One-click local backup & restore** — ✅ done (v4.56.0). `LocalBackupService` zips all JSON stores +
   avatars (excludes WebView2 sessions + Ollama models); Settings → Data & Privacy. Restore is zip-slip- and
   signature-guarded and prompts a restart.
10. **Returning-customer / repeat-contact insight** — ☐ pending. New-vs-returning customers per week could be
    built on the existing `ContactPhone` identity + a new first-seen `ContactHistoryStore`; best validated
    against a live logged-in account before shipping (per the project's DOM-data rule).
11. **Dedicated empty-state sweep** across every remaining surface (empty states were folded into touched panels).
12. **Sidebar-rail density at very large counts** — minor; wants a visual pass.
