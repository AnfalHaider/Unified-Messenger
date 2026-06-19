# Build status — Phases 1–5 (done / left)

**Date:** 2026-06-19 · **Baseline:** v4.16.0 · **Source of truth:** [MASTER-PLAN.md](MASTER-PLAN.md)
**Legend:** ✅ done (works; may need adapting to new IA) · ◑ partial (exists in primitive form) · ☐ not started (net-new)

> **Session 4 update (v4.14.0 → v4.16.0): command-center polish + insight strips + instance-lifecycle memory hardening.**
> - **v4.14.0 (track A):** command-center visual polish — each account is a proper card (rounded border, surface background, status-colored accent bar), a large prominent caught-up %, clearer awaiting labels ("needs reply" for read-but-unanswered).
> - **v4.15.0 (track B):** per-account **insight strips** — a one-line "Needs attention — N customers waiting · M unread · oldest X ago" summary (amber when mostly caught up, red when behind; quiet accounts show no strip). Deliberately a **deterministic on-device heuristic** (instant, zero-cost, no cloud/API/AI-runtime dependency); optional local-AI enhancement can layer on later.
> - **v4.16.0 (track C):** **idle-session reaper** — the LRU cap only fired on new-session creation, so briefly-visited accounts stayed live and held RAM. A 1-minute timer now closes non-visible sessions idle past `IdleSessionReapMinutes` (default 20, 0=off). **Professional accounts are exempt** (background oversight keeps reading them); the visible account is never reaped; closing preserves the on-disk profile (reloads still signed in). Layers on the existing LRU cap, memory tiers, Low background memory target, lazy/visible-only warm, and 90s `AdapterHealthMonitor` stale-recovery.

> **Session 3 update (v4.9.0 → v4.13.0): Phase 1 is effectively complete; Phase 3 oversight features added.**
> - **Shell IA done (v4.10.0–v4.13.0):** sidebar groups by Professional/Personal scope with a **scope switch**; professional accounts form a **location rail** (right-click → "Set location…"); the **command center is the L0 home** (full-width, Personal Overview in an Expander); the OCC became a dedicated **Work Queue (L1)** page (sidebar button, Ctrl+Shift+Q); Workspace Management is discoverable in **Settings**; a **4-step guided onboarding** wizard runs on first launch.
> - **Phase 3 (v4.9.0–v4.9.2):** proactive **threshold alerts** (configurable, edge-triggered) + **"since you were last here" digest** + **custom From/To date range**.
> - **Known constraints/decisions:** sidebar **drag-reorder removed** (froze the app — WinUI drag-in-ScrollViewer); reorder is via right-click **Move up/down**. Awaiting-list **message preview** is DOM-scraped (only covers chats rendered in the sidebar). Busy accounts' chat-store read can be slow (F-11) — tied to the instance-lifecycle work below.

> **Session 2 update (v4.7.0 → v4.8.9):** the **command center (L0)** is now built and shipped — it's the default Dashboard tab with auto-refresh, sparklines, per-account/By-location rollup (no raw-id leak), a date window (Today/7d/All), and a click-through **awaiting-reply** accordion that opens the actual waiting chat.
>
> **Key deviation from the plan:** on-time was redefined from *reply-latency SLA* to **"caught up %" = WhatsApp's own unread signal**, read directly from WhatsApp Web's local `model-storage` IndexedDB `chat` store. Reason (verified live): WhatsApp Web multi-device keeps only a small recent `message` cache, does **not** persist per-chat `lastMessage`, and uses `@lid` privacy ids — so reply-latency history is not reliably available, but `unreadCount` is. See [[whatsapp-web-indexeddb-oversight]] memory and `OversightChatSnapshotService`. Gotchas fixed along the way: `ExecuteScriptAsync` doesn't await promises (start/poll), long `message`-store cursors hang the read transaction (use bounded `chat` `getAll`), focus by sidebar `data-id` JID not title text.

> **Honest summary:** the **backend engine** (triage, SLA, analytics, backfill, Ollama, notifications, live WebView) is largely ✅/◑. The **product redesign** (workspace-rail IA, command-center, Workspace Management, channel-aware dashboards) and the **new channels/features** (Google reviews, Telegram, Meta, generic URL, digest, threshold alerts, ONNX tier, tone, business-hours SLA) are mostly ☐. No phase is "complete." This is a multi-week build; nothing below was implemented in this pass — it is a code-grounded audit.

---

## Phase 1 — WhatsApp oversight foundation

| Item | Status | Evidence / gap |
|---|---|---|
| Live view of a chat (L2) | ✅ | `InstanceSessionManager` + `ConversationNavigationCoordinator.NavigateToThreadAsync` |
| Analytics baseline, no-AI (Tier 0) | ✅ | `HeuristicTriageProcessor` (keyword urgency/sentiment) |
| SLA integrity (exclude backfilled, at-risk) | ✅ | shipped v4.5.0 (`ThreadData.IsSlaBreached/IsSlaAtRisk`) |
| L1 WhatsApp metrics + work queue | ✅ | OCC KPIs + work queue (`OperationsCommandCenterService`) |
| Personal/Professional scope | ✅ | **v4.11.0:** sidebar scope switch (All / Professional / Personal, persisted, shown only when both scopes exist) + scope-grouped sections |
| Command center (L0) — **backend engine** | ✅ | **Increment 2 (2026-06-16):** `OversightRollupBuilder` produces per-entity health (account *or* location) — on-time %, urgent, dropped, freshness, worst-first sort, needs-attention summary; works in ByInstance/ByLocation. Pure + 4 unit tests. |
| Command center (L0) — **live bridge** | ✅ | **Increment 3 (2026-06-16):** `OversightService.BuildSnapshot(grouping, instances)` wires the rollup to live threads + per-location SLA + connection-status freshness; registered in DI as `_services.Oversight`. Startup smoke clean. |
| Command center (L0) — **UI** | ✅ | **v4.7.0→v4.8.9:** `CommandCenterPanel` is the **default** Dashboard tab. Per-account & **By-location** rollup (per-instance location, no GUID leak); **auto-refresh** (20s); per-row **7-day sparkline**; **date window** Today/7d/All (`OversightWindow`, scoped by chat last-activity); **caught-up %** headline + exact **"N awaiting reply"**; **awaiting accordion** under each card listing the actual waiting customers (name or phone, "Unsaved contact" for `@lid`), each **click-through** to the chat (focus by `data-id` JID); **"syncing…"** state when an account's chat data hasn't loaded so header and list never disagree; manual **Re-sync history**. |
| Worst-first + needs-attention + freshness/stale (logic) | ✅ | in the rollup snapshot (Increment 2) |
| Per-card sparklines (UI) | ✅ | v4.7.0 — `BuildSparkline`, 7-day trend |
| Caught-up % from WhatsApp unread (chat store) | ✅ | **NEW (v4.8.1+):** `OversightChatSnapshotService` + IndexedDB `chat`-store read; primary on-time source, windowed by last activity |
| Workspace rail IA | ✅ | **v4.10.0–v4.12.0:** scope-grouped sidebar + per-location sub-headers (location rail) for professional accounts |
| Locations as first-class | ✅ | **v4.12.0:** "Set location…" assigns `BranchKey`; accounts sharing a location group in the rail and roll up in the command center |
| Workspace Management — **data layer** (per-location SLA + business hours) | ✅ | **Increment 1 (2026-06-16):** `WorkspaceProfile`/`BusinessHours` models, persisted in `AppSettings.WorkspaceProfiles`; `BusinessHoursCalculator` (SLA clock pauses outside hours); per-location threshold via `OperationalThresholds.GetSlaThresholdMinutes(locationKey)`; wired into the SLA clock. Backward-compatible (no profiles → identical). 5 new unit tests; 48/48 green. |
| Workspace Management — **UI** (per-location hours + SLA editor) | ◑ | **Increment 4 (2026-06-16):** `WorkspaceManagementDialog` (ContentDialog) edits per-location SLA + business hours; launched via the **command palette** (Ctrl+K → "Manage workspaces") and **Ctrl+Shift+W** (low-blast-radius — a dialog, not a SettingsPage rewrite). `WorkspaceManagementHelper` derives locations from professional accounts (2 tests). Build + 11 tests + startup smoke green. **Pending: visual verification by user** (sandbox blocks UI rendering check). Instance-assignment UI deferred. |
| WhatsApp history backfill (robust) | ✅* | **R-1 done (v4.8.x), reframed:** reads conversation state straight from local IndexedDB (`chat`-store `getAll`, stable JIDs) instead of DOM-walking; promise start/poll + watchdog; reconciliation marks answered chats. *Honest limit: WhatsApp Web only reliably exposes **current unread state**, not deep reply-latency history — so the metric is unread-based, not latency-based.* |
| Instance lifecycle (connected-but-light, stale, sane default cap) | ◑ | sessions + `MemoryUsageTargetLevel.Low` + LRU ✅; **default cap = 0 (unbounded)**, stale marking, orphan-handler fix ☐ |
| First-run onboarding | ✅ | **v4.13.0:** 4-step guided cold-start wizard (Welcome → Add account → Set locations → Hours/SLA), per-step skip, exception-guarded |
| Command center as L0 home + Work Queue (L1) | ✅ | **v4.13.0:** Dashboard *is* the command center; OCC moved to a dedicated Work Queue page (sidebar button, Ctrl+Shift+Q, nav-command routing) |

## Phase 2 — AI tiers

| Item | Status | Evidence / gap |
|---|---|---|
| Tier 0 heuristic baseline | ✅ | `HeuristicTriageProcessor` |
| Tier 2 local LLM (Ollama) | ✅ | `OllamaInferenceClient`, enrichment queue, Settings → AI (model pull, test) |
| Graceful no-AI degradation | ✅ | dashboard works on heuristic alone |
| AI source badges | ✅ | Heuristic / AI / Analyzing chips |
| Tier 1 lightweight (ONNX/Windows ML) | ☐ | net-new |
| Additive "assessment / recommendation" strips | ✅ (heuristic) | **v4.15.0:** per-account command-center insight strips (`CommandCenterPanel.BuildInsightStrip`) — deterministic on-device heuristic. Optional Ollama-generated phrasing still ☐. |
| Tone / quality (analyze outbound staff replies) | ☐ | net-new; Tier-2 feasible (research confirmed) |

## Phase 3 — Oversight depth & scale

| Item | Status | Evidence / gap |
|---|---|---|
| Time range (Today/Week/Date), weekly charts | ✅ | command center **Today / Last 7 days / All time** selector, scoped by chat last-activity (v4.8.3); weekly sparkline per row |
| Notifications infra | ◑ | `AppNotificationService` + `NotificationHub` send **message** toasts |
| Proactive **threshold** alerts (waiting > X / on-time < Y) | ✅ | **v4.9.0/4.9.2:** `OversightAlertMonitor` toasts when an account's awaiting-reply count crosses a configurable threshold (edge-triggered) |
| "Since you last opened" digest | ✅ | **v4.9.2:** `BuildDigest` summarizes new-since-last-seen / total awaiting on first command-center view per session |
| Search + cards/list density at scale | ◑ | command-palette search ✅, `OccCompactCardDensity` ✅; entity/rail search + list-density ☐ |
| Generic-URL webview instances | ☐ | `PlatformDefinition` is WhatsApp-only |

## Phase 4 — Google Business reviews channel
☐ **Not started.** Net-new: embed Business Profile web UI, scrape reviews (rating/% responded/unanswered), reply-from-web, review-metrics module. (API exists but excluded by no-cloud rule.)

## Phase 5 — Additional channels (Telegram, then Meta)
☐ **Not started.** `PlatformKind`/adapters are WhatsApp-only. Each is an isolated integration (Telegram low-risk; Meta higher-risk).

## Cross-cutting (any phase)

| Item | Status | Evidence / gap |
|---|---|---|
| Ingress coalescing | ✅ | `WebMessageIngressService` (v4.2) |
| List/kanban virtualization | ◑ | ItemsRepeater migration — verify no residual ListView |
| WebView2 memory strategy + default cap + orphan handler | ☐ | RED (post-suspend RAM unmeasured; cap=0; API-mixing) |
| `IInstanceConnection` abstraction | ☐ | net-new; future-proofs channels |
| Contrast remediation | ◑ | teal-on-light AA partial |
| CI asset guard | ✅ | shipped this session |
| Stress fixtures in CI | ◑ | Stage-4 tests exist; not all wired |

---

## What this means
- **Done (✅):** the conversation-analytics engine — triage, SLA timing, work queue, backfill plumbing, Ollama AI, live chat navigation, notifications plumbing, heuristic no-AI baseline.
- **Partial (◑):** everything that exists as "branches + OCC tabs + flat sidebar" but must become "locations + command center + workspace rail," plus AI-strip UX, time-range, search/density, onboarding, backfill robustness, instance lifecycle.
- **Not started (☐):** the workspace-rail IA, Workspace Management (business hours + per-location SLA), channel-aware dashboards, **Google reviews / Telegram / Meta / generic-URL channels**, "since you last opened" digest, threshold alerts, Tier-1 ONNX, tone analysis, and the WebView2 memory/abstraction work.

## What's left — **updated 2026-06-19 (post v4.16.0)**

**Phase 1 (WhatsApp oversight foundation): complete.** Command center home, caught-up metric + awaiting list + drill-down, date window, sparklines, location rail + scope switch, Workspace Management (data + Settings), unread-based backfill, 4-step onboarding, Work Queue. The former cross-cutting gap (instance lifecycle / WebView2 memory) is now **closed** as of v4.16.0.

**Phase 3: alerts + digest + date ranges done.** Remaining: search/density at scale ◑, generic-URL webview instances ☐.

~~1. **Instance lifecycle / WebView2 memory.**~~ **DONE (v4.16.0 + prior).** Session cap defaults to 6 with LRU eviction (not unbounded); `AdapterHealthMonitor` marks adapters stale at 90s and `MainWindow.OnAdapterStaleDetected` runs the orphan/recovery handler; per-instance memory tiers + Low background memory target; v4.16.0 added the time-based idle-session reaper (closes idle non-visible personal sessions, professional-exempt). Addresses F-11 slowness + post-suspend RAM.

Remaining work, highest-leverage first:
1. **Phase 2 — deeper AI tiers.** Insight strips ship as a heuristic (v4.15.0); next is optional **Ollama-generated** per-account summaries (background-cached, heuristic fallback when AI off) and **outbound staff-reply tone/quality**. Tier-1 lightweight ONNX still net-new.
2. **Phase 3 leftovers.** Generic-URL webview instances (non-WhatsApp, no dashboard data); entity/rail search + list-density at scale.
3. **Phase 4 — Google Business reviews channel** (embed web UI, scrape ratings/% responded/unanswered, reply-from-web).
4. **Phase 5 — Telegram, then Meta** (isolated per-channel adapters).
5. **Polish/cleanup.** Remove dead drag code; make the awaiting-list preview more reliable (or a bounded message-store read); optional true drag-reorder via `ListView.CanReorderItems`; contrast remediation; CI stress fixtures.

Each step ships green and is reversible.
