# Remaining work — prioritized backlog

**As of:** 2026-06-27 · **Baseline:** v4.41.2 · **Source of truth:** [MASTER-PLAN.md](MASTER-PLAN.md)

Everything in the v4.22–v4.24 UI/UX modernization plan and the reported bugs (delete crash, reorder
hang, opened≠replied, Google-Business sidebar, embed channels, Work-Queue→Needs-reply merge, new
channels + UA) is **shipped through v4.28.1**. Phase 1 is complete; P1-A→D and **P2-A (unsaved-contact
phone resolution + message-preview harvest)** all shipped through v4.39.10. What follows is the
substantive roadmap that remains — none of it is a quick win.

Task numbers (#NN) match the running list referenced in `CLAUDE.md`.

### Shipped v4.40.0 → v4.41.2 (this work-stream)
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
Net-new AI features layered on the existing `OversightInsightService` infra. None exist in code yet:
- **#25 AI shift briefing** — start-of-shift summary of where each location stands.
- **#33 anomaly-narrated alerts** — explain *why* an alert fired, not just that it did.
- **#34 cross-location ranking rationale** — narrate why one location ranks worse than another.
- **#36 end-of-day projection** — project where the day lands given current pace.

All prompts must send aggregate counts only — never customer names or message text (see
`OversightInsightService` contract).

---

## P3 — larger / infrastructure

### P3-A · `IInstanceConnection` abstraction (§10 A-7, #26)
The oversight/triage/notification layer couples directly to `InstanceSessionManager` + WebView2.
Introduce `IInstanceConnection` so the data layer doesn't depend on WebView2 directly. Cross-cutting,
no user-visible change, high regression surface.

### P3-B · Tier-1 lightweight AI (ONNX Runtime / Windows ML)
§6 Tier-1: small CPU model for better sentiment/classification than the Tier-0 heuristic, tiny RAM.
Runtime integration + model packaging + wiring between Tier-0 and Tier-2.

### P3-C · Post-suspend WebView2 RAM instrumentation
§10 RED partially closed (LRU cap 6, idle reaper, memory tiers shipped). Remaining: measure post-suspend
RAM at many-instance scale + CI stress fixtures to confirm the memory strategy holds.

### P3-D · True L1 channel-aware entity view
§9 L1: clicking an account should open a per-entity view with channel-aware tabs before the live WebView
(L2). Currently it jumps straight to L2. Depends on P2-B scrapers.

### P3-E · Oversight snapshot persistence (Tier-3 foundation, #35 → #37)
- **#35 hourly oversight snapshot persistence** — ✅ done (v4.40.0). Activity-history log
  (`MessageAnalyticsService`, ~400-day daily + hourly buckets) + durable `oversight-snapshot.json`.
- **#37 week-over-week narrative** — ☐ pending, **now unblocked** by the activity history. Narrate
  trend changes (this week vs last) from the accrued buckets.

---

## Minor polish (no live account needed)
- Remove dead drag-reorder code (replaced by right-click Move up/down).
- Sidebar-rail search/density at very large account counts (Phase 3 leftover).
- Remaining contrast remediation (teal-on-light AA partial).

## Icon-feature follow-ups (from v4.41.x)
- **Import-from-account robustness** — the canvas→fetch+poll capture (v4.41.2) is best-effort; the self-avatar
  selector may still need live DOM tuning per platform (WhatsApp's own photo is cross-origin `pps.whatsapp.net`,
  so canvas taints; fetch fallback added). Upload is the reliable alternative.
- **Settings → Accounts Change-icon entry point** — held; Settings has no active-accounts list, so it needs a
  new accounts-management surface (right-click entry already ships).
- **Brand-logo import for other channels** — Google/Telegram/Messenger selectors are placeholders pending live
  tuning.

---

## Recommended order
1. **#32 Google review-health card** — top user-facing value, user is unblocked (has a live account).
2. **#37 week-over-week narrative** — now unblocked by the v4.40 activity history; reads accrued buckets.
3. **P2-D Tier-2 AI features (#25 / #33 / #34 / #36)** — highest-leverage net-new work; infra already exists.
4. **#24 Telegram / Messenger / Instagram scrapers** — as each channel/account becomes available.
5. **P3-A (#26) + P3-B / P3-C / P3-D** — infra, when the product surface is settled.
