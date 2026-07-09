# Unified Messenger — Master Plan (single source of truth)

**Status:** Authoritative. Supersedes the planning/research docs listed in §16.
**Baseline build:** v4.6.0 · **Last updated:** 2026-06-16
**One-line objective:** a **free, fully-local Windows app that gives a business owner digestible oversight of customer conversations across their locations and channels** — without reading every chat.

---

## 1. What it is (and isn't)

**Is:** a Windows (WinUI 3) **oversight workspace**. You install every location's customer channels on **your** machine, grouped (optionally) into per-location workspaces. The app **passively scrapes** those conversations and turns them into a **location-scoped dashboard** — who's replying on time, what's urgent, which customers are being dropped, review health — with **optional on-device AI** for assessments. WhatsApp first; other channels later.

**Isn't:** a bulk sender, an API client, a cloud service, or a team inbox with roles. No automation, no recurring cost, no data leaves the machine.

**Primary user & job:** a multi-location business owner/manager who today has *no unified visibility* — "I don't know if staff are replying on time, in the right tone, or dropping customers." The app answers "**what's happening where**" at a glance.

---

## 2. Decisions of record

| # | Decision | Date |
|---|---|---|
| D-1 | **No Meta/WhatsApp APIs and no cloud** — zero recurring cost, nothing leaves the machine | 2026-06-16 |
| D-2 | **Unofficial protocol libraries (Baileys/whatsmeow/mautrix) are rejected** — research shows they carry *higher* ban risk than the real WhatsApp Web; the no-ban-risk rule selects **WebView2 + real web clients** | 2026-06-16 |
| D-3 | **Read-only automation stance** — the app never auto-sends; the human may reply fully through the embedded web client | 2026-06-16 |
| D-4 | **Locations are an optional grouping layer** — works per-account with no locations defined | 2026-06-16 |
| D-5 | **Two scopes:** Personal (own accounts) and Professional (business), kept separate | 2026-06-16 |
| D-6 | **AI is an optional, tiered, free, on-device download** — dashboard degrades to analytics-only without it | 2026-06-16 |
| D-7 | **Per-platform isolation** — each channel is its own integration module; **refine WhatsApp first** | 2026-06-16 |
| D-8 | **No roles/permissions; no cross-machine sync** — anyone with access to an instance sees the same scoped data | 2026-06-16 |
| D-9 | MVVM + presenters · refresh coordinator · CI-built releases · interface composition root | ADR 001–004 |
| D-10 | SLA excludes backfilled/historical threads; adds at-risk window | v4.5.0 |

---

## 3. The model: scopes, instances, locations

- **Two scopes.** **Personal** = the user's own accounts; simple activity view, no SLA/locations, private. **Professional** = business oversight (everything below).
- **Instances always exist; locations are optional.** An *instance* is one connected account/channel. A *location (workspace)* is an optional grouping of instances. With no locations, the dashboard is **per-instance**; define locations and the same cards **roll up by location**.
- **Two instance types:**
  1. **Integrated channel** — platform-specific scraper feeds the dashboard (WhatsApp now; Google reviews, Telegram, Meta later).
  2. **Generic URL webview** — embed any web app for convenience (Rambox-style); **contributes no dashboard data**.

---

## 4. Channels & isolation (D-7)

| Channel | Status | Data it contributes | Notes |
|---|---|---|---|
| **WhatsApp / WhatsApp Business** | **Now — refine first** | Conversation metrics: on-time %, urgent, dropped, tone, work queue | Real `web.whatsapp.com` in WebView2 |
| **Google Business reviews** | Planned | Review metrics: new reviews, avg rating, % responded, unanswered | Embed Google Business Profile **web** UI; scrape reviews; reply from web. *Chat is dead (shut down 2024); reviews only.* |
| **Telegram** | Later | Conversation metrics | Same per-profile web model |
| **Instagram / Messenger** | Later / optional | Conversation metrics | Meta actively fights automation — higher maintenance/ban risk; passive read only |
| **Generic URL** | Anytime | None | Convenience embed only |

Each integration is **isolated** (own adapter module, own scraper, own metric widgets). The dashboard is **channel-aware**: WhatsApp and Google contribute *different* metric modules into the same location view.

---

## 5. Automation stance (D-3)

- **The app never automates sending** — no bulk, no auto-replies, no scripted actions. This is what keeps ban risk low (real first-party web client + human-paced manual use).
- **The human has full manual use** of the embedded web client (reply in WhatsApp Web, reply to a Google review) whenever they want.
- "Read-only" describes the *app's* behaviour (passive scraping for analytics), not a restriction on the user.

---

## 6. AI: optional, tiered, free, on-device (D-6)

The dashboard works fully **without** AI; AI is additive.

| Tier | Tech (free, local) | Runs on | Adds |
|---|---|---|---|
| **0 — none** | existing heuristic engine (keyword urgency/sentiment) | any PC, no download | urgency/sentiment + all analytics & charts |
| **1 — lightweight** | ONNX Runtime / Windows ML small models (CPU) | almost any PC, small download | better sentiment/classification, tiny RAM |
| **2 — local LLM** | Ollama (integrated) — Llama 3.2 3B, Phi-4-mini, Gemma/Qwen | capable PCs, larger download | summaries, **assessments, recommendations, tone** |

UI shows mode honestly: no model → analytics only; model present → **additive "AI insight" strips** on the same cards (never a separate screen). Tone/quality is a Tier-2 feature (reads outbound staff replies).

---

## 7. Data & history model

- **History comes from scraping, not uptime.** The scraper backfills message history so analytics **survive the app being closed**. Charts always render the **week**; a **date filter** views past days. Current view = live + scraped.
- **Freshness is explicit.** Each instance shows "synced Xm ago" or **"⚠ stale — reconnect"**. Closing the app makes data stale — the UI must say so, never show stale numbers as current.
- **Determinism:** the same account scraped on any machine yields the same daily/weekly aggregates (source messages are identical) — *provided backfill is comprehensive* (see Risk R-1).

---

## 8. SLA model

- **Business-hours-aware, per-location.** A 2am message isn't "late" by 9am; the SLA clock respects each location's working hours and **pauses outside them** (standard support-SLA pattern). Threshold + hours are **per-location, configured in Settings → Workspace Management**, with sensible defaults so it works out-of-the-box.
- **"On-time" keys off reply *timing and direction*** — customer inbound timestamp → next business outbound timestamp in that chat — **not** customer read-receipts (blue ticks are unreliable; the customer can disable them). This is scrape-able from the chat DOM and is the right signal for oversight.
- Metrics that matter (in priority): **on-time %, urgent, dropped/unanswered**, then volume, then **tone** (AI tier-2).

---

## 9. UI/UX design (validated via interactive previews)

**Shell:** left **workspace rail** (entities, worst-first) · main content · top bar with **scope toggle** (Personal/Professional) and **AI toggle**.

**Three-level drill-down:**
- **L0 · Command center** — one **health card per entity** (account, or location when grouped): on-time %, urgent, dropped, reviews, freshness dot, sparkline, optional AI strip. A **needs-attention banner** (cross-entity: "N customers need a reply now — most urgent at X · Jump"). **Worst-first ordering.** **Group-by none ↔ location** switch; a **"Define locations"** prompt when none.
- **L1 · Entity view** — **channel-aware tabs** (WhatsApp metrics + work queue; Google reviews metrics + review list). 
- **L2 · Live view** — the real WhatsApp Web (full manual reply) with a **"never auto-sends"** banner; or a channel detail.

**The six required improvements (all previewed):**
1. **Guided cold-start** — add account → optionally group into locations → set hours/SLA → optionally install AI (each skippable).
2. **Time range** — Today / This week / pick-a-date; charts always weekly.
3. **Proactive alerts** — desktop notification when a customer waits > X or a location drops below Y%.
4. **"Since you last opened" digest** — what changed since the app was last open.
5. **Scale handling** — search/filter + **cards ↔ list density** toggle for many accounts.
6. **Per-channel modules** — different metric widgets per channel feeding one location view.

**Principles:** status-first and glanceable; **colour + icon + text** (never colour alone, WCAG 1.4.1); progressive disclosure (glance L0 → drill only where needed); the dashboard degrades gracefully without AI and without locations.

---

## 10. Architecture & technical reality

- **Stay on WebView2 + real web clients.** It is simultaneously the **lowest ban-risk** (first-party client, no protocol impersonation), the **only multi-channel-extensible** option (Rambox model; protocol bridges are WhatsApp-specific), and — via the **shared `CoreWebView2Environment` + per-profile** model — **memory-efficient** (shared browser/GPU/network process collection; only per-tab renderer is additive). Earlier "30+ live is impossible" was based on antidetect browsers (separate Chromium each) and **does not apply** here.
- **Instance lifecycle for oversight:** keep background instances **connected but memory-light** (`MemoryUsageTargetLevel.Low`) so data stays current; **close only as last resort** (closing stops messages → stale). The cap/eviction must prefer suspend-low over close and mark closed entities **stale**.
- **Connection abstraction (A-7):** introduce `IInstanceConnection` so the OCC/triage/notification layer doesn't depend on WebView2 directly — keeps channels isolated and future-proofs the data layer.

**Current state (reconciled, v4.6.0):** ingress coalescing ✅ (v4.2) · list/kanban virtualization ◑ (ItemsRepeater; verify) · DOM observers ◑ (rAF coalescing; verify ≤1 scoped MO) · WebView2 memory 🔴 (post-suspend RAM unmeasured; **default cap = 0 = unbounded**; `TrySuspend`/`MemoryUsageTargetLevel` API-mixing; orphan `NavigationCompleted` handler) · SLA integrity ✅ (v4.5) · OCC P1 UX ✅ (v4.6) · contrast ◑.

---

## 11. Constraints (hard)

Local-only · zero ongoing cost · no cloud · no Meta/WhatsApp APIs · no protocol libraries (ban risk) · no cross-machine sync · no roles/permissions · all AI on-device · customer data never leaves the machine.

---

## 12. Build roadmap

**Phase 1 — WhatsApp oversight foundation (refine first).**
Shell with workspace rail + scope toggle · L0 command center (per-account, needs-attention, worst-first, freshness, sparklines) · L1 WhatsApp metrics + work queue · L2 live view · **Settings → Workspace Management** (define locations, assign instances, business hours + SLA) · analytics baseline (no-AI, Tier-0 heuristic) · robust WhatsApp history backfill · instance lifecycle (connected-but-light, stale marking, sane default cap).

**Phase 2 — AI tiers.** Optional download (Tier-1 ONNX, Tier-2 Ollama) · additive insight strips · tone/quality.

**Phase 3 — Oversight depth & scale.** "Since you last opened" digest · proactive alerts/toasts · time-range + date filter · search + list-density · generic-URL instances.

**Phase 4 — Google Business reviews channel** (embed + scrape + reply; review metrics module).

**Phase 5 — Additional channels** (Telegram, then Meta with risk caveats), each isolated.

**Cross-cutting (any phase):** the v4.6.0 perf RED items (WebView2 memory strategy + orphan-handler fix + default cap), `IInstanceConnection` abstraction, contrast remediation, CI asset guard (done), stress fixtures.

---

## 13. Definition of done (per the objective)

- A new user with **no locations** still gets a useful per-account command center; defining locations rolls it up with nothing to relearn.
- The owner sees **"what's happening where" in seconds** — needs-attention first, worst-first, freshness honest.
- Works **fully without AI**; with a model, adds assessments — no nagging.
- **History survives restarts**; charts weekly; dates filterable; stale data clearly marked.
- WhatsApp is the only fully-built channel; the channel model is **isolated and extensible** to Google reviews / Telegram / Meta.
- Bounded, configurable memory at the owner's many-instance scale; all swarm vectors green with instrumentation.
- Zero recurring cost; nothing leaves the machine.

---

## 14. Open questions / risks

- **R-1 Backfill completeness:** WhatsApp Web lazy-loads chats/messages; deep per-conversation history is bounded. Aggregate daily counts reconstruct well; accurate historical per-conversation SLA for old dates needs more robust backfill — **the foundational feasibility item** (validated further in §17).
- **R-2 Tone on-device:** quality is hard for tiny models; Tier-2 LLM + careful prompting required; not guaranteed on low-end machines (hence optional).
- **R-3 Google reviews scraper:** Google login/2FA inside a WebView + periodic UI changes = ongoing maintenance.
- **R-4 Channel maintenance:** scraping any web client breaks when the site changes; per-channel isolation contains the blast radius.
- **R-5 Owner-machine scale:** many locations × channels live on one machine — memory strategy (§10) must hold.

---

## 15. Glossary
Instance = one connected account/channel · Workspace/Location = optional grouping of instances · Integrated channel = scraper-backed (feeds dashboard) · Generic URL = embed only · Scope = Personal vs Professional · Tier 0/1/2 = no-AI / ONNX / local-LLM.

## 16. Superseded & folded-in documents
`docs/ui-ux-research-and-recommendations.md` · `docs/multi-instance-research-and-limitations.md` · `docs/validation/{completion-criteria,v2-reaudit-checklist,wave11-checklist,remaining-issues-log}.md`. Living reference: `docs/architecture/*`, `docs/design-system/*`, `README.md`, `.crawl4ai/occ-research/**`.

## 17. Research basis
WhatsApp 4-device cap & Companion Mode · WhatsApp-Web RAM at scale & shared-environment WebView2 process model · protocol-library ban risk (Baileys/whatsmeow) vs real web client · Beeper/Matrix bridge architecture · Rambox/Ferdium webview-per-service model · Google Business Messages shutdown (2024) · empty-state / SLA-dashboard / info-density UX. Full citations in the superseded research docs (§16).

### Implementation-feasibility pass (2026-06-16) — all green, with caveats
- **WhatsApp on-time/dropped is feasible from message timestamps + direction**; do **not** depend on customer read-receipts (unreliable when disabled). Backfill depth bounded by WhatsApp Web lazy-loading (R-1). ([getkanal — read receipts](https://getkanal.com/blog/whatsapp-read-receipts-blue-ticks-guide), [whatsapp-web.js #67](https://github.com/pedroslopez/whatsapp-web.js/issues/67))
- **Business-hours SLA pause is a standard, well-defined pattern** (timer counts only in working hours; optionally pauses awaiting customer). ([Intercom — SLAs & office hours](https://www.intercom.com/help/en/articles/9263617-slas-and-office-hours), [LiveAgent](https://support.liveagent.com/804145-Understanding-SLA-Time-Calculations))
- **On-device tone/sentiment is realistic at Tier-2** — Phi-4-mini / Llama 3.2 3B run 15–25 tok/s CPU-only and handle sentiment/classification well; a 3B model on support text can rival far larger ones. Confirms tiered-AI plan. ([MachineLearningMastery — SLMs on a laptop](https://machinelearningmastery.com/top-7-small-language-models-you-can-run-on-a-laptop/), [TinyWeights](https://tinyweights.dev/posts/best-small-language-models-2026/))
- **Google reviews: web-scrape path is viable** (Business Profile web dashboard reads/replies per location). The clean Business Profile **API exists but is excluded by D-1** (cloud/cost); embed + scrape + reply-from-web instead; expect login/2FA + UI-change maintenance (R-3). ([Google review management](https://support.google.com/business/answer/3474050), [GBP reviews API (for reference only)](https://developers.google.com/my-business/reference/rest/v4/accounts.locations.reviews/list))
- **Proactive desktop toasts are feasible on the unpackaged WinUI 3 app** via `ToastNotificationManagerCompat` (works fully unpackaged); infra already exists (`AppNotificationService`). ([MS Learn — app notifications quickstart](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/notifications/app-notifications/app-notifications-quickstart))
