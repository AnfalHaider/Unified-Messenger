# Unified Messenger — Enhancement Roadmap

> **Scope:** All optional enhancements **except** Instance & session management (Path C): lazy WebView loading, per-instance sleep/unload, startup warm mode settings, edit URL/platform after creation, import/export `instances.json`, instance notes/tags.

**Legend:** **S** = small (≤1 day), **M** = medium (2–4 days), **L** = large (1+ week).  
**Agent types:** `ui-shell` · `notifications` · `dashboard` · `adapters` · `platform` · `devops`

---

## Phase Overview

| Phase | Theme | Goal |
|-------|--------|------|
| **1** | UX polish & dev ergonomics | Sidebar, shortcuts, notification quick wins, smoother dev loop |
| **2** | Hub & dashboard depth | Rich notifications, analytics charts, command palette |
| **3** | Platforms & performance | New adapters, suspend/cap, resource visibility |
| **4** | System integration & shipping | Badges, startup, CI/CD, auto-update, export |

---

## Phase 1 — UX Polish & Dev Ergonomics

### UI & Shell

| ID | Task | Files | Deps | Cx | Agent | Acceptance |
|----|------|-------|------|-----|-------|------------|
| UI-01 | Compact / hover-expand sidebar rail | `MainWindow.xaml(.cs)`, `Controls/WorkspaceSidebar.*` | — | M | ui-shell | Collapses to ~56px icon rail; labels on hover; pin persists state; WebView reflows cleanly |
| UI-02 | DenDen Hub sidebar polish | `Controls/WorkspaceSidebar.*`, `PlatformBrandingHelper.cs` | UI-01 | M | ui-shell | OVERVIEW/PRO/PERSONAL headers; status lines; footer badge layout; selection accent |
| UI-03 | Global keyboard shortcuts | `MainWindow.*`, `KeyboardShortcutService.cs` (new), `ShellNavigationService.cs` | — | M | ui-shell | Ctrl+1–9 instances, Ctrl+D dashboard, Ctrl+Shift+N panel, Ctrl+, settings |
| UI-04 | Theme override (light/dark/system) | `App.xaml(.cs)`, `AppSettings.*`, `SettingsPage.*` | — | M | ui-shell | Persisted in settings.json; applies without restart |

### Notification Hub

| ID | Task | Files | Deps | Cx | Agent | Acceptance |
|----|------|-------|------|-----|-------|------------|
| NOT-01 | Mark all as read (panel) | `NotificationFeedPanel.*`, `NotificationHub.cs` | — | S | notifications | Button calls `MarkAllAlertsRead()`; distinct from Clear all |
| NOT-02 | Smarter auto-open rules | `MainWindow.xaml.cs`, `AppSettings.*`, `SettingsPage.*` | — | M | notifications | Settings: unfocused / never / always; panel respects rule |

### Developer

| ID | Task | Files | Deps | Cx | Agent | Acceptance |
|----|------|-------|------|-----|-------|------------|
| DEV-01 | Reliable `dotnet run` | `UnifiedMessenger.csproj`, `launchSettings.json`, README | — | M | devops | Fresh clone launches with package identity; documented fallback |

---

## Phase 2 — Hub Depth & Dashboard Analytics

### Notification Hub

| ID | Task | Files | Deps | Cx | Agent | Acceptance |
|----|------|-------|------|-----|-------|------------|
| NOT-03 | Rich notification cards | `NotificationFeedPanel.*`, `NotificationAlert*.cs`, `PlatformBrandingHelper.cs` | UI-05 opt | M | notifications | Accent strip, relative time, grouped sections, read/unread styling |
| NOT-04 | Per-instance notification mute | `MessengerInstance.cs`, `InstanceRegistryService.cs`, `WorkspaceSidebar.*`, `NotificationHub.cs` | — | M | notifications | Context menu mute; skip toasts/badges; persisted in instances.json |
| NOT-05 | Muted-chat badge policy | `whatsapp/telegram-adapter.js`, `AppSettings.*`, `SettingsPage.*` | — | M | notifications | Toggle include/exclude muted platform chats in badge totals |
| NOT-06 | Toast customization | `AppNotificationService.cs`, `AppSettings.*`, `SettingsPage.*` | — | M | notifications | Platform logo, grouping, dedupe tags; activation opens instance |

### Dashboard

| ID | Task | Files | Deps | Cx | Agent | Acceptance |
|----|------|-------|------|-----|-------|------------|
| DASH-01 | Time-series chart library | `DashboardPage.*`, `MessageAnalyticsService.cs`, `.csproj` | — | L | dashboard | 7-day sent/received chart replaces bar placeholders |
| DASH-02 | Configurable SLA threshold | `MessageAnalyticsService.cs`, `AppSettings.*`, `SettingsPage.*`, `DashboardPage.*` | — | S | dashboard | 5–120 min setting; breach count recalculates |
| DASH-03 | Operational KPI expansion | `MessageAnalyticsService.cs`, `DashboardPage.*` | DASH-01 | M | dashboard | Peak hour, response rate %, daily trend |
| DASH-04 | Global command palette | `CommandPalette.*` (new), `MainWindow.*`, `ShellNavigationService.cs` | UI-03 | L | dashboard | Ctrl+K fuzzy search instances, alerts, settings actions |

### UI & Shell (continued)

| ID | Task | Files | Deps | Cx | Agent | Acceptance |
|----|------|-------|------|-----|-------|------------|
| UI-05 | Profile avatars in sidebar | `WorkspaceSidebar.*`, `ProfileAvatarService.cs` (new), `adapter-core.js` | ADAPT-06 opt | M | ui-shell | Circular avatar with platform glyph fallback; cached on disk |
| UI-06 | Instance reordering (drag-drop) | `WorkspaceSidebar.*`, `MessengerInstance.cs`, `InstanceRegistryService.cs` | — | M | ui-shell | SortOrder in instances.json; drag within category |

---

## Phase 3 — Platform Adapters & Performance

### Platform Adapters

| ID | Task | Files | Deps | Cx | Agent | Acceptance |
|----|------|-------|------|-----|-------|------------|
| ADAPT-01 | Messenger adapter hardening | `messenger-adapter.js`, `PlatformAdapters.cs`, `AdapterHealthMonitor.cs` | — | M | adapters | Resilient selectors; SPA navigation; stable health dot |
| ADAPT-05 | Adapter auto-recovery | `AdapterHealthMonitor.cs`, `InstanceSessionManager.cs`, `adapter-core.js` | — | M | adapters | Stale → re-inject scripts; no duplicate listeners |
| ADAPT-02 | Slack web adapter | `slack-adapter.js`, `slack-chrome.css`, `PlatformDefinition.cs` | ADAPT-05 | M | adapters | Platform picker entry; badge + intercept |
| ADAPT-03 | Discord web adapter | `discord-adapter.js`, `discord-chrome.css`, `PlatformDefinition.cs` | ADAPT-05 | M | adapters | Unread badge; login unblocked |
| ADAPT-04 | Signal & Teams adapters | `signal/teams-adapter.js`, styles, `PlatformDefinition.cs` | ADAPT-05 | L | adapters | Selectable at add-instance; badge or graceful fallback |
| ADAPT-06 | Chrome tuning expansion | `WebViewChromeStyleInjector.cs`, `Assets/Styles/*.css` | ADAPT-02–04 | S | adapters | Per-platform CSS presets mapped by platform id |

### Memory & Performance

| ID | Task | Files | Deps | Cx | Agent | Acceptance |
|----|------|-------|------|-----|-------|------------|
| MEM-01 | `TrySuspendAsync` inactive WebViews | `InstanceSessionManager.cs`, `MainWindow.xaml.cs` | — | M | platform | Background suspend on switch; resume within 30s |
| MEM-02 | Memory tier user override | `MessengerInstance.cs`, `InstanceRegistryService.cs`, `InstanceSessionManager.cs` | MEM-01 | M | platform | Low/Normal/High per instance; persisted |
| MEM-03 | Resource monitor dashboard | `ResourceMonitorService.cs`, `DashboardPage.*` | — | M | platform | Working set + per-instance tiles; 30s refresh |
| MEM-04 | Session cap (LRU) | `InstanceSessionManager.cs`, `AppSettings.*`, `SettingsPage.*` | MEM-01 | L | platform | Max concurrent WebViews; visible never evicted |

### UI

| ID | Task | Files | Deps | Cx | Agent | Acceptance |
|----|------|-------|------|-----|-------|------------|
| UI-07 | Bottom notification panel option | `MainWindow.*`, `NotificationFeedPanel.*`, `AppSettings.*` | NOT-03 | M | ui-shell | Right vs bottom layout; persisted |

---

## Phase 4 — System Integration & Shipping

### Settings & System

| ID | Task | Files | Deps | Cx | Agent | Acceptance |
|----|------|-------|------|-----|-------|------------|
| SYS-01 | Pin to taskbar prompt | `MainWindow.xaml.cs`, `AppSettings.*`, `SettingsPage.*` | — | S | platform | `RequestPinCurrentAppAsync`; don't ask again |
| SYS-02 | `BadgeNotificationManager` migration | `TaskbarBadgeService.cs`, `.csproj` | — | M | platform | Replace `BadgeUpdateManager`; respects toggle |
| SYS-03 | Taskbar overlay fallback (Win10) | `TaskbarOverlayService.cs` (new), `TaskbarBadgeService.cs` | SYS-02 | M | platform | `ITaskbarList3` when numeric badge unavailable |
| SYS-04 | Startup with Windows | `StartupTaskService.cs` (new), `SettingsPage.*` | — | M | platform | Registry Run key or Startup folder toggle |
| SYS-05 | Clear analytics data | `MessageAnalyticsService.cs`, `SettingsPage.*` | — | S | dashboard | Confirm dialog; deletes analytics.json |
| SYS-06 | Notification sound prefs | `AppNotificationService.cs`, `AppSettings.*`, `SettingsPage.*` | NOT-06 | S | notifications | Silent / default system sound |

### Dashboard & DevOps

| ID | Task | Files | Deps | Cx | Agent | Acceptance |
|----|------|-------|------|-----|-------|------------|
| DASH-05 | Analytics export (CSV/JSON) | `MessageAnalyticsService.cs`, `DashboardPage.*` | DASH-03 | M | dashboard | File picker export per instance metrics |
| DEV-02 | Adapter tests (headless WebView2) | `UnifiedMessenger.Tests/` (new) | ADAPT-01, ADAPT-05 | L | devops | Fixture HTML; badge-count tests per adapter |
| DEV-03 | CI/CD pipeline | `.github/workflows/build.yml` | DEV-01 | L | devops | x64/ARM64 publish + Inno Setup installer on PR |
| DEV-04 | Auto-update UI & controls | `GitHubUpdateService.cs`, `AboutPage.*`, `SettingsPage.*` | DEV-03 | M | devops | Manual check, disable auto-update, update prompt before silent install |

---

## Explicitly Excluded (Path C)

- Load-on-first-visit / lazy WebView loading  
- Per-instance sleep / unload  
- Startup warm mode settings  
- Edit instance URL / platform after creation  
- Import / export `instances.json`  
- Instance notes / tags  

---

## Multi-Agent Execution Batches

| Batch | Agent | Tasks | Parallel |
|-------|-------|-------|----------|
| **A** | ui-shell | UI-01, UI-02, UI-03, UI-04 | Yes |
| **B** | notifications | NOT-01 → NOT-02 → NOT-03–06 | Partial |
| **C** | dashboard | DASH-02, DASH-01, DASH-03, DASH-04, DASH-05 | DASH-04 after UI-03 |
| **D** | adapters | ADAPT-05 → ADAPT-01 → ADAPT-02/03/04/06 | Sequential core |
| **E** | platform | MEM-01 → MEM-02/03/04, SYS-01–04 | MEM chain sequential |
| **F** | devops | DEV-01 → DEV-02 → DEV-03 → DEV-04 | Sequential |

**Total: 36 tasks** · ~18–24 agent-days (excluding Path C)

---

## Branding Assets

| Asset | Path | Use |
|-------|------|-----|
| Icon master (1024) | `Assets/Branding/icon-master.png` | Source for regeneration |
| Wide master (1240×600) | `Assets/Branding/wide-master.png` | Marketing / store collateral source |
| App icon | `Assets/AppIcon.ico` | Window, installer, toast logo, About page |

Regenerate with `UnifiedMessenger/tools/Regenerate-BrandingAssets.ps1`.

**Palette:** Slate `#1E293B` · Teal `#14B8A6` · Blue `#3B82F6` · Violet `#8B5CF6`
