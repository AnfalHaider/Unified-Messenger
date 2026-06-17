# Build status — Phases 1–5 (done / left)

**Date:** 2026-06-16 · **Baseline:** v4.6.0 · **Source of truth:** [MASTER-PLAN.md](MASTER-PLAN.md)
**Legend:** ✅ done (works; may need adapting to new IA) · ◑ partial (exists in primitive form) · ☐ not started (net-new)

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
| Command center (L0) — **UI** (health-card grid, group-by switch) | ☐ | binds to `_services.Oversight`; needs XAML |
| Worst-first + needs-attention + freshness/stale (logic) | ✅ | in the rollup snapshot (Increment 2) |
| Per-card sparklines (UI) | ☐ | needs a small chart component |
| Workspace rail IA | ◑ | `WorkspaceSidebar` is a **flat instance list**, not a per-location rail |
| Locations as first-class | ◑ | "branches" exist (`BranchKey`, `BranchWorkspaceHelper`, `ActiveWorkspaceContext`) — need promoting to workspaces |
| Workspace Management — **data layer** (per-location SLA + business hours) | ✅ | **Increment 1 (2026-06-16):** `WorkspaceProfile`/`BusinessHours` models, persisted in `AppSettings.WorkspaceProfiles`; `BusinessHoursCalculator` (SLA clock pauses outside hours); per-location threshold via `OperationalThresholds.GetSlaThresholdMinutes(locationKey)`; wired into the SLA clock. Backward-compatible (no profiles → identical). 5 new unit tests; 48/48 green. |
| Workspace Management — **UI** (per-location hours + SLA editor) | ◑ | **Increment 4 (2026-06-16):** `WorkspaceManagementDialog` (ContentDialog) edits per-location SLA + business hours; launched via the **command palette** (Ctrl+K → "Manage workspaces") and **Ctrl+Shift+W** (low-blast-radius — a dialog, not a SettingsPage rewrite). `WorkspaceManagementHelper` derives locations from professional accounts (2 tests). Build + 11 tests + startup smoke green. **Pending: visual verification by user** (sandbox blocks UI rendering check). Instance-assignment UI deferred. |
| WhatsApp history backfill (robust) | ◑ | `WhatsAppBackfillProvider` exists but bounded (deep-backfill MVP ~3 chats) — **R-1** needs hardening |
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
| Time range (Today/Week/Date), weekly charts | ◑ | OCC date-range + Live/Historical exist; the simple Today/Week/Date control + always-weekly default ☐ |
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

## Recommended execution order (verified increments, lowest-risk first)
1. **Data + settings layer (mostly C#, low XAML risk):** promote branches→workspaces/locations; add business hours + per-location SLA in Settings → Workspace Management; fix default session cap + stale marking + orphan handler.
2. **Command center backend:** per-entity rollup (account or location), needs-attention + worst-first + freshness from existing metrics.
3. **Shell IA (higher XAML risk — incremental):** workspace rail + scope switch + the L0/L1/L2 surfaces, each build-verified.
4. **Phase 2 AI-strip UX + Tier-1 ONNX; Phase 3 digest/alerts/time-range/density.**
5. **Phase 4 Google reviews; Phase 5 Telegram → Meta.**

Each step ships green and is reversible. This is the safe way to turn the preview into the real app without breaking v4.6.0.
