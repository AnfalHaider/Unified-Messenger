# Unified Messenger

Native WinUI 3 desktop client for running **multiple isolated WhatsApp / WhatsApp Business Web sessions** in one window, with a unified notification hub and lightweight operations dashboards.

**Current release:** v4.8.4 (Command center shows the exact "N awaiting reply" count alongside caught-up %, scoped to the date window)

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
