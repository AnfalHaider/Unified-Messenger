# PROMPT — Rebuild Unified Messenger's UI to match the team's reference vision

> Copy everything below this line into a fresh Claude Code session started in
> `D:\Projects\Unified Messenger`. Attach the 9 reference screenshots to the same message.

---

## Mission

Transform Unified Messenger's UI/UX to match the team's reference mockups (9 screenshots attached):
a modern command-center product with a **section-based left navigation** (Dashboard, Inbox, Analytics,
Reviews, Reports, Accounts, Notifications, Settings), consistent KPI cards with week-over-week deltas,
a full chart suite (bar, area/line, donut, sparkline), a read-only unified Inbox, and dedicated
Analytics / Reviews / Reports / Notifications pages — in both light and dark theme.

You may refactor, move, or delete any existing code. The existing **data/services layer is good and
must be reused** — this is a presentation-layer rebuild, not a data rewrite. Read `AGENTS.md` first
and treat it as law; read `docs/ux-modernization-plan.md` for the design-system baseline.

## Non-negotiable constraints (from AGENTS.md — these override the mockups)

The mockups were drawn by a design team without knowledge of the product's hard constraints. Where a
mockup element conflicts with a constraint, build the **adapted** version below. Never build the
literal mockup version of a dropped item.

1. **Fully local, zero cloud, no APIs.** All data comes from the existing WebView2 scraping pipeline
   and on-device stores. All AI stays on Ollama.
2. **The app never sends anything.** Read-only scraping only. No composer that sends, no reply
   automation. "Reply" actions may only **deep-link/focus the real web client** (WhatsApp Web /
   Google Business page inside the app's WebView) where the owner types manually.
3. **Single-user oversight tool. No roles, teams, or permissions.**
4. No unofficial protocol libraries. WebView2 + injected JS only.

### Mockup-element verdict table (build / adapt / drop)

| Mockup element | Verdict | Reason / adaptation |
|---|---|---|
| Section left-nav (Dashboard…Settings) | **BUILD** | Core of the vision. |
| KPI cards + vs-last-week deltas | **BUILD** | Data exists (`KpiTrendStore`, `ResponseTimeTracker`, `MessageAnalyticsService`). |
| Bar / area / donut charts | **BUILD** | New reusable chart controls needed. |
| Inbox conversation list + transcript | **ADAPT** | Read-only. Transcript from `TranscriptBuilder`/`ContactHistoryStore`. |
| "Type a message…" composer + send | **ADAPT** | Replace with a prominent **"Open in WhatsApp →"** button that focuses the chat in the account's WebView (`ConversationFocusHelper`). |
| Open/status dropdown, Labels | **ADAPT** | Local-only chat status (Open/Handled/Snoozed via `AwaitingOverrideStore`) and local labels (new small store). |
| "Assign to Fahad", Team, mentions, "assigned to you" notifications | **DROP** | Single-user constraint. |
| Users & Roles tab, 2FA, Role-Based Access, Active Sessions | **DROP** | Single-user, local app. Replace Security card with "Local data & privacy" (everything stays on this PC). |
| Review "Reply" button | **ADAPT** | Deep-link to the review in the embedded Google Business web client (click-through already shipped v4.49.0). |
| Avg rating 4.7 / Total reviews 971 KPIs | **ADAPT** | Google's embedded surface doesn't expose rating/total (documented in AGENTS.md Phase 4). Show what `GoogleReviewSnapshotService` provides (unanswered, reply rate, actionable reviews); add rating/total only if the live DOM verifiably exposes them — never fabricate. |
| Q&A section (Reviews page) | **ADAPT** | Best-effort scrape; hide section when no data. |
| Instagram/Messenger/Telegram "Connected" tiles | **ADAPT** | Show real per-account connection state from `InstanceConnectionStatusService`; embeds exist, metric scrapers are pending (#24) — badge them "Embed only / metrics pending", never fake "Connected + metrics". |
| Workspace language/timezone/date-format settings | **ADAPT** | Keep what exists (workspace name exists via `WorkspaceManagementDialog`); add date/time-format + first-day-of-week only if genuinely wired into the charts/reports. No fake settings. |
| "Fahal/Fahad Admin" user footer | **DROP** | No user accounts. Footer may show workspace name instead. |
| Mobile phone mockups | **DROP** | Windows desktop app. The narrow-window responsive layout should, however, roughly follow the phone layout (stacked KPI grid 2×2). |
| Export as PDF | **ADAPT** | Ship CSV (exists) + Markdown (exists) + printable HTML → user prints to PDF. No cloud render. |

## What already exists — reuse, don't rebuild

Read these before writing any code:

- **Shell**: `MainWindow.xaml` (+ `MainWindowShellLayout`, `ShellController`, `ShellNavigationCoordinator`,
  `ShellNavigationService`) — title-bar scope selector, 320px `WorkspaceSidebar` (account list), right
  notification dock, `ContentFrame`, per-account WebView host with browser nav bar, command palette.
- **Dashboard**: `Pages/DashboardPage.xaml` hosts `CommandCenterPanel` (≈3k-line imperative card builder:
  KPI band, account cards, needs-reply list, AI insight strips), `ActivityPatternsPanel` (filterable
  hour/day/month bar chart), `ReviewHealthPanel`, `PersonalOverviewPanel` (flyout).
- **Chart primitives**: `Controls/Shared/MiniSparkline.cs`, `Controls/MessageVolumeLineChart.*`,
  `Services/ChartPalette.cs` (per-account stable colours).
- **Data services (all shipped and tested)**: `ResponseTimeTracker` (First Response Time, SLA met %,
  answered today), `MessageAnalyticsService` (~400-day per-account activity history, day×hour matrix),
  `KpiTrendStore` (KPI history for deltas/sparklines), `AwaitingOverrideStore` (mark-handled/snooze),
  `GoogleReviewSnapshotService`, `BusinessReport` + `DashboardReportHelper` + `WeeklyReportDialog`
  (weekly report, anomalies, .md/.csv export), `NotificationHub` + `NotificationFeedPanel`,
  `TranscriptBuilder`, `ContactHistoryStore`, `ThreadRegistryService`, `OversightService` /
  `OversightRollupBuilder` / `OversightEntityHealth`, `QuietHours`, `BusinessHoursCalculator`,
  `OccDateRangeFilterHelper`/`State` (date-range filtering), `ThemeService` + `Themes/Tokens.xaml`
  design tokens, `UmSemanticBrushes/Colors`.
- **Settings**: one `SettingsPage.xaml` (51KB) + 8 partial classes, already tabbed by
  `SettingsNavigationHelper`.
- **Docs**: `docs/ux-modernization-plan.md` (typography/hierarchy critique — implement its
  recommendations as part of this work), `docs/architecture/adr/*`, `docs/design-system/*`.

## Gap analysis → work packages

Deliver as **sequenced increments**, one commit + version bump each, following the repo's commit
convention (`vX.Y.Z: … (Phase N — slice) (Increment NN)`) and the 4-file version sync
(csproj / app.manifest / installer-shared.iss / README.md + `docs/phase-status.md`).

### WP0 — Navigation shell (the enabler; do first)
Replace the account-list-only sidebar with the mockups' **section navigation** while keeping accounts
reachable:
- New left rail sections: **Dashboard, Inbox, Analytics, Reviews, Reports, Accounts, Notifications,
  Settings** — icons per mockup, InfoBadge on Inbox (needs-reply count) and Notifications (unread).
- **Accounts** section = the current account list + WebViews (the existing `WorkspaceSidebar` account
  groups move here, or the rail gets an expandable Accounts group). Right-click menus, scope selector,
  location groups, add-account must all keep working.
- Route via the existing `ShellNavigationCoordinator`/`ContentFrame`; new `Pages/*` for each section.
- Keep keyboard shortcuts + command palette working; register new destinations there.
- Persist last-visited section; default to Dashboard.

### WP1 — Chart control suite (shared primitives; do second)
Build reusable, theme-aware WinUI controls in `Controls/Charts/` (pure XAML/`Shapes`/composition — no
external chart libraries, keep zero-dependency constraint):
- `BarChartView` — labeled X axis (day/date), Y gridlines with K-formatting, hover tooltips, optional
  stacked-by-account mode (refactor/absorb `ActivityPatternsPanel`'s internal renderer).
- `AreaLineChartView` — smooth line + gradient fill, axis labels, min/max scaling (Response-time and
  Replies-15m charts; absorb `MessageVolumeLineChart`).
- `DonutChartView` — segments + centre label + legend with % (Channel distribution, SLA performance).
- `DeltaBadge` — the "↓23% vs last week" red/green chip, colour-aware of metric polarity (down is
  **good** for response time, **bad** for messages? No — volume down is neutral/red per mockup; encode
  polarity per metric explicitly).
- `KpiStatCard` — icon chip + label + big value + `DeltaBadge` + optional sparkline; replaces ad-hoc
  KPI tiles.
- All must render correctly in **light and dark** theme (mockups show both) using `Tokens.xaml` +
  `ChartPalette`; follow WCAG non-colour-alone cues already standard in this repo.
- Before building charts, load the **`dataviz` skill** for form/colour rules, then apply repo tokens.

### WP2 — Dashboard page (mockups 1–3)
Rebuild `DashboardPage` composition to the mockup layout, keeping `CommandCenterPanel`'s logic:
- Header: page title + **account-scope dropdown** + **date-range dropdown** ("This week (7 days)",
  etc. — reuse `OccDateRangeFilterHelper`) + bell shortcut to Notifications.
- KPI row (4 `KpiStatCard`): Messages, Response Time, Replies (15m), SLA Met — each with vs-prior-period
  delta from `KpiTrendStore`/`BusinessReport` comparative logic.
- Overview bar chart with metric picker (Messages ▾) — `BarChartView` fed by `MessageAnalyticsService`.
- **Top Performing Accounts** card: rank accounts by a composite score (reuse
  `OversightRollupBuilder`'s health ordering inverted — document the score formula in code), platform
  icon + name + channel + %; "View all" → Accounts/Analytics.
- **Customers waiting for reply** card: big awaiting count (current-state semantics from v4.51.0 —
  do not regress to date-windowed) + trend sparkline; click → needs-reply list (existing behaviour).
- **Channel distribution** `DonutChartView` (message share per platform from analytics history).
- Keep: account cards grid / needs-reply drill-down / AI insight strips / mark-handled + snooze —
  reachable from the Dashboard (e.g. below the mockup rows or via the account cards section). Do not
  delete shipped capabilities to match a picture; integrate them.
- Preserve empty/loading/stale states (`DashboardCardEmptyStateHelper`, skeletons) for every new card.

### WP3 — Analytics page (mockup 4)
New `Pages/AnalyticsPage`: header (account filter incl. "All WhatsApp Accounts", range, **Export** →
CSV of the charted series), KPI row (icon-chip variant), 2×2 chart grid — Messages Over Time (bar),
Average Response Time (area, minutes), SLA Performance (donut: met/missed/no-SLA — derive "no SLA"
from threads without tracked FRT), Replies Within 15 Minutes (area, %). Per-card range pickers.
Data: `MessageAnalyticsService` + `ResponseTimeTracker` + `KpiTrendStore`. If a series genuinely has
no history yet (e.g. SLA before tracking started), show the standard empty-state, never sample data.

### WP4 — Inbox page (mockup 5) — read-only, this is the sensitive one
New `Pages/InboxPage`, three columns:
- **List**: search box, filter chips (All / per-platform with counts), pinned + recent grouping,
  rows = avatar, name/phone (`OversightThreadEnricher` phone resolution), preview (store-bridge
  previews), timestamp, unread badge. Source: `ThreadRegistryService` + oversight snapshot.
- **Transcript**: read-only bubbles from `ContactHistoryStore`/`TranscriptBuilder` (locally captured
  history only — state clearly "captured history; open the app for full thread"). Header: contact,
  phone, channel badge. **Primary action: "Open in WhatsApp →"** (focus chat via
  `ConversationFocusHelper` in the account's WebView). **No composer. No send. Ever.**
- **Details rail**: first/last seen, totals (from `ContactHistoryStore`), local status
  (Open/Handled/Snoozed → `AwaitingOverrideStore`), local labels (new tiny persisted store,
  follow the pattern of `AwaitingOverrideStore`), SLA state chip (within/past target from
  `ResponseTimeTracker` target), Notes tab (local free text per contact).
- Drop: Assign-to, language/location guesses.

### WP5 — Reviews page (mockup 6)
Promote `ReviewHealthPanel` into `Pages/ReviewsPage`: KPI row limited to **truthful** metrics
(new/actionable reviews, responded %, unanswered count; rating/total only if the DOM exposes them —
verify against the live embed before claiming), Recent Reviews list (reviewer, snippet, time-ago,
**"Open review →"** deep-link), Top Locations (per-account review health), Q&A section behind a
has-data gate. Location filter + range picker + CSV export.

### WP6 — Reports page (mockup 7)
Promote `WeeklyReportDialog`/`BusinessReport` into `Pages/ReportsPage`: arbitrary **date-range picker**
(extend `BusinessReport` beyond fixed weekly if needed), location/account filter, Generate button,
KPI row with vs-prior-period deltas, Messages-over-time by date, Channel-distribution donut,
**Top Performing Accounts table** (Account / Messages / Avg response / SLA met / Replies-15m — add
review column only if real), anomaly call-outs (existing), Export card: **CSV, Markdown, printable
HTML** (opens in default browser for print-to-PDF).

### WP7 — Notifications page (mockup 8)
Promote the dock's `NotificationFeedPanel` into `Pages/NotificationsPage`: filter chips
(All / Unread / Reviews / SLA alerts / System — no Mentions), master-detail layout (row list + detail
pane with preview, **Open Conversation** via existing `NotificationNavigationHelper`, Mark as read /
Mark all as read), gear → notification settings. Keep the compact dock working (it's the ambient
surface; the page is the full surface) or explicitly retire the dock — decide, document in an ADR,
and delete dead code if retired.

### WP8 — Settings restructure (mockup 9)
Reorganize `SettingsPage` tabs to: **General** (workspace, appearance/theme, formats),
**Channels & Accounts** (integration tiles with real status + Manage → account settings),
**Notifications** (existing toggles + quiet hours), **Business Hours & SLA** (surface
`BusinessHoursCalculator` config + FRT target editing), **Data & Privacy** (backup/export — exists via
`LocalBackupService`, retention, delete-all with confirm, store-bridge health line stays), **AI**
(existing Ollama section), **System** (existing: startup, updates, session/memory). Card-based visual
style per mockup. Keep every existing setting reachable — audit the 8 partials and map each control to
a new home before moving anything; delete only genuinely dead settings.

### WP9 — Design-system pass (runs through all WPs, finalize last)
Implement the already-written recommendations in `docs/ux-modernization-plan.md`: larger type scale
with a hero number, elevation/depth on key surfaces, restrained banners, one colour system. Verify
both themes end-to-end. Then run the **`frontend-design-audit:evaluate`** skill on the finished UI and
fix severity 3–4 findings.

## Execution & multi-agent orchestration

- **Phase A — recon (parallel, read-only):** spawn parallel `Explore` agents: (1) full inventory of
  `CommandCenterPanel.xaml.cs` internals (what to extract into shared controls), (2) Settings partials →
  control-to-new-tab mapping, (3) `NotificationHub`/feed data shapes, (4) transcript/contact-history
  data availability for the Inbox. Then one `Plan` agent to sequence increments and flag risky
  refactors (especially WP0 shell rewiring and absorbing `ActivityPatternsPanel`).
- **Phase B — implementation: sequential increments** in the main checkout, one WP (or WP-slice) per
  increment/commit. Do **not** parallelize XAML/csproj edits across worktree agents — the single
  project file, version-sync ritual, and imperative UI builders make merges pathological. Parallelism
  is for research and review, not for concurrent WinUI edits.
- After each increment: build → targeted tests → publish `-p:Platform=x64` → kill stale process →
  smoke test ALIVE → visual check → commit + version bump. Use the repo's exact commands in AGENTS.md.
- **Review:** after WP2 and again at the end, run `/code-review`; run the `simplify` skill after the
  big extractions (WP1/WP2) to de-duplicate the imperative builders.
- Write an **ADR** (`docs/architecture/adr/`) for: the section-nav shell (WP0), the read-only Inbox
  decision (WP4), and the notification dock's fate (WP7). Update `docs/phase-status.md` and
  `docs/remaining-work.md` at the end.

## Testing requirements (every increment)

- xUnit tests for every new pure/service class (chart data shaping, ranking score, label store,
  report range logic) — follow existing helper-class test patterns; **targeted `--filter` with exact
  class names only** (headless-hang rule in AGENTS.md).
- UI wiring verified by launching the published exe (single-instance mutex: always
  `Stop-Process -Name UnifiedMessenger -Force` first). Screenshot each finished page in **both
  themes** and compare against the mockups.
- No regression to shipped behaviours: current-state awaiting semantics, mark-handled/snooze,
  needs-reply drill-down, AI insight strips, re-sync flow, quiet hours, taskbar badge, tray.
- Never invent data to make a chart look like the mockup. Empty state > fake numbers.

## Definition of done

Every mockup surface has a real counterpart (per the verdict table), light + dark themes match the
reference feel, all charts render from real local data with honest empty states, nav/badges/command
palette work, all constraints intact (zero network egress beyond existing web clients + Ollama +
GitHub update check; zero send paths), tests green, versions synced, installer builds, smoke test
ALIVE, docs/ADRs updated.
