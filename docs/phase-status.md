# Build status — Phases 1–5 (done / left)

**Date:** 2026-06-21 · **Baseline:** v4.27.1 · **Source of truth:** [MASTER-PLAN.md](MASTER-PLAN.md)
**Legend:** ✅ done (works; may need adapting to new IA) · ◑ partial (exists in primitive form) · ☐ not started (net-new)

> **Session 5 update (v4.22.0 → v4.27.1): UI/UX modernization, shell IA, bug fixes, Work-Queue merge.**
> - **Command-center modernization (v4.22–v4.25):** vertical avatar cards, bar-chart sparklines, urgent/dropped sub-metrics, attention Jump button, dark insight strips, segmented group control, "Define locations" CTA, title-bar **scope selector + AI toggle**, compact-sidebar default, **WCAG 1.4.1 status glyph**, AI-strip sanitization.
> - **Shell IA dialogs (v4.23):** SetLocation / ConfirmPermanentDelete / AutoUpdate / PinToTaskbar; WorkQueue view-state.
> - **Critical build fix:** the documented publish command wrote to `bin\Release` but the installer packages `bin\x64\Release` → every install Jun 19–20 shipped a stale binary. Fixed; CLAUDE.md updated; **always `-p:Platform=x64` + verify installed `FileVersion`.**
> - **Bug fixes (v4.26):** instance **delete** no longer crashes (`UiThreadRunner` pins WebView2/COM teardown to the UI thread); **reorder** no longer hangs (sidebar menu rebuild is reparent-safe); **opened≠replied** (direction-first awaiting + C# sticky-awaiting); **embed channels now appear in the sidebar** (split `IsPlatformModuleEnabled` vs `IsSidebarVisible`) and are excluded from the oversight command center.
> - **Work Queue → Dashboard merge (v4.27):** new "**Needs reply**" command-center mode = flat, cross-account, worst-first awaiting list; standalone OCC kanban **retired** (sidebar button collapsed; page/code dormant + reversible).
> - **Blocked (needs a live WhatsApp session):** unsaved-contact phone resolution (`@lid`→phone) + message-gist preview.

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
| Additive "assessment / recommendation" strips | ✅ | **v4.15.0:** per-account command-center insight strips (`CommandCenterPanel.BuildInsightStrip`) — deterministic on-device heuristic. **v4.17.0:** optional Ollama-phrased line (`OversightInsightService`, ✦ AI tag, counts-only prompt, heuristic fallback). |
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
◑ **Embed slice done (v4.21.0).** Telegram (`web.telegram.org`) and Messenger (`messenger.com`) are registered platforms — selectable in "Add account", each gets its own isolated WebView session and branded accent colour. Routes to `NullPlatformAdapter` (no metric scraping yet). A Telegram adapter reading unread/awaiting from `web.telegram.org` DOM and a Messenger adapter (passive read-only; Meta fights automation) are future work that need a live logged-in account to tune.

## Cross-cutting (any phase)

| Item | Status | Evidence / gap |
|---|---|---|
| Ingress coalescing | ✅ | `WebMessageIngressService` (v4.2) |
| List/kanban virtualization | ◑ | ItemsRepeater migration — verify no residual ListView |
| WebView2 memory strategy + default cap + orphan handler | ☐ | RED (post-suspend RAM unmeasured; cap=0; API-mixing) |
| `IInstanceConnection` abstraction | ☐ | net-new; future-proofs channels |
| Contrast remediation | ◑ | teal-on-light AA partial; **WCAG 1.4.1 non-color status glyph on L0 cards done (v4.25.0)** |
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
0. **UI/UX modernization (v4.22–4.24, cross-cutting).** ☐ Three-increment visual redesign tracked below. Highest priority — the product works but the dashboard doesn't look or feel premium compared to the proposed design.
1. **Phase 2 — deeper AI tiers.** ✅ Insight strips now have optional Ollama-phrased lines (v4.17.0, heuristic fallback). Remaining: **outbound staff-reply tone/quality** scoring; Tier-1 lightweight ONNX (net-new).
2. **Phase 3 leftovers.** ✅ Command-center entity search + compact density (v4.18.0); ✅ generic-URL webview instances (v4.19.0). Remaining: sidebar-rail search/density at very large account counts.
3. **Phase 4 — Google Business reviews channel.** ◑ Embed slice done (v4.20.0). Remaining: metric-scraping adapter — needs live logged-in account.
4. **Phase 5 — Telegram, then Meta.** ◑ Embed slice done (v4.21.0). Remaining: metric-scraping adapters — needs live logged-in accounts.
5. **Polish/cleanup.** Remove dead drag code; awaiting-list preview reliability; contrast remediation; CI stress fixtures.

## UI/UX Modernization Plan (v4.22–v4.24)

**Reference:** Proposed mockup screenshot shared 2026-06-20. Goal: make the live UI match the proposed design.

### Gap analysis (current → proposed)

| Element | Current | Proposed | Increment |
|---|---|---|---|
| Sparklines | Line polyline 64×18 | Colored vertical bar chart | v4.22.0 |
| Sub-metrics | None per card | urgent + dropped counts | v4.22.0 |
| Needs-attention banner | Text only | Text + **Jump** button | v4.22.0 |
| AI insight strip style | Amber/red background | Dark neutral surface | v4.22.0 |
| Card layout | Horizontal row expander | Vertical card: avatar, large %, bar chart, sub-metrics, strip | v4.23.0 |
| Account avatar in card | None | Colored circle with initials | v4.23.0 |
| Group mode control | ToggleSwitch | Segmented "Group: none ∣ By location" buttons | v4.24.0 |
| Scope toggle | Sidebar dropdown | "Professional ∣ Personal" in title bar | v4.24.0 |
| Sidebar default | Pinned expanded (320px) | Compact icon rail (56px) | v4.24.0 |
| Locations CTA | None | Banner when no locations defined | v4.24.0 |

### v4.22.0 — Quick wins (Increment 52)
- `BuildSparkline` → 7 vertical `Rectangle` bars, color-matched, rounded tops
- `BuildRowContent` → urgent + dropped sub-metrics row after awaiting count
- Attention banner → add `Jump` `Button`; on click navigate to worst entity's first member instance
- `BuildInsightStrip` → replace amber/red `SystemFillColor*BackgroundBrush` with dark neutral surface (`ControlSolidFillColorDefaultBrush` with slight accent tint)

### v4.23.0 — Vertical card layout (Increment 53)
- `BuildRowContent` → restructure to vertical card: avatar circle top-left, account name, status dot; large colored % hero; bar sparkline + freshness; urgent/dropped row; AI strip at card bottom
- Avatar via `PlatformBrandingHelper.GetInitials` + accent color per instance (needs lookup from registry)
- Expander still wraps the whole card; awaiting list expands below

### v4.24.0 — Shell modernization (Increment 56) ✅ shipped
- ✅ Replace `GroupToggle` ToggleSwitch → two `ToggleButton`s ("By account" / "By location") styled as a segmented control *(landed early in v4.22.0)*
- ✅ Move scope switch out of sidebar → `ScopeSelector` ComboBox in `TitleBar.RightHeader` (All ∣ Professional ∣ Personal). Scope state stays owned by `WorkspaceSidebar`; title bar drives it via `SetScope` / `ScopeSelectorStateChanged` so the rail render logic is untouched. Shown only when both scopes have accounts.
- ✅ Title-bar **AI toggle** mirroring `AppSettings.EnableLocalAi` (two-way sync with Settings → AI; graceful heuristic fallback).
- ✅ `AppSettings.SidebarPinnedExpanded` default `false` (compact 56px rail on first run); existing users keep their persisted value.
- ✅ "Define locations ↗" CTA border in `CommandCenterPanel` *(landed early in v4.22.0)*.

**Deferred from this increment:** a true `SelectorBar` segmented look for the scope switch (shipped as a compact ComboBox for reliability in the 48px title bar); status glyph alongside card color for strict WCAG 1.4.1.
