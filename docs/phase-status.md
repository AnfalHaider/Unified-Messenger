# Build status — Phases 1–5 (done / left)

**Date:** 2026-06-18 · **Baseline:** v4.8.9 · **Source of truth:** [MASTER-PLAN.md](MASTER-PLAN.md)
**Legend:** ✅ done (works; may need adapting to new IA) · ◑ partial (exists in primitive form) · ☐ not started (net-new)

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
| Personal/Professional scope | ◑ | `WorkspaceCategory` + OCC/Personal tabs exist — **not** a top-level scope switch with separate rails |
| Command center (L0) — **backend engine** | ✅ | **Increment 2 (2026-06-16):** `OversightRollupBuilder` produces per-entity health (account *or* location) — on-time %, urgent, dropped, freshness, worst-first sort, needs-attention summary; works in ByInstance/ByLocation. Pure + 4 unit tests. |
| Command center (L0) — **live bridge** | ✅ | **Increment 3 (2026-06-16):** `OversightService.BuildSnapshot(grouping, instances)` wires the rollup to live threads + per-location SLA + connection-status freshness; registered in DI as `_services.Oversight`. Startup smoke clean. |
| Command center (L0) — **UI** | ✅ | **v4.7.0→v4.8.9:** `CommandCenterPanel` is the **default** Dashboard tab. Per-account & **By-location** rollup (per-instance location, no GUID leak); **auto-refresh** (20s); per-row **7-day sparkline**; **date window** Today/7d/All (`OversightWindow`, scoped by chat last-activity); **caught-up %** headline + exact **"N awaiting reply"**; **awaiting accordion** under each card listing the actual waiting customers (name or phone, "Unsaved contact" for `@lid`), each **click-through** to the chat (focus by `data-id` JID); **"syncing…"** state when an account's chat data hasn't loaded so header and list never disagree; manual **Re-sync history**. |
| Worst-first + needs-attention + freshness/stale (logic) | ✅ | in the rollup snapshot (Increment 2) |
| Per-card sparklines (UI) | ✅ | v4.7.0 — `BuildSparkline`, 7-day trend |
| Caught-up % from WhatsApp unread (chat store) | ✅ | **NEW (v4.8.1+):** `OversightChatSnapshotService` + IndexedDB `chat`-store read; primary on-time source, windowed by last activity |
| Workspace rail IA | ◑ | `WorkspaceSidebar` is a **flat instance list**, not a per-location rail |
| Locations as first-class | ◑ | "branches" exist (`BranchKey`, `BranchWorkspaceHelper`, `ActiveWorkspaceContext`) — need promoting to workspaces |
| Workspace Management — **data layer** (per-location SLA + business hours) | ✅ | **Increment 1 (2026-06-16):** `WorkspaceProfile`/`BusinessHours` models, persisted in `AppSettings.WorkspaceProfiles`; `BusinessHoursCalculator` (SLA clock pauses outside hours); per-location threshold via `OperationalThresholds.GetSlaThresholdMinutes(locationKey)`; wired into the SLA clock. Backward-compatible (no profiles → identical). 5 new unit tests; 48/48 green. |
| Workspace Management — **UI** (per-location hours + SLA editor) | ◑ | **Increment 4 (2026-06-16):** `WorkspaceManagementDialog` (ContentDialog) edits per-location SLA + business hours; launched via the **command palette** (Ctrl+K → "Manage workspaces") and **Ctrl+Shift+W** (low-blast-radius — a dialog, not a SettingsPage rewrite). `WorkspaceManagementHelper` derives locations from professional accounts (2 tests). Build + 11 tests + startup smoke green. **Pending: visual verification by user** (sandbox blocks UI rendering check). Instance-assignment UI deferred. |
| WhatsApp history backfill (robust) | ✅* | **R-1 done (v4.8.x), reframed:** reads conversation state straight from local IndexedDB (`chat`-store `getAll`, stable JIDs) instead of DOM-walking; promise start/poll + watchdog; reconciliation marks answered chats. *Honest limit: WhatsApp Web only reliably exposes **current unread state**, not deep reply-latency history — so the metric is unread-based, not latency-based.* |
| Instance lifecycle (connected-but-light, stale, sane default cap) | ◑ | sessions + `MemoryUsageTargetLevel.Low` + LRU ✅; **default cap = 0 (unbounded)**, stale marking, orphan-handler fix ☐ |
| First-run onboarding | ◑ | `FirstRunOnboardingHelper` (Personal/Professional) — not the 4-step guided cold-start |

## Phase 2 — AI tiers

| Item | Status | Evidence / gap |
|---|---|---|
| Tier 0 heuristic baseline | ✅ | `HeuristicTriageProcessor` |
| Tier 2 local LLM (Ollama) | ✅ | `OllamaInferenceClient`, enrichment queue, Settings → AI (model pull, test) |
| Graceful no-AI degradation | ✅ | dashboard works on heuristic alone |
| AI source badges | ✅ | Heuristic / AI / Analyzing chips |
| Tier 1 lightweight (ONNX/Windows ML) | ☐ | net-new |
| Additive "assessment / recommendation" strips | ◑ | next-action/summary exist; the card insight-strip UX ☐ |
| Tone / quality (analyze outbound staff replies) | ☐ | net-new; Tier-2 feasible (research confirmed) |

## Phase 3 — Oversight depth & scale

| Item | Status | Evidence / gap |
|---|---|---|
| Time range (Today/Week/Date), weekly charts | ✅ | command center **Today / Last 7 days / All time** selector, scoped by chat last-activity (v4.8.3); weekly sparkline per row |
| Notifications infra | ◑ | `AppNotificationService` + `NotificationHub` send **message** toasts |
| Proactive **threshold** alerts (waiting > X / on-time < Y) | ☐ | net-new (feasible via `ToastNotificationManagerCompat`) |
| "Since you last opened" digest | ☐ | net-new |
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

## Recommended execution order — **updated 2026-06-18 (post v4.8.9)**

Done: data/settings layer (Workspace Management), command-center backend **and** UI (steps 1–2 + most of the L0 surface), date window, sparklines, backfill robustness (unread-based), location rollup.

Next, lowest-risk / highest-value first:
1. **Proactive threshold alerts (Phase 3) — recommended.** Desktop toast when an account's "N awaiting" (or oldest wait) crosses a threshold. Small step on top of `OversightChatSnapshotService` (we already compute awaiting per account); delivers the core "oversight without watching" promise. Notifications infra (`AppNotificationService`/`NotificationHub`) already exists.
2. **"Since you last opened" digest (Phase 3)** — same data source; summarize new awaiting since last session.
3. **Shell IA (higher XAML risk):** promote the command center from a tab to the home surface; workspace rail + Personal↔Professional scope switch.
4. **Instance lifecycle / WebView2 (cross-cutting RED):** stale-marking + session cap + orphan handler — addresses the "Connecting… → syncing…" timeout seen on busy accounts (e.g. F-11).
5. **Phase 2 AI-strip UX + Tier-1 ONNX; Phase 4 Google reviews; Phase 5 Telegram → Meta.**

Each step ships green and is reversible.
