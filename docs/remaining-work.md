# Remaining work — prioritized backlog

**As of:** 2026-08-22 · **Baseline:** v4.99.43 · **Source of truth:** [MASTER-PLAN.md](MASTER-PLAN.md)

> **Read §0 first.** Everything below §0 was written against **v4.56.0** and is a *historical* record of a
> completed work-stream. It was not maintained through v4.99.x, and several items it lists as pending have
> since shipped. §0 is the current backlog; `CHANGELOG.md` is the accurate record of what shipped.

---

# §0 · Current backlog (v4.99.43)

Grouped by what actually gates each item. Nothing here is speculative — every measurement below was taken
from the tree at this baseline, and every finding ID is traceable to `docs/audit/`.

## 0.1 · UI/UX — mostly landed; three items left

**No longer the largest open body of work.** v4.99.35 unified the type, icon and spacing scales, and
v4.99.36 gave the app its own ground and accent — see [scales.md](../design-system/scales.md) and the
CHANGELOG. What is left below is accessibility and audit coverage, not visual consistency.

> **The two headline "dated" problems were not design choices at all.** The warm background tint was the
> owner's **desktop wallpaper** showing through MicaBackdrop into a content area with no background of its
> own, and the brick red on every toggle was the **Windows personalization accent**. Both varied per
> machine and both collided with the meanings this app assigns to colour. The fix was to make the app own
> its surfaces and its accent, not to pick nicer colours. Worth remembering before diagnosing the next
> "the app looks wrong" report: check what is inherited from Windows first.

| # | Item | Measured at this baseline | Status |
|---|---|---|---|
| **U1** | **Spacing on a 4px grid** | **Done (v4.99.35).** Was 33 distinct `Padding` values (4 token / 29 literal) across 62 uses, plus 19 `Margin` literals. Now 21 distinct (9 token / 12 literal), every literal on-grid and asymmetric by intent. Pinned by `EverySpacingValueSitsOnTheFourPixelGrid`. | ✅ done |
| **U2** | **Seven-step type ramp, four-step icon scale** | **Done (v4.99.35).** Was 12 distinct text sizes and 12 icon sizes; the original "16" figure counted `obj/` build copies and merged the two scales — corrected in [scales.md](../design-system/scales.md). Now `11·12·14·16·20·24·32` text and `12·16·24·40` icons, with **zero literal font sizes** in either XAML or C#. Nothing below 11px. | ✅ done |
| **U3** | **Corner radius** | **Fixed and guarded.** Down from 6 ad-hoc values to the 6/8/12 scale, pinned by `DesignScaleTests.EveryCornerRadiusComesFromTheScale`. | ✅ done |
| **U4** | **No hardcoded colours in XAML** | Pinned by `DesignScaleTests.NoColourIsHardcodedInXaml`. | ✅ done |
| **U5** | **Icon-only controls are named** | Pinned by `DesignScaleTests.EveryIconOnlyControlIsNamedForAScreenReader`. | ✅ done |
| **U6** | **`SystemFillColor*` brushes were never contrast-measured** | They are used **more** than the app's own tokens (36/33/29 references vs 12/12/11), so the majority of status colour on screen is unaudited. Sharper since v4.99.36: the app now pins its own accent, so these are the last surfaces still taking their colour from Windows. | ☐ open |
| **U7** | **Imperative card builders draw from the shared scale** | **Done (v4.99.35).** `Services/UmScale.cs` holds the ramp as constants (not a resource lookup — that would be a UI-thread WinRT call, the mistake that already cost one process-terminating crash). 116 literals across 16 files converted. `TheCodeScaleMatchesTheXamlTokens` pins the C# copy against `Tokens.xaml`, and `NoLiteralFontSizeInCode` is the guard that never existed. | ✅ done |
| **U8** | **Dedicated empty-state sweep** | Partly done — every empty state *touched* during the audit was fixed, and v4.99.34 corrected the three account-list surfaces. The remaining surfaces (Analytics, Reviews, Reports, Notification Hub) have never been reviewed as a set. | ◑ partial |
| **U9** | **Focus order outside the dashboard shell** | Settings, Analytics, Reviews, Reports and every dialog are untested. Shift+Tab and modal focus containment specifically. | ☐ open |
| **U10** | **"Instance" leaks into accessible names** (F-ORCH-06) | Settings and the account context menu speak *"instance"* to a screen reader where the visible label says *"account"*. | ☐ open |
| **U11** | **Sidebar rows could not be activated by assistive tech** | **Done (v4.99.44).** Every navigable row — Dashboard, Analytics, Reviews, Reports and every account — was a plain `Border`, which exposes no automation pattern: they announced as `Group` and offered nothing to invoke. Found while driving the app through UI Automation, where the only way into the Reviews page was to compute its rectangle and click by screen coordinates. `NavigationRow` (a `ContentControl`, because `Border` is sealed) now carries a peer reporting control type **Button** with `IInvokeProvider`. Verified live: all rows report Button + Invoke, and invoking Reviews opens the page. | ✅ done |

**What guards this now.** `DesignScaleTests` reads **both `.xaml` and `.cs`** — a new off-scale font size,
icon size or padding fails the build. That is what stops U1/U2/U7 re-drifting, and it is the reason those
three had to ship together rather than in sequence.

## 0.2 · Data accuracy — what the numbers still get wrong

The brief's hardest line is **no wrong numbers**, and this is where the remaining ones are.

| # | Item | Detail | Status |
|---|---|---|---|
| **D1** | **Call outcomes** | **Done (v4.99.37).** Every call-log entry was counted as a missed call needing a call back, regardless of outcome or direction. Reading WhatsApp's own `callOutcome` live: of 317 inbound calls only 166 were actually missed. Missed calls 86 → 36, total waiting 258 → 206. | ✅ done |
| **D2** | **The IndexedDB fallback cannot read `callOutcome`** | It has no decrypted message model, so it emits an empty outcome and every call stays counted. Correct by design — unknown never closes a chat — but it means an account running on the fallback still over-counts missed calls. Surfacing *which* path an account is on is F-SNAP-02. | ☐ open |
| **D3** | **"Uncategorised" was the largest queue bucket** | **Done (v4.99.38).** Read through: not unclassifiable, but Roman Urdu enquiries, bare names sent to book, attachments and sign-offs with typos. Customers chasing an unanswered message now count as at-risk. Fell from roughly two in five waiting conversations to about one in five. | ✅ done |
| **D4** | **`instances.json.bak` (489 bytes, dated 2026-08-12)** | A stale seeded-default store sitting beside the real one. Harmless, but it is exactly the kind of file that misleads the next person diagnosing "my accounts are gone". | ☐ open |
| **D5** | **Google reviews are read 50 at a time, out of 1,671** | Pagination is off (`MaxPages = 1`) after walking every page produced 2,000 reviews for a 991-review profile and 1,200 for a 435 — two to three times over. The desk states its own coverage ("covers the 150 most recent of 1,671") rather than implying completeness, so this is honest but partial. Re-enable only on proof of identical totals across two consecutive passes. | ☐ open |
| **D6** | **Median reply time is not obtainable** | Google publishes no reply dates anywhere the scrape can reach. The tile says so rather than showing a number. The alternative — measuring from when *we* first saw a review unanswered, labelled "since install" — is an owner decision (§0.6), not an engineering one. | ☐ open |
| **D7** | **Per-review stars were wrong for the entire life of the feature** | **Done (v4.99.41).** Every review reported 5 stars: all five glyphs are the same codepoint and the rating is carried in their *colour*. Five unanswered one-stars and a two-star at DHA-2 were displayed as "★5 · Positive" and ranked below praise. Now reads the leading run of colour. | ✅ done |

## 0.2b · Review Desk — all five tiers shipped (v4.99.40 → v4.99.43)

The Reviews page was rebuilt from the approved design in
[review-desk-spec.md](../design/review-desk-spec.md). `ReviewHealthPanel` no longer appears on it; the desk
absorbed everything that panel did. What follows is the state of each tier and what is genuinely left.

| Tier | What shipped | Left |
|---|---|---|
| **0 · Trust** | Coverage stated everywhere ("covers the 150 most recent of 1,671", "of the 50 most recent"), never "all". Weighted business-wide rating, labelled as a weighted mean because Google publishes one per location and never one for the business. | D5 — the 50-per-page limit itself |
| **1 · Queue** | One worst-first queue across every location; severity by star rating with unread ratings ranked below known complaints and above known praise; ↑↓/J/K/Home/End with focus as the selection so a screen reader hears it; critical strip for any one- or two-star. | Mark handled / snooze for reviews (no equivalent of `AwaitingOverrideStore` yet) |
| **2 · History** | `ReviewHistoryStore` — one reading per account per day, surviving restarts. Velocity derived from the lifetime total's movement rather than per-review dates the scrape never sees. Every figure carries the span it was actually measured over. | Needs a second day of readings before the trend tiles say anything; D6 |
| **3 · Local AI** | Reply drafting for reviews that have words, with guardrails that refuse refunds, discounts, free treatments, invented links and unfilled templates. Themes computed deterministically. | Nothing outstanding — but see the lesson below |
| **4 · Ask for a review** | Candidate selection, the once-ever store, and the WhatsApp hand-off. | Never seen with a live qualifying customer — see §0.4 |
| **5 · Native** | Unanswered-review badges on the sidebar, an unhappy-review toast that stays silent on install day, honest badge wording for a channel with no messages. | — |

**The lesson from tier 3, recorded because it cost a user-visible bug.** The themes line was routed through
the local model to "rephrase more naturally", on the reasoning that a model handed a finished sentence — no
reviews, no arithmetic — had nothing left to be wrong about. That reasoning was wrong. With `phi3:mini`
installed it rendered the correct sentence *"Two of the 3 waiting reviews with text mention good results,
all at Google Depilex DHA-2"* as *"Two positive **waiter** experiences were mentioned in the last three
Google reviews about our **product, Depladuril HA-2**, praising its **effectiveness**"* — misreading
"waiting" as "waiter", inventing a product name, and inventing what the reviews praised. Rewriting is
generating. **Where the app can compute something correct, do not route it through a model afterwards.** The
AI surface is now confined to the one place with something genuinely to write: a reply to a review that has
actual words in it.

## 0.3 · Open audit findings

| ID | Sev | What | Why still open |
|---|---|---|---|
| **F-SNAP-02** | S2 | A *degraded* read (store bridge failed, IndexedDB succeeded) is visible only in `app.log` | Needs a surfaced health state, not just a log line |
| **F-OFFLINE-07** | S3 | An **aborted** navigation puts an account into `Error` with no retry scheduled | Deliberately left: it changes *when* accounts enter the error state |
| **F-OFFLINE-08** | S3 | The dashboard tells an offline owner to "click Re-sync", which cannot work until the connection returns | Small — needs the same connection-status join `ScanBlockedMessage` already got |
| **F-ORCH-06** | S3 | "Instance" as an accessible name (see U10) | — |
| **F-METRICS-11** | S4 | End-of-day projection skew | **WONTFIX by decision** — bound measured at under 2% |

## 0.4 · Untested and material

Not defects — gaps in what has actually been *proven*. Listed because the brief is "sellable tomorrow",
and these are the distance between that claim and evidence.

- **The "ask for a review" panel has never been seen with a live candidate.** Replaying the owner's real
  chat snapshot through the selector: 42 chats, 33 awaiting a reply, 1 where the salon spoke last, and the
  7 that reach the gratitude check have **no preview text** to judge on — previews fill in from the store
  bridge about a minute after an account loads. The rules are covered by test and by that replay; the panel
  correctly renders nothing today, which is indistinguishable from a panel that would never render anything.
- **The unhappy-review toast has never actually fired.** Its *silence* is verified — a cleared store plus a
  full pass recorded two existing two-star reviews and raised nothing, which is the install-day rule. The
  firing path needs a genuinely new one- or two-star review to arrive.
- **The nightly UI smoke workflow is still red**, and has been for many runs. FlaUI cannot attach to a
  desktop on a hosted GitHub runner. Increment 45 made it report *which* failure it is (a Win32 probe
  distinguishes "the app never opened a window" from "this environment cannot automate one") and stopped it
  discarding the structural audit and unit results on the way out — but the underlying "hosted runners have
  no interactive desktop" question is unresolved. The `Build` workflow, which gates releases, is green.
- **A real screen reader has never been run.** Both audit sessions read the UIA tree those tools consume,
  in focus order — much closer than a static dump, but nobody has listened to it.
- **Soak under account churn.** The 3.6-hour soak found no leak, but it was **idle**. A leak that only
  appears when accounts cycle, re-sync or navigate would not have shown.
- **The ~3.1 GB WebView2 footprint** — stable, but eight times the app's own, and unaddressed.
- **A network drop while pages are already loaded** — the commoner real case. No `NavigationCompleted`
  fires, so the retry does not cover it, and the app may keep reporting "Connected" while the web client
  is offline.
- **The updater's own network path** against a real outage (the dead-proxy technique reaches only WebView2).
- **Five of twelve dialogs have never been opened** — SetLocation, EditInstanceMetadata, PinToTaskbar,
  AutoUpdate, ConfirmPermanentDelete (the last deliberately, being step two of permanent deletion on a
  machine holding real accounts).
- **The all-caught-up hero has never been rendered**, only reasoned about. It needs zero awaiting across
  every account, which cannot be staged without overwriting live data.
- **ARM64 is published but never installed.** Every release since v4.99.28 ships an ARM64 installer with a
  verified checksum; no one has run it on ARM hardware.
- **The uninstall data-erasure option** (v4.99.14) is unverified at runtime — confirming it would have
  destroyed the owner's data.
- **Nothing has been tested against the real store from a normal launch, historically.** Almost every
  session drove the app from inside an MSIX container, which silently redirected its `%LOCALAPPDATA%`
  writes. Resolved on 2026-08-19 by migrating 9.5 GB back to the real store, but it means older
  measurements in this document were taken against the container copy, not the store an owner's install
  actually reads. See the two AGENTS.md gotchas — the second one (identical bytes AND identical hashes from
  both paths) is what made it invisible for so long.

## 0.5 · Gated on an external dependency

Unchanged in substance; still the only items that cannot be built on this machine today.

1. **#24 Telegram / Messenger / Instagram DOM scrapers** — needs a live logged-in account per channel.
   Highest user-facing value once unblocked. Meta is read-only and fights automation.
2. **P3-D multi-channel L1 view** — the WhatsApp per-account drill-down ships (`AccountDetailDialog`,
   v4.53.0); the channel-tabbed version depends on #24.
3. **P3-B Tier-1 ONNX** — needs a chosen, downloaded model plus runtime packaging. Cannot be validated blind.
4. **Icon import-from-account robustness · brand-logo import for other channels** — live per-platform DOM tuning.
5. **Code-signing the installer** — needs a certificate. Closes F-OFFLINE-01 properly.

## 0.6 · Decisions only the owner can make

Recorded rather than guessed, because each changes what the numbers *mean*.

- **The SLA threshold is 15 minutes; the measured median reply time is hours, not minutes.** Every account
  therefore reads as failing, and the dashboard currently shows **SLA met 0%** — now the most alarming
  figure on the screen and the one least connected to reality. Either the target reflects the business (and
  the dashboard should say how far off it is), or it should move. A business decision, not a bug, and the
  single highest-value thing on this list that costs no engineering time.
- **Whether "median reply time" should measure from installation.** Google publishes no reply dates, so the
  tile is blank and says why. It *could* measure from when the app first saw a review unanswered — honest,
  but covering only replies made after install, and it must be labelled that way. Worth having, or drop the
  tile? A judgement about what the owner wants to see, not a technical limit.
- **Whether the backlog cutoff stays at 7 days.** The live/backlog split at 7 days is what turned 466
  "waiting" into a workable 58-item queue; the boundary itself was chosen, not derived.
- **Whether `main`'s "Audit Files" commit (`954145e`, ~1,400 graphify cache files) should be dropped.**
  Repository housekeeping with a rewrite cost.

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
