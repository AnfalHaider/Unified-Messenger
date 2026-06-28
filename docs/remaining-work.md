# Remaining work — prioritized backlog

**As of:** 2026-06-29 · **Baseline:** v4.45.0 · **Source of truth:** [MASTER-PLAN.md](MASTER-PLAN.md)

Everything in the v4.22–v4.24 UI/UX modernization plan and the reported bugs (delete crash, reorder
hang, opened≠replied, Google-Business sidebar, embed channels, Work-Queue→Needs-reply merge, new
channels + UA) is **shipped through v4.28.1**. Phase 1 is complete; P1-A→D and **P2-A (unsaved-contact
phone resolution + message-preview harvest)** all shipped through v4.39.10. What follows is the
substantive roadmap that remains — what's left is gated on external dependencies (live Telegram/Meta
accounts, or a chosen ONNX model).

Task numbers (#NN) match the running list referenced in `CLAUDE.md`.

### Shipped v4.42.0 → v4.45.0 (this work-stream)
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
See the **P2-A VERIFIED FACTS** section in `CLAUDE.md`.

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

Everything doable without an external dependency is now shipped (#26, P3-C, dead-code/contrast polish,
Settings change-icon, the Google + Tier-2 AI suite). What remains is **gated**:

1. **#24 Telegram / Messenger / Instagram scrapers** — needs a live logged-in account per channel to tune the
   DOM queries (Meta read-only; it fights automation). Highest user-facing value once unblocked.
2. **P3-D L1 channel-aware entity view** — depends on #24 (the per-entity tabs would be empty without scrapers).
3. **P3-B Tier-1 ONNX** — needs a chosen, downloaded model + runtime packaging; can't be built/validated blind.
4. **Icon import-from-account robustness · brand-logo import for other channels** — live per-platform DOM tuning.
5. **Sidebar-rail density at very large counts** — minor; wants a visual pass.
