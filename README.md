# Unified Messenger

Native WinUI 3 desktop client for running **multiple isolated WhatsApp / WhatsApp Business Web sessions** in one window, with a unified notification hub and lightweight operations dashboards.

**Current release:** v4.48.0 (Data-accuracy audit fixes: **Messages/day no longer counts group chats, WhatsApp Status, broadcasts, or channels** — only real customer messages (this is what inflated the count to hundreds/day), and the authoritative message-store recount now **replaces** old inflated figures on Re-sync instead of keeping the higher number. Daily buckets use your **local calendar day** (evening messages no longer roll onto the previous day in UTC+ timezones). The Activity-graph **date-range filter now actually works** — hour-of-day reads a per-day×per-hour matrix scoped to the selected range instead of always showing all-time. And **Response time / SLA met exclude pre-existing backlog** — only messages that arrived while the app was watching that account count, so the first sync no longer reports a misleading multi-day FRT. Requires one Re-sync after updating. Builds on v4.47.0 redesigned account cards)  ·  _older:_ (Redesigned account cards with **live per-account detail**: a per-card freshness stamp ("updated 3m ago"), a detail-chip row — **reply ~Xm** (median first-reply time), **N answered today**, **N past your reply target**, urgent, dropped — plus a "longest wait right now" nudge, tooltips explaining every number in plain language, and a **status legend** under the cards. Fixes the contradictory "45 late on a 100% caught-up account" figure — "past target" is now counted from the same live snapshot as the awaiting pill, so the numbers always agree. Also: **shimmer skeleton cards** while the first scan runs, subtle card entrance/reposition motion, and a labeled 7-day sparkline. Builds on v4.46.0 (First Response Time, SLA met %, responsive clickable KPI band). Plus the redesigned command center + KPI band, Activity patterns graph + week-over-week trend, AI shift briefing, durable oversight snapshot, custom account icons, the Google Business Reviews section, and a top Personal button.)

### What's in v4.44.0
- **Smarter AI shift briefing (#33/#34/#36):** the briefing now adds an **end-of-day projection** ("on pace for ~N today"), an **anomaly flag** ("busier than usual"), and a **ranking rationale** (the account furthest behind + its caught-up %). Deterministic heuristics with a local-AI swap when Ollama is on.

### What's in v4.43.0
- **AI shift briefing (#25):** a one-line, whole-business "where to focus first" summary under the KPI band — deterministic heuristic always, swapped for a local-AI line when Ollama is on (aggregate counts only; account names but never customer names/text).
- **Week-over-week trend (#37):** the Activity patterns panel now shows this-week-vs-last-week message volume + the busiest weekday, derived from the on-device activity history.

### What's in v4.42.0
- **Google Business review-health (Phase 4):** a new dashboard **Reviews** section scrapes each Google Business account's live reviews page for **reviews awaiting a reply** (the actionable signal) and **reply rate** on the loaded page. Refresh on demand. (Google exposes no aggregate rating/total on the manager reviews page, so those aren't shown.)

### What's in v4.41.0
- **Custom account icons (expanded):** right-click an account → **Change icon** to choose a social-media brand logo (WhatsApp, Telegram, Instagram, Facebook, Messenger, X, TikTok, YouTube, LinkedIn, Discord, Pinterest, Reddit, WeChat, Google), a general icon, **import the account's profile photo**, or **upload an image from your PC**. Reset to initials anytime.

### What's in v4.40.0
- **Command-center redesign:** at-a-glance KPI band (caught up · awaiting · messages/day · busiest window), redesigned account cards (avatar, status %, full-height status rail, awaiting pill, in-card AI strip), info-styled dismissible digest, single-scroll dashboard.
- **Activity patterns graph:** one filterable chart — Hour of day / Day of week / Month — with account + range filters, peak highlight, and a plain-language insight line. Reads an on-device activity-history log (retained ~400 days; fully local).
- **Durable oversight snapshot:** the live dashboard (caught-up %, awaiting list, counts) now persists to disk, loads instantly on launch with an "Updated …" stamp, and re-sync updates incrementally instead of starting blank. Analytics history merges (never wipes/double-counts) on re-sync.
- **Custom account icons:** right-click an account → **Change icon** to pick a social-media brand logo (WhatsApp, Telegram, Instagram, Facebook, Messenger, X, TikTok, YouTube, LinkedIn, Discord, Pinterest, Reddit, WeChat, Google — via a bundled Font Awesome Brands font), a general icon, or **import the account's profile photo** from its live session. Reset to initials anytime. Shows in the sidebar and dashboard cards.
- **Bug fixes:** removed a stray floating `Ctrl+D` tooltip (suppressed auto-generated accelerator tooltips); **fixed account names vanishing** after adding an account (the sidebar reused cached rows whose label references were cleared on rebuild — rows are now recreated so titles/status/badges stay correct); the Change-icon dialog no longer gets occluded by an open account's WebView.

## Scope

| In scope | Out of scope (deferred) |
|----------|-------------------------|
| Multi-instance WhatsApp & WhatsApp Business (WebView2 profiles) | Telegram, Slack, Discord, Meta, Google Business, custom URLs |
| Unified desktop notification feed + taskbar badge | Auto-draft injection, copilot hotkeys |
| Fixed-layout **Operations Command Center** (heuristic triage, branch pills, kanban) | OCC layout builder, platform intelligence panels, CSV export |
| **Personal Overview** panel | Voice-note pipeline, branch pulse LLM summaries |
| Heuristic message triage + optional **local Ollama** enrichment (top urgent live threads) | Tier 5 message decryption, multi-platform adapters |
| **Local AI (Ollama)** — Settings toggle, model pull, on-device summaries | Cloud LLM, auto-send replies |
| **Startup backfill** (IndexedDB + sidebar snapshot, OCC status) | Full deep backfill automation (MVP: bounded walk only) |
| Lite installer (~66 MB); Ollama runtime downloaded from Settings › AI (v3.7.0) | Per-platform module toggles |

Requires **Windows 10 1809+** or **Windows 11** and the **WebView2 Runtime** (preinstalled on most Windows 11 systems).

## Download (Windows)

| Platform | Installer |
|----------|-----------|
| **x64** | [UnifiedMessengerSetup.exe](https://github.com/AnfalHaider/Unified-Messenger/releases/latest/download/UnifiedMessengerSetup.exe) |
| **ARM64** | [UnifiedMessengerSetup-arm64.exe](https://github.com/AnfalHaider/Unified-Messenger/releases/latest/download/UnifiedMessengerSetup-arm64.exe) |

All releases: [github.com/AnfalHaider/Unified-Messenger/releases](https://github.com/AnfalHaider/Unified-Messenger/releases)



### What's in v4.34.0

- **The real cause of the freezes: a registry semaphore re-entrancy deadlock.** Every instance mutation — Remove, Move up/down, Rename, Set location, Mute, Memory tier — does `await _gate.WaitAsync()` and then looked up the instance via the public `FindById`, which itself did a synchronous `_gate.Wait()` on the **same** non-reentrant `SemaphoreSlim`. Holding the gate and re-acquiring it = instant permanent deadlock that froze the whole app (and was also why the registry tests "hang" per CLAUDE.md). Fixed by giving in-gate callers a lock-free `FindByIdNoLock`. A regression test now drives all eight mutators in sequence under a 10s cap. *(v4.33's WebView2-timeout hardening still stands as defense-in-depth, but this deadlock was the freeze.)*

### What's in v4.33.0

- **Instance context-menu freeze fixed (especially "Remove instance").** Removing an instance disposes its WebView2, and the teardown awaited `TrySuspendAsync()` on the UI thread with **no timeout** — on a busy/loading WebView that await could never complete, hanging the UI thread and freezing the *whole* app (so every other context-menu action you tried afterward also looked stuck). Two fixes: (1) disposal now **closes the WebView directly** without the pointless pre-close suspend; (2) **every WebView2 operation now has a 12-second timeout** (`WebViewUiAwaiter`) — a wedged op becomes a recoverable, logged error instead of a permanent freeze. This covers Remove, Refresh WebView, account switching, and the permanent-delete profile wipe. The non-WebView actions (Move, Rename, Mute, Set location, Memory tier) never touched WebView2 — they only appeared stuck because a hung Remove had already frozen the app.

### What's in v4.32.0

- **Removed the dormant OCC / Work Queue subsystem.** Retired as a destination in v4.27.0 and with its SLA logic harvested into the command center in v4.31.0, the kanban/triage/branch-filter subsystem was deleted: **43 files / ~5,400 lines** — the OperationsCommandCenter control + partials, KanbanColumnBoard, WorkQueuePage, the `Occ*` filter/view-mode/state services, presenters, view-models, the branch pill bar, and 8 OCC tests; plus the Work-Queue navigation (ShowWorkQueueAsync, the Ctrl+Shift+Q shortcut, the sidebar Work Queue button, and the OCC command-palette/nav-event handlers). The **shared** SLA/triage engine (`ThreadData`, `BusinessHoursCalculator`, `OperationalThresholds`, `MessageTriageService`, `ThreadRegistryService`) is kept — it feeds the command center's "N late" metric. A few `Occ`-prefixed *utilities* that the live analytics/personal-dashboard depend on (`OccDateRangeFilterHelper`, the `OccQueueFilter`/`OccViewMode` enums) were kept as well. 83 tests green.

### What's in v4.31.0

- **The real business-hours SLA is now on the cards (P1-A).** MASTER-PLAN §8's centerpiece — reply-latency measured within each location's working hours — was computed in the rollup but then thrown away in favour of the unread-based "caught up %". Each card now also shows a **"N late"** sub-metric (next to urgent/dropped): open conversations past their business-hours reply SLA (`ThreadData.IsSlaBreached` + per-location `BusinessHoursCalculator`). It's independent of the caught-up %, so responsiveness — not just unread state — is finally visible, and it shows 0 when there's no thread data.
- **OCC decision (P1-B):** the dormant Operations Command Center stays retired; its valuable SLA logic (which lives in shared services, not the kanban UI) is harvested into the command center rather than deleted. Documented in `docs/remaining-work.md`.

### What's in v4.30.0

- **WCAG 1.4.1 coverage finished (P1-C).** The shape-distinct status glyph (✓/⚠/⨯) was only on comfortable-density cards. It now also appears on **compact cards** (where the % is hidden, so status was previously colour-only) and a warning glyph precedes the count on **Needs-reply rows** — status is never conveyed by colour alone anywhere.
- **Sticky-awaiting safety valve (P1-D).** v4.26's sticky-awaiting could, in theory, keep a chat "awaiting" forever if an outbound reply was never observed (no DOM hint, no persisted last message). A chat can now only be carried as awaiting via inheritance while its last activity is within **7 days**; past that an unconfirmed-clear is allowed through, so it can't get permanently stuck. A genuinely-waiting chat keeps getting fresh awaiting reads and is unaffected. (Regression test added.)

### What's in v4.29.0

- **Workspace sidebar redesign.** A cleaner, more functional account rail (research-backed: clear hierarchy, channel cues, density, collapsible groups):
  - **Collapsible location groups** — each location sub-header (e.g. "DHA-2") now has a **chevron + account count** and can be collapsed/expanded (click or keyboard); collapse state persists across refreshes and only applies in the expanded rail.
  - **Channel-aware row subtitles** — each account's second line now shows its **channel** ("WhatsApp", "Meta Business Suite", "Google Business", "Discord"…) instead of a repeated "Connected · syncing". Real problems still surface ("Signed out — tap to reconnect", "Connection error"); transient connecting/syncing is conveyed by the status dot's colour.
  - **Tighter density** so more accounts fit without scrolling.
  - The full sidebar contract (navigation events, badges, health dots, accessibility, compact icon-rail, context menus) is preserved — purely additive/visual.

### What's in v4.28.1

- **Command center no longer shows "No professional accounts yet" on startup** while WhatsApp's first IndexedDB history scan is still running. It now reads **"Syncing accounts — reading each account's local history…"** when oversight accounts exist but haven't reported data yet, and only says "no accounts" when there genuinely are none.

### What's in v4.28.0

- **New embed channels — Discord, Meta Business Suite, Instagram.** "Add account" now offers these alongside WhatsApp / Google Business / Telegram / Messenger / generic. They were missing, so trying to add (e.g.) a Discord or Meta Business Suite account fell back to **WhatsApp** — the instance then loaded WhatsApp Web. Each new channel is embed-only (own isolated session, branded accent, no oversight scraping yet).
- **Google Business "browser not supported" fixed.** Only Discord previously got a desktop user-agent; every other embed used WebView2's default UA, which Google/Meta reject. All embed channels (Google Business, Meta, Messenger, Telegram, Discord, Instagram, generic) now send a clean desktop **Chrome UA**. WhatsApp keeps its default UA (the scraper depends on it). *Note:* Google's sign-in may still resist embedded browsers (their anti-embedding is aggressive); the UA fix removes the blanket "unsupported browser" block.
- *Heads-up:* instances created before this (named "Meta Business Suite"/"Discord" but stored as WhatsApp) won't auto-correct — remove and re-add them on the proper channel.

### What's in v4.27.1

- **Embed channels no longer clutter the command center.** A *professional* Google Business / Telegram / Messenger / generic instance has no WhatsApp chat store to scan, so it would show in the oversight cards stuck at "syncing…" forever. The command center (and the "Needs reply" list) now include only oversight-capable platforms (WhatsApp family). Embed channels stay fully visible and usable in the sidebar — they just don't appear as oversight cards.

### What's in v4.27.0

- **"Needs reply" — the Work Queue, merged into the Dashboard.** The command-center segmented control gains a third mode (**By account ∣ By location ∣ Needs reply**). "Needs reply" is a single **flat, cross-account list of every customer awaiting a reply, worst-first** (most unread, then longest-waiting), each row a click-through straight to the live chat. It's derived entirely from the same oversight snapshot that powers the per-card accordion — no manual drag-to-status, no drift, and fully consistent with the read-only stance (a row just navigates you to the chat to reply by hand). Respects the date window and compact density.
- **Standalone Work Queue (kanban OCC) retired** as a sidebar destination — its purpose now lives in "Needs reply." The page and OCC code remain intact and dormant (still reachable via Ctrl+Shift+Q / command palette), so the change is reversible. *Why:* a manual kanban duplicated the Dashboard's awaiting data, fought the app's passive/derived philosophy, and sat at the wrong altitude for an owner (a doer's tool, not an overseer's).

### What's in v4.26.1

- **Embed channels now appear in the sidebar.** Adding a Google Business (or Telegram / Messenger / generic URL) account left it addable and visible in the Work Queue but **invisible in the sidebar** — so it could never be opened, and therefore "never loaded." The sidebar was gated on a WhatsApp-only check (`IsPlatformModuleEnabled`) that's really the "participates in WhatsApp scraping pipelines" gate. Split it: WhatsApp-only stays for backfill/adapter/analytics, and a new `IsSidebarVisible` (any addable platform) drives sidebar visibility. Embed channels now show and open normally. (4 regression tests across the embed platforms.)

### What's in v4.26.0

- **Removing an instance no longer crashes** with *"the application called an interface that was marshalled for a different thread."* The teardown touches WebView2 (COM/STA) and UI-coupled services, so it must run on the UI dispatcher thread; it's now pinned there via `UiThreadRunner` (a plain `ConfigureAwait` isn't enough — WinRT awaitables resume on thread-pool threads regardless).
- **Moving an instance up/down no longer hangs the app.** The sidebar menu rebuild used an incremental reconciliation that could re-insert a cached row still parented at another index — WinUI mishandles re-parenting inside the same panel and wedged the layout pass. The rebuild now detaches and re-adds in order (flicker-free at this list size).
- **Opening a chat no longer counts as "replied."** Caught-up % is direction-first now: a chat is "awaiting" when the last message is **not from us**, using WhatsApp's persisted message direction first and the rendered-row direction next; the unread marker (which clears the instant you open a chat) is only a last resort. A new **sticky-awaiting** rule keeps a chat marked awaiting until an outbound reply is actually observed, so opening an off-screen chat can't silently flip it to "caught up." (3 regression tests.)

### What's in v4.25.1

- **Cleaner AI insight strips:** the on-device model's output is now sanitized harder before it reaches a card. Previously a sentence ending in a quote (e.g. `…respond immediately." Next action steps: …`) slipped a verbose run-on and a stray quote into the strip. The sanitizer now cuts at the first sentence/clause boundary (`. ! ? ;`) regardless of a following quote, strips interior quotes, and enforces a hard word/character cap (the small model routinely ignores the prompt's length limit). Three regression tests added.

### What's in v4.25.0

- **WCAG 1.4.1 — non-color status cue:** each command-center card now shows a shape-distinct status glyph (✓ on track / ⚠ needs attention / ⨯ behind) next to the caught-up %, so health is communicated by **shape, not colour alone**. The glyph carries an accessible name and tooltip. Closes the §9 "colour + icon + text (never colour alone)" principle for the L0 cards (comfortable density).

### What's in v4.24.0

- **Shell modernization (closes the v4.24.0 increment of the UI/UX plan):**
  - **Title-bar scope selector:** the account-scope switch (All ∣ Professional ∣ Personal) moved out of the sidebar and into the title bar, per the §9 shell spec. It still appears only when both scopes have accounts, persists across sessions, and re-renders the rail immediately. The scope state stays owned by the sidebar; the title-bar control drives it through a small public API (`WorkspaceSidebar.SetScope` / `ScopeSelectorStateChanged`), so the rail's render logic was not disturbed.
  - **Title-bar AI toggle:** a one-click **AI** toggle in the title bar mirrors `AppSettings.EnableLocalAi` — flip on-device AI insight strips on/off without opening Settings. It stays in sync if the same setting is changed from Settings → AI, and degrades gracefully to heuristics when off or when Ollama is unavailable.
  - **Compact sidebar by default:** fresh installs now open with the 56px icon rail (`SidebarPinnedExpanded` defaults off); the title-bar pin button expands it. **Existing users keep their persisted expanded/compact preference** — the new default only applies to brand-new installs.

### What's in v4.23.0

- **Shell IA foundation:** core state and dialog infrastructure for the oversight-foundation branch:
  - `ShellViewState.WorkQueue` — new enum case; `WorkspaceSidebarHelper`, `WorkspaceSidebarViewModel`, and `MainWindowViewModel` all track work-queue selected state correctly so the sidebar selection ring follows navigation.
  - **SetLocationDialog** — `IsProfessional` accounts can now be grouped under named locations via the context-menu "Set location…" entry; existing locations auto-populate the editable combo.
  - **ConfirmPermanentDeleteDialog** — safety confirmation before permanent instance removal (replaces an inline `ContentDialog` build in `ShellController`).
  - **AutoUpdateDialog** — prompt shown by `GitHubUpdateService` when a newer installer version is detected; user can install now or defer.
  - **PinToTaskbarDialog** — one-time nudge (respects `HasPromptedPinToTaskbar` setting) to pin the app for quick access.
  - **MainWindow event-handler refactor:** all inline lambdas in `AttachShellHandlers` / `DetachShellHandlers` are now named methods so the detach pass actually removes the correct delegate (previously the detach was a no-op because lambda identity doesn't match).
  - **Sidebar drag-reorder removed:** OLE drag-in-ScrollViewer causes a WinUI freeze; the drag loop code and `_isDragging` guard have been removed. The `InstanceReorderRequested` event stub is preserved with `#pragma warning disable CS0067` for future re-wiring.
  - **ScopeFilterCombo compact-mode fix:** the scope filter combo was shown unconditionally in compact sidebar mode; it now follows the same `_isCompact` guard as other labels.

### What's in v4.22.0

- **Command-center visual modernization:** three coordinated improvements to make the dashboard closer to the designed target:
  - **Vertical card layout:** each account card now shows a colored avatar circle (initials + account accent color), a large bold caught-up %, and the sparkline stacked naturally rather than as a fixed-width horizontal row.
  - **Bar-chart sparklines:** the 7-day activity trend is now a bar chart (colored vertical bars, rounded tops) instead of a line polyline — more readable at a glance and color-matched to each account's health status.
  - **Urgent + dropped sub-metrics:** `UrgentCount` and `DroppedCount` were already computed in the oversight engine but never surfaced in the UI. They now appear as compact "N urgent / N dropped" labels under the % when non-zero.
  - **Jump button on the needs-attention banner:** when urgent customers are waiting, the banner now shows a "Jump" button that navigates directly to the most critical account's WebView.
  - **"Define locations" CTA:** when no workspace profiles are configured and the dashboard is in per-account mode, a one-line prompt offers a direct link to the Workspace Management settings section.
  - **Segmented grouping control:** the "By account / By location" toggle switch is replaced by two adjacent toggle buttons that read as a segmented control.
  - **Insight strip dark restyle:** the AI/heuristic insight strip uses a dark neutral surface (consistent regardless of alert severity) with an amber ✦ badge — severity is already communicated through the % color in the card header.

### What's in v4.21.0

- **Telegram + Meta Messenger embed channels (Phase 5 — embed slice):** "Add account" now offers **Telegram** (`web.telegram.org`) and **Messenger** (`messenger.com`) as branded channel options, each loading in its own isolated WebView session with a per-platform accent colour. Same embed-slice model as Google Business (v4.20.0) and generic web page (v4.19.0): the channels are fully usable for manual reading and replying, but **no adapter scraping** yet — conversation metrics (unread/awaiting) are not yet surfaced in oversight. Each will have its own adapter when a live logged-in account is available to tune the DOM reader against. Meta Messenger carries higher maintenance risk (Meta actively fights automation) so the adapter scope is passive read-only only.

### What's in v4.20.0

- **Google Business reviews channel (Phase 4 — embed slice):** "Add account" now offers **Google Business**, a branded channel that loads your Google Business reviews console (`business.google.com`) in its own isolated session — reviews one click away alongside your messaging accounts. **Scope note:** this is the *embed* slice. Automatic review-metric scraping (star rating, % responded, unanswered count surfaced in oversight) is the planned next step — it needs a live, logged-in Google Business account to build and tune the DOM reader against, so it ships separately rather than as unverified code. For now the channel routes to the no-op adapter (no oversight metrics).

### What's in v4.19.0

- **Generic web-page instances (Phase 3):** "Add account" now offers a **Web page** platform — enter any http/https URL and it's monitored in its own isolated WebView tab (own profile/session), just like a messaging account. No adapter scraping and **no oversight metrics** (it routes to the no-op adapter), so it's a lightweight way to keep a dashboard, booking page, or web tool one click away. The plumbing (custom-URL field, no-op adapter, generic chrome CSS) was already present; this registers the `generic` platform so it's selectable and no longer collapses to WhatsApp.

### What's in v4.18.0

- **Command-center filter + density (Phase 3 — scale):** a **filter box** in the command-center toolbar narrows the cards to accounts/locations whose name matches as you type (in By-location view, a location shows if its own name matches — all members — or only the members that match). A **Compact / Comfortable** toggle switches to denser rows (tighter padding, smaller %, sparkline and freshness label hidden) so a multi-location owner with many accounts can see more at once. No data changes — purely how the existing oversight rows are filtered and laid out.

### What's in v4.17.0

- **Local-AI insight strips (Phase 2):** when **Settings → AI** is enabled and the on-device Ollama runtime is reachable, each "needs attention" strip is re-phrased by the local model (`phi3:mini` by default) into a natural one-line assessment + next step, marked with a small **✦ AI** tag. It's **fully on-device** — only aggregate counts (waiting/unread/oldest-wait/caught-up %) are sent to the local model, never customer names or message text. Generation is background, cached per account by a state signature, and serialized so a burst of accounts doesn't hammer the runtime. If AI is off, still loading, or unreachable, the strip shows the instant **heuristic** line from v4.15.0 — so it never blocks or regresses.

### What's in v4.16.0

- **Idle-session reaper (track C — lifecycle/memory hardening):** the WebView concurrency cap was only enforced when a *new* account was opened, so briefly-visited accounts stayed live and held RAM indefinitely. A 1-minute timer now closes any non-visible session that's sat idle past `IdleSessionReapMinutes` (default 20). **Professional accounts are exempt** — they stay live so background oversight keeps reading them — and the **visible** account is never reaped. Closing a session doesn't sign it out (the profile's on-disk data persists); it reloads, still signed in, on next open. Set the minutes to 0 to disable. Complements the existing per-instance LRU cap, memory tiers, low background memory target, and 90s stale-adapter recovery.

### What's in v4.15.0

- **Command center insight strips (track B):** any account that needs attention now shows a one-line, plain-language summary under its health row — e.g. *"Needs attention — 5 customers are waiting on a reply · 3 unread · oldest 2 hrs ago."* The strip is **amber** when the account is still mostly caught up and **red** when it's falling behind; fully caught-up accounts stay quiet (no strip). It's a **deterministic, on-device heuristic** — instant, no cloud, no API, no AI runtime required, so it always works at zero cost. (Optional local-AI enhancement can layer on top later.)

### What's in v4.14.0

- **Command center visual polish (track A):** each account is now a proper **card** (rounded border, surface background) with a **status-colored accent bar**, a **large prominent caught-up %**, a 15px account name, and clearer awaiting labels ("needs reply" for read-but-unanswered chats instead of "0 unread").

### What's in v4.13.2

- **Groups/broadcasts excluded from oversight:** the replied-based "awaiting" introduced in 4.13.1 wrongly counted internal team groups (e.g. "Team Anfal", "Daily Branch Status") and broadcasts as awaiting a reply. Oversight now skips `@g.us` / `@broadcast` / `@newsletter` / status chats — only 1:1 customer conversations count.
- **Clean message previews:** the sidebar preview scrape now targets the message-text span and strips icon-token noise (`ic-imagePhoto`, `wds-ic-readYou`, `ic-push-pin`).
- **Better unsaved-contact names:** broader title extraction (primary cell + title attribute) so unsaved 1:1 chats show their number/name more often instead of "Unsaved contact".

### What's in v4.13.1

- **"Awaiting" now means not-replied, not just unread:** a chat where the **customer had the last word** counts as awaiting even after you open/read it. Derived from the last-message **direction** (the sidebar's sent-tick), falling back to the unread marker only when the chat isn't rendered. Fixes "it says caught up even though I haven't replied."
- **Phone numbers for unsaved contacts:** 1:1 chats with no saved name now show the number from the sidebar (was "Unsaved contact").
- **No more accordion flashing:** the command center skips its card rebuild when nothing changed (render change-detection), so the 20s auto-refresh no longer makes the lists flicker.

### What's in v4.13.0

- **Command center is the home surface (L0):** the Dashboard now *is* the command center (full width), with Personal Overview in a collapsible Expander.
- **Work Queue (L1):** the Operations Command Center moved to a dedicated **Work Queue** page with its own sidebar button (`Ctrl+Shift+Q`), header status, and refresh; its branch-filter / lane-focus / urgent-queue navigation commands route here.
- **Workspace Management in Settings:** a "Workspace management" section with a live summary and an "Open workspace manager" button (still reachable via `Ctrl+K`).
- **Guided cold-start onboarding:** a 4-step first-run wizard (Welcome → Add account → Set locations → Hours/SLA) with per-step skip, then optional follow-up dialogs. Hardened so a wizard hiccup never crashes startup or nags on every launch.

### What's in v4.12.4

- **Sidebar drag-reorder removed:** dragging a row inside the scrolling menu reliably froze the app (a WinUI drag-in-ScrollViewer issue that three targeted fixes couldn't resolve). Drag is disabled (`CanDrag=false`, drop target off). **Reorder accounts via the right-click "Move up / Move down" menu** (added in 4.12.3) — reliable and freeze-free.

### What's in v4.12.3

- **Real drag-freeze cause fixed:** the sidebar navigated to an account on **`PointerPressed`**, so the instant you pressed to start a drag it kicked off a heavy WebView switch on the UI thread → freeze. Navigation now happens on **`Tapped`** (a click without a drag), so dragging no longer triggers a switch.
- **Reliable Move up / Move down:** the account right-click menu gains drag-free reorder via `MoveInstanceAsync`, so repositioning always works regardless of drag.

### What's in v4.12.2

- **Drag-reorder hang fixed:** after the v4.12.1 crash fix, a drag could still freeze the app because frequent connection-status updates (accounts "Connecting…/syncing") called the sidebar's `Refresh` *during the live drag*, restructuring `MenuStack` and removing the dragged row out from under the OLE drag loop. The sidebar now tracks an `_isDragging` flag (set on `DragStarting`, cleared on `DropCompleted` and before the deferred reorder) and **skips the structural rebuild while a drag is in progress** — doing only safe content updates, then restructuring once the drag ends. Context-menu actions verified wired end-to-end.

### What's in v4.12.1

- **Drag-reorder crash fixed:** dragging a sidebar account to reposition it crashed natively because the reorder rebuilt the menu — removing the dragged element — *synchronously inside the drop event*. The reorder is now **deferred to the next dispatcher tick** so the drag-drop operation completes first. Both handlers are also exception-guarded.
- **Same class fixed on the OCC kanban board:** its drag-over accessed `DragUIOverride` without a null check and fired transfer/re-render events synchronously in the drop — now null-guarded, exception-safe, and deferred.

### What's in v4.12.0

- **Shell IA (step 3) — location rail:** right-click a professional account → **"Set location…"** to assign it a location (pick an existing one or type a new name; "Clear" to remove). Accounts sharing a location now appear under a **location sub-header** in the sidebar (single-account locations stay flat so the rail isn't cluttered) — and they already roll up together in the command center's By-location view. New lightweight `UpdateInstanceBranchKeyAsync` (metadata only, no session reload).

### What's in v4.11.1

- **Startup-crash hotfix:** the new scope-switch ComboBox fired its `SelectionChanged` during `InitializeComponent` (from an initial `IsSelected`), which ran the sidebar render before services were ready and crashed startup ("Cannot create instance of type WorkspaceSidebar"). The initial selection is removed and the handler is guarded until the sidebar's first real refresh.

### What's in v4.11.0

- **Shell IA (step 2) — scope switch:** when both Professional and Personal accounts exist, the sidebar shows an **All / Professional / Personal** selector that filters the account list to one scope (persisted). Hidden for single-scope setups so it never hides your only accounts.

### What's in v4.10.0

- **Shell IA (step 1) — scope-grouped sidebar:** the account list now splits into **Professional** and **Personal** sections (when both exist) instead of one flat "Active accounts" list, making the Personal/Professional scope first-class in navigation. A single-scope setup keeps one clean header. Foundation for the location rail and scope switch.

### What's in v4.9.3

- **Audit fixes:** By-location accordions now keep their expanded/collapsed state across the 20s auto-refresh (instance rows already did; locations didn't). The IndexedDB scan is now serialized per instance, so the background monitor and a manual Re-sync can't clobber each other's shared result.

### What's in v4.9.2

- **"Since you were last here" digest (A):** once per session, the command center summarizes what's waiting — *"Since Jun 18, 9:14 AM: 7 new awaiting reply · 21 total across 2 accounts · oldest since…"* — using a persisted last-seen timestamp. (`OversightChatSnapshotService.BuildDigest`.)
- **Hardened chat-store read (B):** the IndexedDB scan watchdog now allows 20s (was 8s) so a busy account's `getAll` over thousands of chats completes instead of timing out into "syncing…".
- **Configurable alert threshold (C):** Workspace management (Ctrl+K) now has an **"Alert when awaiting reply reaches N"** setting (0 = off, default 5); the background monitor reads it each pass.

### What's in v4.9.1

- **Custom date range:** the command-center window selector adds **"Custom range"**, revealing **From/To** calendar pickers. Caught-up % and the awaiting list are then scoped to chats active in that range (To is inclusive through end-of-day). `OversightWindow.Custom` + `windowEndUtc` plumbed through the snapshot queries.
- **More robust message preview:** the awaiting-list glimpse now scrapes the sidebar's secondary cell with broader selectors and falls back to matching by chat **title** when the row `data-id` doesn't line up with the chat id — so previews show for more chats.

### What's in v4.9.0

- **Proactive awaiting-reply alerts:** a background monitor re-reads each connected professional account's unread snapshot every ~3 minutes and raises a **desktop toast** when an account's awaiting-reply count crosses a threshold (default 5) — edge-triggered so it won't spam. This also keeps the command-center numbers fresh between manual re-syncs. (`OversightAlertMonitor` + `OversightSnapshotReader`.)
- **Message glimpse in the awaiting list:** each waiting chat now shows a one-line preview of its last message (scraped from the sidebar, since WhatsApp Web doesn't persist `lastMessage` in the chat store), so you can triage who to answer first without opening each chat.

### What's in v4.8.9

- **Header and awaiting list can no longer disagree:** previously an account whose chat-store read hadn't landed showed thread-based numbers in the header ("21 awaiting reply") while the accordion — driven only by the unread snapshot — was empty. Now an account with no chat data reads **"syncing…"** with a matching empty list, so the headline always reflects the actual waiting customers.

### What's in v4.8.8

- **Click-through focus fixed:** opening a chat from the awaiting list now matches the sidebar row by its **`data-id` (JID)** rather than the visible title — so chats whose internal id never appears in the title text (especially WhatsApp `@lid` privacy ids) focus correctly instead of failing with "could not focus the requested chat".
- **Honest contact labels:** only real phone ids (`@c.us`) render as a `+number`; WhatsApp privacy ids (`@lid`) show "Unsaved contact" instead of a fake 15-digit "number".

### What's in v4.8.7

- **Readable names in the awaiting list:** unsaved WhatsApp contacts (which the chat store titles generically as "New message") now show the **phone number derived from the chat JID** (e.g. "+92332…"), so every waiting customer is identifiable.

### What's in v4.8.6

- **Awaiting-reply list is now an inline accordion** under each account card (not a popup): expand a row to reveal the waiting customers (name + unread, worst-first); click one to open that chat. Header click no longer navigates away, and expanded rows stay open across the auto-refresh.

### What's in v4.8.5

- **"Awaiting reply" is now click-through:** clicking the count opens a flyout listing the actual customers waiting (name + unread count, worst-first), scoped to the date window and aggregated across a location's accounts. Click any entry to jump straight into that WhatsApp conversation. The snapshot now keeps each chat's JID + name for this.

### What's in v4.8.4

- **Exact "N awaiting reply" count** next to each account's caught-up % — the number of chats with unread customer messages (not yet responded to) within the selected date window. Replaces the stale thread-based urgent/dropped columns with the actionable number that matches the metric.

### What's in v4.8.3

- **The date filter now works on the caught-up metric.** The chat-store snapshot keeps each chat's last-activity time, so Today / Last 7 days / All time scope the % to conversations *active in that window* (e.g. "of the chats active today, how many are caught up"). An account with no chats active in the window reads "no activity" rather than a stale number.

### What's in v4.8.2

- **Cleaner command center:** removed the now-inert date-window selector (caught-up % is a live signal, so the window didn't change it) and relabeled the headline to "caught up".
- **Resilient first-load probe:** the IndexedDB scan now self-settles via a watchdog and the Re-sync probe retries, so an account whose WhatsApp Web is still loading no longer shows a hard timeout — it resolves on a later pass.

### What's in v4.8.1

- **Trustworthy on-time = "caught up %":** the command center now derives each account's headline number from **WhatsApp's own unread marker**, read directly from the chat store in local IndexedDB (chats with no unread customer message = caught up). This needs no message history and no fragile name matching, so it reflects reality even when the app's reconstructed thread list is stale — fixing the misleading near-0% readings.
- **Why:** WhatsApp Web (multi-device) keeps only a small recent cache in the `message` store and does not persist per-chat `lastMessage`, but `unreadCount` is reliable for every chat. Reading the `chat` store with a single bounded `getAll` also avoids the long-cursor read-transaction hang that the `message` store caused.
- **Manual "Re-sync history"** button refreshes the snapshot on demand; the regular startup backfill keeps it current. Also fixes a WebView2 plumbing bug (ExecuteScriptAsync does not await promises) that made the IndexedDB read silently return nothing.

### What's in v4.8.0

- **Command center is now the default landing tab**, with auto-refresh (20s) and per-row 7-day activity sparklines.
- **Date-windowed on-time** (Today / Last 7 days / All time, default Today): responsiveness is measured over conversations active in the window — including messages that arrived before the account was connected today — while older open conversations are surfaced as carried backlog ("from history") instead of saturating the number.
- **Per-account location rollup:** By-location groups each account into exactly one location (no more split accounts) and never leaks a raw branch id / instance GUID as a location name.
- **Robust backfill from IndexedDB:** history is read straight from WhatsApp Web's local `model-storage` (stable chat JIDs for every conversation, no DOM walking), replacing the bounded 3-chat DOM scroll. **Reconciliation** migrates legacy title-keyed threads to their stable JID and marks conversations whose last message is from you as **answered**, so on-time reflects what was actually replied. `OversightWindow` + `ReconcileConversationKey` (new unit tests).

### What's in v4.7.0

- **Oversight redesign foundation (master plan Phase 1):** a new **Command center** dashboard tab showing per-account / per-location health (on-time %, urgent, dropped, freshness) sorted worst-first, with a needs-attention banner, By-account↔By-location toggle, and collapsible location accordions revealing member accounts. **Workspace Management** (`Ctrl+K → Manage workspaces`) sets per-location SLA targets + business hours (the SLA clock pauses outside working hours). **Drill-down:** click an account row to open its WhatsApp view. Backed by `OversightRollupBuilder` + `OversightService` (11 new unit tests). See `docs/MASTER-PLAN.md`.

### What's in v4.6.0

- **P1 UX pass (UI/UX audit):** KPI strip now sits **above** the date-range/volume card (work surfaces sooner); the empty volume panel shows a **"Sync message history" CTA** instead of a dead end; the Live/Historical control has an explicit **"View mode"** label; thread cards add **non-color SLA glyphs** (⚠ breached / ⏱ approaching) for WCAG 1.4.1; and the card action reads **"Open chat →"**. All five P1 items shipped and verified.

### What's in v4.5.0

- **SLA metric integrity (UI/UX audit P0-3):** Backfilled/historical threads are no longer counted as SLA breaches — the SLA clock applies only to threads observed live after connect. Added an **at-risk** warning window (≥50% of the threshold) and a **carried-over-from-history** count, so the OCC headline numbers reflect the real live workload instead of reading "all open exceed SLA". Decision recorded: the app stays on WhatsApp Web and **does not use Meta/WhatsApp APIs**.
- **CI asset guard:** the build now fails if runtime assets (`Assets\AppIcon.ico`, branding) are missing from output, preventing the class of bug behind the v4.4.0 tray crash.
- **Empty-state copy:** clearer, directional guidance on the message-volume panel.
- See `docs/ui-ux-research-and-recommendations.md` for the full audit and the sequenced remainder.

### What's in v4.4.0

- **Launch-stability hardening:** fixed three startup/early-runtime crashes — a filter-chip null-reference during OCC XAML load, a taskbar-pin WinRT call that is unavailable in unpackaged builds, and a fatal tray-icon load when bundled assets were missing.
- **Asset packaging fix:** `AppIcon.ico` and brand wordmark images are now copied to the publish/install output (`CopyToOutputDirectory`); the sidebar wordmark and tray icon render correctly.
- **Update integrity:** installer verification now performs full Authenticode policy validation via `WinVerifyTrust` (chain + trust, not signature-presence only), with an optional publisher pin and existing SHA-256 sidecar check.
- **Installer path fix:** `installer.iss` / `installer-arm64.iss` read publish output from `bin\<Platform>\Release\...`, preventing stale XBF/DLL packaging.

### What's in v3.7.0

- **Settings-only Ollama:** Lite installer (~66 MB); no embedded Ollama zip. Runtime downloads on Settings › AI enable with size disclosure and progress UI.
- **Wave 0 UX honesty:** Thread cards show heuristic previews and source badges (Heuristic / AI / Analyzing…) instead of misleading "Awaiting AI" copy.
- **Local Ollama AI:** Settings › AI section (enable toggle, download runtime, endpoint, model picker, test connection, pull progress); optional OCC header chip (AI ready / AI offline).
- **Inference pipeline:** Heuristic-first triage with bounded AI enrichment for top urgent live threads via OllamaSharp (gated until runtime is running).

### What's in v3.4.0

- **WhatsApp startup backfill (P0?P3):** Re-wired `BackfillSyncManager` + `WhatsAppBackfillProvider` after connect; IndexedDB candidate collection with unread/recent/all modes; conversation+day dedupe store; triage enqueue + `RecordBackfillInbound` + thread registry timestamps.
- **P1 metadata:** Message-store daily sent/received aggregates (no decryption); sidebar snapshot ingress.
- **P2 scroll-back:** Open-chat history chunk collection; OCC backfill status caption (`UmBrandTealDarkBrush`).
- **P3 deep backfill (MVP):** Opt-in bounded sidebar walk (max 3 chats); full async automation deferred.
- **Settings:** Startup backfill toggle, mode, max chats, recent window, deep backfill opt-in.

### What's in v3.3.0

- **Phase 10+ audit completion:** Personal Overview binds ViewModel `ObservableCollection`s directly (list virtualization restored); high-contrast theme support via system detection; cross-column kanban drag updates thread status and persists to `triage_v2.json`.
- **OCC UI polish:** Metric cards, thread cards, kanban, message-volume chart, and workspace sidebar token pass (uncommitted polish merged).

### What's in v3.2.1

- **Startup fix:** Adapter script preload before WebView2 COM calls during `WarmAll` startup (cross-thread registration); rebuild installers for installed users.
### What's in v3.2.0

- **Ultimate audit remediation:** Persist triage, thread registry, and kanban display order to `triage_v2.json`; doc reconciliation; OCC keyboard reorder (`Alt+Up/Down`, `Escape`).
- **Dead code removal:** Global hotkey service, legacy multi-platform connection handshake profiles, unused `AwaitingLocalAi` enum.
- **UX & ops:** Command palette thread search, first-run Personal vs Professional onboarding, HTTPS-only WebView navigation, default startup warm mode `VisibleOnly`.
- **Tests:** Triage persistence round-trip + kanban keyboard reorder unit tests.

### What's in v3.1.5

- **UI hyper-loop polish:** Design-token pass across Operations Command Center, Personal Overview, kanban, message-volume chart, metric/thread cards, and workspace sidebar; shared scroll-offset preservation for list refresh stability.
- **Token cleanup:** Command palette modal scrim, notification feed typography, and sidebar compact padding wired to theme tokens.
### What's in v3.1.4

- **Hyper-loop audit fixes:** Stop WhatsApp telemetry from double-counting analytics, ignore orphan branch keys in OCC pills, guard OCC date-range picker races and unload leaks, reuse message-volume chart geometries, and clear telemetry timers on adapter dispose.
- **Tests:** Two regression tests for branch-key collection and telemetry analytics isolation.

### What's in v3.1.3

- **Full branding refresh:** Gradient app icon plus UNIFIED MESSENGER wordmark on About and sidebar; brand blue accent tokens (#1B75BB?#2E3191).
- **Audit fixes:** Removed dead copilot hotkey registration, fixed CI benchmark gate, refreshed UiSmoke OCC probes.

### What?s in v3.1.2

- **Updated branding:** Gradient four-bubble app icon applied across shell, tray, toasts, About page, and installers.

### What?s in v3.1.1

- **Startup fix:** Light/Dark theme no longer crashes launch when applied before the main window is created.

### What?s in v3.1.0

- **Dashboard overhaul:** OCC date-range filtering, message-volume trend chart, deeper WhatsApp telemetry ingress.
- **Sidebar UX:** Compact status labels, WhatsApp-focused instance list, improved truncation and tooltips.
- **Workspace purge:** Removed legacy multi-platform adapters, Ollama/AI stack, and obsolete tests/docs.
- **534** unit tests (x64, Release); trimmed UiSmoke harness (sidebar, OCC, Personal, settings, notifications).

## Architecture

```
???????????????????????????????????????????????????????????????
?  Shell (MainWindow + WorkspaceSidebar + Notification Hub) ?
?????????????????????????????????????????????????????????????
? WhatsApp     ? Dashboard                ? Settings / About?
? instances    ?  ?? Operations (OCC)     ?                 ?
? (WebView2    ?  ?? Personal Overview    ?                 ?
?  profiles)   ?                          ?                 ?
?????????????????????????????????????????????????????????????
         ? DOM scripts (adapter-core, whatsapp-adapter)
         ? WebMessage bridge
         ?
   Notification + triage services (heuristic, local JSON stores)
```

- **WebView2:** One shared environment / user-data folder; isolated `ProfileName` per account.
- **OCC:** Fixed panels ? KPI strip, branch workspace pills, immediate queue, kanban columns ? fed by heuristic triage and thread registry.
- **Notifications:** DOM-scraped counts and alerts merged into a single native feed.

## Build from source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 x64 or ARM64
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (for installers)

### 1. Build

```powershell
dotnet build UnifiedMessenger.sln -c Release -p:Platform=x64
```

### 2. Publish + installer

```powershell
dotnet publish UnifiedMessenger\UnifiedMessenger.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o UnifiedMessenger\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```

> Note: `installer.iss` / `installer-arm64.iss` read the publish output from `bin\<Platform>\Release\...\publish` (i.e. `bin\x64\Release\...` and `bin\ARM64\Release\...`). Keep the `-o` path above in sync with the installer's `PublishDir`, or omit `-o` and let `-p:Platform=x64` place the output there automatically.

ARM64: repeat with `-r win-arm64`, `-p:Platform=ARM64`, the matching `bin\ARM64\Release\...` output path, and `installer-arm64.iss`.

Outputs land in `dist\`:

- `dist\UnifiedMessengerSetup.exe` (x64)
- `dist\UnifiedMessengerSetup-arm64.exe` (ARM64)

### 3. Run tests

```powershell
dotnet test UnifiedMessenger.Tests\UnifiedMessenger.Tests.csproj -p:Platform=x64 -c Release
```

### 4. UI smoke validation (optional)

```powershell
dotnet publish UnifiedMessenger\UnifiedMessenger.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o UnifiedMessenger\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish
dotnet run --project UnifiedMessenger.UiSmokeTests\UnifiedMessenger.UiSmokeTests.csproj -c Release -- "UnifiedMessenger\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\UnifiedMessenger.exe"
```

## CI/CD

GitHub Actions (`.github/workflows/build.yml`):

1. **verify** ? build + unit tests (Release, x64)
2. **package** ? publish win-x64 and win-arm64, compile Inno Setup, SHA-256 sidecars
3. **ui-smoke** ? FlaUI harness against published x64 binary
4. **release** ? tag `v*` only; attaches CI-built installers to GitHub Releases

Push tag `v3.1.5` to publish a release. Pushing to `main` alone updates source but not the Releases page.

## Auto-update

`GitHubUpdateService` checks GitHub Releases on startup. When a newer tag is available, it downloads the matching installer, verifies Authenticode (and SHA-256 when published), and runs a silent Inno install. Control behavior under **Settings ? Updates**.

## Keyboard shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+D | Dashboard |
| Ctrl+, (comma) | Settings |
| Ctrl+Shift+N | Toggle notification panel |
| Ctrl+K | Command palette |
| Ctrl+1?9 | Switch to instance (sidebar order) |

## User data

| Data | Location |
|------|----------|
| Instance registry | `%LocalAppData%\UnifiedMessenger\instances.json` |
| Analytics | `%LocalAppData%\UnifiedMessenger\analytics.json` |
| Triage | `%LocalAppData%\UnifiedMessenger\triage_v2.json` |
| Settings | `%LocalAppData%\UnifiedMessenger\settings.json` |
| WebView profiles | `%LocalAppData%\UnifiedMessenger\WebView2\` |

Legacy `instances.json` entries with removed platform IDs are migrated to WhatsApp on first load after v3.0.0. Legacy settings keys from pre-lite builds are dropped when settings are re-saved.

## License

See repository license file for terms.
