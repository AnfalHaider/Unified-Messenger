# Unified Messenger — AGENTS.md

## What this project is

A **free, fully-local Windows oversight app** for a multi-location business owner to monitor customer conversations (WhatsApp first, then Google Business / Telegram / Messenger). The app passively scrapes connected web clients and surfaces health metrics — on-time %, awaiting reply, stale accounts — in a command-center dashboard.

**Hard constraints (never violate):**
- Nothing on cloud. No APIs. No recurring cost. Zero data leaves the machine.
- App never auto-sends. Automation is read-only scraping only.
- All AI is fully on-device via Ollama. No cloud LLM.
- No roles/permissions. Anyone with access to the installed machine sees the same data.
- No unofficial protocol libraries (Baileys, whatsmeow, etc.) — ban risk. Use real web clients in WebView2.

---

## Tech stack

- **WinUI 3 / Windows App SDK 2.1.3** — unpackaged desktop app (`WindowsPackageType=None`, no MSIX)
- **.NET 8** — `net8.0-windows10.0.19041.0`, nullable enabled, `LangVersion=latest`
- **WebView2 1.0.3967.48** — each account/channel is an isolated session in `CoreWebView2Environment`
- **CommunityToolkit.Mvvm 8.4.0** — `ObservableObject`, `RelayCommand`
- **OllamaSharp 5.4.12** — local LLM integration (Tier 2 AI)
- **H.NotifyIcon.WinUI** — system tray
- **xUnit** — test framework (`UnifiedMessenger.Tests`)
- **Inno Setup 6** — installer (`installer.iss`, `installer-arm64.iss`, shared constants in `installer-shared.iss`)

---

## Build, publish, and install cycle

**Dev build (fast check):**
```
dotnet build UnifiedMessenger/UnifiedMessenger.csproj -c Release --nologo -v quiet
```

**Publish win-x64 (shipping binary):**
```
dotnet publish UnifiedMessenger/UnifiedMessenger.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true --nologo -v quiet
```
⚠️ **`-p:Platform=x64` is mandatory.** The installer (`installer.iss`) packages from `bin\x64\Release\...\win-x64\publish`. Without `-p:Platform=x64`, publish writes to `bin\Release\...\publish` instead, and the installer silently ships a **stale** binary from the old x64 folder — the app installs and runs fine but shows old code. Always confirm the installed exe version after install: `(Get-Item "$env:LOCALAPPDATA\Programs\UnifiedMessenger\UnifiedMessenger.exe").VersionInfo.FileVersion`.

**Smoke test — ALWAYS kill any leftover instance first:**
```powershell
Stop-Process -Name UnifiedMessenger -Force -ErrorAction SilentlyContinue
# wait ~500ms, then launch and check for ALIVE
Start-Process "...\publish\UnifiedMessenger.exe"
Start-Sleep -Seconds 4
Get-Process UnifiedMessenger -ErrorAction SilentlyContinue  # must exist
```
The app uses a single-instance mutex (`UnifiedMessenger_AppMutex`). If a stale process holds it, the new binary exits immediately with no output. Always kill before smoke-testing.

**Compile installer:**
```
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "D:\Projects\Unified Messenger\installer.iss"
# Output: dist\UnifiedMessengerSetup.exe
```

**Silent install + ALIVE check:**
```powershell
Start-Process "dist\UnifiedMessengerSetup.exe" "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART"
Start-Sleep -Seconds 12
Start-Process "$env:LOCALAPPDATA\Programs\UnifiedMessenger\UnifiedMessenger.exe"
Start-Sleep -Seconds 5
Get-Process UnifiedMessenger  # must show ALIVE
```

---

## Version sync — 4 files, always in lockstep

When bumping to a new version (e.g. `4.22.0`):

| File | Field |
|---|---|
| `UnifiedMessenger/UnifiedMessenger.csproj` | `<Version>`, `<AssemblyVersion>`, `<FileVersion>` |
| `UnifiedMessenger/app.manifest` | `assemblyIdentity version=` |
| `installer-shared.iss` | `#define MyAppVersion` |
| `README.md` | `**Current release:**` line + new `### What's in vX.Y.Z` section |

Also update `docs/phase-status.md` header date + baseline version.

---

## Running tests

**Always use targeted class-name filters** — many tests spin up WebView2, registry fixtures, or real async pipelines that hang in headless CI. Never run the full suite unfiltered.

```powershell
# Targeted (safe, fast):
dotnet test UnifiedMessenger.Tests/UnifiedMessenger.Tests.csproj --nologo -v quiet `
  --filter "FullyQualifiedName~PlatformDefinitionTests|FullyQualifiedName~PlatformAdapterFactoryTests"

# Avoid filters like "~PlatformAdapter" — grabs extra classes that hang headless.
# Use exact class names, not substrings of substrings.
```

Test fixtures: `UnifiedMessenger.Tests/Fixtures/` (HTML files for script tests). Assets (JS, CSS) are linked into the test output via the `.csproj`.

---

## Project structure

```
UnifiedMessenger/
  App.xaml / App.xaml.cs          — application entry, DI composition root
  MainWindow.xaml / .cs           — shell host, single window
  Models/                         — plain data models (AppSettings, PlatformDefinition, ChatEntry, …)
  Services/                       — all business logic (no UI dependencies). Files stay in the flat
                                    `UnifiedMessenger.Services` namespace regardless of folder, so a file can
                                    be moved between module folders with zero code changes.
    Oversight/                    — command-center engine (rollup, snapshot reader, awaiting, response time)
    Analytics/                    — message analytics, contact history, business report
    Session/                      — WebView2 session lifecycle, nav guard, connection
    Notifications/                — toast/tray/taskbar-badge notification surfaces
    Distribution/                 — GitHub auto-update, startup, single-instance
    Adapters/                     — platform scraper adapters
    Ai/                           — Ollama client + insight service
    Backfill/                     — IndexedDB backfill pipeline
    Shell/                        — shell navigation + controller
    Contracts/                    — service interfaces
    (root)                        — cross-cutting infra (DI, paths, logging, theme, UI helpers)
  Controls/                       — reusable XAML controls (.xaml + .cs)
    CommandCenterPanel.xaml.cs    — the L0 command-center (imperative card builder)
    WorkspaceSidebar.xaml.cs      — left rail with scope switch + location groups
  Pages/                          — top-level pages (DashboardPage, SettingsPage, …)
  Dialogs/                        — ContentDialogs (AddInstance, Delete, Rename, …)
  Assets/
    Scripts/                      — JS injected into WebView2 (whatsapp-adapter.js, …)
    Styles/                       — CSS injected per platform (*-chrome.css)
    Config/                       — JSON config assets

UnifiedMessenger.Tests/           — xUnit tests
  Backfill/                       — backfill pipeline tests
  Ai/                             — AI service tests
  Fixtures/                       — HTML fixtures for script tests

docs/
  MASTER-PLAN.md                  — authoritative product spec (read this before adding features)
  phase-status.md                 — current build status per phase (update after every increment)
  architecture/                   — ADRs
```

---

## Key services

### Platform model
- **`PlatformDefinition.All`** — registry of selectable platforms: `whatsapp`, `whatsappbusiness`, `googlebusiness`, `telegram`, `messenger`, `generic`. Add new platforms here.
- **`PlatformDefinition.NormalizePlatformId(id)`** — returns the registered `Id` or falls back to `"whatsapp"` for truly unknown ids.
- **`PlatformAdapterInternals.ResolveEnabledAdapter(platformId)`** — switch on normalized ID; unknown/new platforms fall through to `NullPlatformAdapter` (`PlatformId = "generic"`). Add a case here when building a real scraping adapter.
- **`AddInstanceDialogHelper`** — drives the "Add account" dialog; reads `PlatformDefinition.All`.

### Oversight engine (L0 command center)
- **`OversightChatSnapshotService`** — reads WhatsApp Web's local `model-storage` IndexedDB `chat` store via `ExecuteScriptAsync`. Returns `ChatEntry` list. **Gotchas:** `ExecuteScriptAsync` doesn't await JS promises — use start/poll pattern. Long `message`-store cursors hang; use bounded `chat` `getAll`. Focus by sidebar `data-id` JID not title text.
- **`OversightService`** — wires `OversightRollupBuilder` to live instances; builds `OversightEntityHealth` snapshots.
- **`OversightEntityHealth`** — per-account/location health: `OnTimePercent`, `AwaitingCount`, `MeasuredCount`, `HasChatData`, `IsStale`, `TrendCounts`, `DisplayName`, `Key`, `MemberInstanceIds`.
- **`OversightRollupBuilder`** — pure rollup logic; produces worst-first sorted health entries.
- **`OversightAlertMonitor`** — edge-triggered threshold toasts when awaiting > X.
- **`OversightInsightService`** — per-account AI insight cache: keyed by `(entityKey, signature)`, background Ollama generation, heuristic fallback. Prompts send **only aggregate counts** — never customer names or message text.

### Unsaved-contact phone + message preview (P2-A, shipped v4.39.10) — VERIFIED FACTS

These were confirmed by reading the live WhatsApp Web IndexedDB via F12 DevTools (DevTools is enabled). Don't re-derive or re-guess them — three earlier guesses were wrong.

- **WhatsApp Web stores unsaved contacts under `@lid` privacy JIDs**, not phone numbers. The `chat` store's conversation key is the `@lid` for these.
- **`@lid` → phone lives in the `contact` store.** Each record is keyed by its `id` (which *is* the `@lid` for unsaved contacts) and carries `phoneNumber` as a `@c.us` JID, e.g. `{ id: "…@lid", phoneNumber: "923105325598@c.us", pushname: "…" }`. The dedicated `lid-pn-mapping` store exists but is **empty** — ignore it. `whatsapp-adapter.js` `buildLidPhoneMap` builds `contact.id → digits(contact.phoneNumber)`; the scan sets `contactPhone = lidPhoneMap[jid] || umExtractDigits(jid)`.
- **Message bodies are ENCRYPTED at rest** in the `message` store's `msgRowOpaqueData` blob (`iv`/`_keyId`/`_scheme`). The `chat` store has no body. So **no readable preview exists in IndexedDB**; decryption is out of scope. The only plaintext preview source is the **live sidebar DOM**.
- **Sidebar row DOM:** each `[role="row"]` exposes two `span[title]` — `[0]` = name/phone, `[1]` = last-message text — and carries **no `data-id`**. `window.__umStartPreviewHarvest()` does a **synchronous** single pass over the ~60 rendered rows (background webviews throttle `setTimeout` to ~1/sec, so scrolling never finishes), keying previews by the title's phone digits into `window.__umHarvestedPreviews`; the scan joins by resolved phone.
- **Two C# parse paths build `ChatEntry` from the scan JSON — keep both in sync:** `WhatsAppBackfillProvider.ProcessIndexedDbConversationsAsync` and `OversightSnapshotReader.ParseChatEntries`. Both must read `contactPhone`. `OversightThreadEnricher.Enrich` prefers `chat.ContactPhone` → `+<digits>`. Tests: `OversightThreadEnricherTests` (7, green).
- **`OnResyncHistory` reloads each account's WebView before probing** so freshly-installed scraper JS takes effect (the adapter script is injected only on document creation). `HarvestPreviewsAsync` waits ~25s for the chat list to re-render before harvesting. Preview harvest runs on the manual Re-sync path only — never the background `OversightAlertMonitor` (so it never scrolls the visible list passively).
- Known limits (accepted): previews only for chats among the ~60 rendered rows (awaiting chats are near the top) and only when the last message has text. Re-sync is slower because it reloads each account first.

### AI layer
- **`OllamaInferenceClient`** — wraps OllamaSharp; `GenerateTextAsync(prompt, systemPrompt, model, ct)` returns trimmed text or null on failure. `GenerateStructuredAsync<T>` for schema-constrained output.
- **`OversightInsightService.Instance`** (singleton) — `TryGet(key, sig)` for cached AI text; `Request(key, sig, facts, onReady)` for background generation.
- AI is gated by `AppSettings.EnableLocalAi` and `OllamaConnectionState`. Always degrade gracefully to heuristic.

### Session lifecycle
- **`InstanceSessionManager`** — LRU cap (default 6), memory tiers (`MemoryUsageTargetLevel.Low` for background), idle-session reaper (1-min timer, `IdleSessionReapMinutes` default 20, professional accounts exempt, visible account never reaped).
- **`AdapterHealthMonitor`** — 90s stale threshold, 5-min recovery cooldown; fires `AdapterStaleDetected` → `MainWindow.OnAdapterStaleDetected`.

### Shell / navigation
- **`ShellController`** — coordinates sidebar ↔ content area.
- **`ShellNavigationCoordinator`** — routes nav commands to pages/instances.
- **`WorkspaceSidebarViewModel`** / **`WorkspaceSidebarHelper`** — sidebar state; scope switch (All/Professional/Personal); location rail (right-click → Set location).

---

## Platform adapter pattern

To add a real scraping adapter for a new channel (e.g. Telegram):

1. Register the platform in `PlatformDefinition.All` (already done for telegram/messenger).
2. Create `Services/Adapters/Modules/TelegramAdapter.cs` implementing the adapter interface.
3. Add a `case "telegram":` in `PlatformAdapterInternals.ResolveEnabledAdapter`.
4. Write a JS scraper in `Assets/Scripts/telegram-adapter.js` (mirroring `whatsapp-adapter.js`).
5. Add a CSS file `Assets/Styles/telegram-chrome.css` for custom chrome.
6. Add the script/CSS to the `.csproj` `<Content>` block.
7. Update `OversightChatSnapshotService` or create a parallel `TelegramChatSnapshotService` to feed oversight metrics.

Scrapers need to be built against a **live, logged-in account** — DOM structure changes per platform. Don't write untestable DOM queries.

---

## UI patterns

- **`CommandCenterPanel.xaml.cs`** — imperative card builder (no data-binding; builds `StackPanel`/`Border` trees in C#). Uses `_lastRenderSignature` string for change detection to skip unnecessary redraws.
- `OnInsightReady()` callback: always dispatch via `DispatcherQueue?.TryEnqueue(...)` — AI callbacks arrive on background threads.
- `BuildInsightStrip(entity)` — returns null when no attention needed; shows heuristic or AI text with `✦ AI` badge.
- Compact/comfortable density: `_compact` bool; card padding, font sizes, and sparkline visibility switch on it.
- Search filter: `_searchQuery` string; `MatchesSearch(entity)` helper.

---

## Known gotchas and decisions

| Gotcha | Fix / Rule |
|---|---|
| Smoke test exits immediately (not ALIVE) | Kill leftover process before testing: `Stop-Process -Name UnifiedMessenger -Force` |
| Installer ships stale binary (UI changes don't appear after install) | Publish with `-p:Platform=x64` — installer reads `bin\x64\Release\...\publish`, but a plain publish writes to `bin\Release\...\publish`. Verify installed exe `FileVersion` after every install. |
| WinUI publish omits `.xbf` and `.pri` files | `CopyWinUIResourcesToPublish` MSBuild target handles this — don't work around it |
| STJ 10 conflict with self-contained .NET 8 | `EnsureSystemTextJson10InPublish` MSBuild target copies STJ 10 dlls post-publish |
| `ReadyToRun` breaks self-contained WinUI publish | Intentionally disabled (`PublishReadyToRun=false`) — don't re-enable |
| Native AOT / trimming disabled | WinUI 3 + WebView2 require full runtime — don't enable |
| `ExecuteScriptAsync` doesn't await JS promises | Use start/poll pattern with a watchdog; never `.Result` a promise bridge |
| Long `message`-store IndexedDB cursor hangs read transaction | Use bounded `chat` `getAll` instead of per-message cursors |
| Test filter too broad → hangs headless | Use exact class names in `--filter`, not loose substrings |
| `NullPlatformAdapter.PlatformId` is `"generic"` not `"whatsapp"` | Adapter factory tests for new platforms expect `"generic"` |
| Unsaved contacts show `@lid` JIDs, not phone numbers | Phone is in the `contact` store's `phoneNumber` field, keyed by `@lid` id. The `lid-pn-mapping` store is empty — don't use it. (See P2-A section.) |
| Message preview is blank | Bodies are encrypted at rest (`msgRowOpaqueData`); harvest preview from the live sidebar DOM (`__umStartPreviewHarvest`), not IndexedDB. |
| Scraper JS change doesn't take effect after install | Adapter is injected on document creation only. Reload the page (Re-sync now does this automatically) or right-click account → Refresh WebView. |
| Background webview throttles `setTimeout` (~1/sec) | Don't rely on timed loops (e.g. scroll harvest) in non-visible webviews; do synchronous single-pass DOM reads. |
| `ChatEntry` field added but not populated | TWO parse paths build it: `WhatsAppBackfillProvider.ProcessIndexedDbConversationsAsync` AND `OversightSnapshotReader.ParseChatEntries`. Update both. |

---

## Commit convention

```
vX.Y.Z: short description (Phase N — what slice) (Increment NN)

Body: what changed and why. What's deferred and why.
```

Use Bash `git commit -m "..."` (not PowerShell here-strings — they break on multi-line with special chars).
Do **not** add `Co-Authored-By` / tool-attribution trailers to commits in this repo.

---

## Phase roadmap (current as of v4.53.0)

See `docs/remaining-work.md` for the detailed backlog. Summary:

| Phase | Status |
|---|---|
| 1 — WhatsApp oversight foundation | ✅ Complete |
| 2 — AI tiers (insight strips + Ollama) | ✅ Core done · ✅ P2-A unsaved-contact phone + preview · ✅ Tier-2 narration suite · ✅ weekly report + anomaly detection (v4.50.0) · ☐ Tier-1 ONNX (needs a model) |
| 3 — Oversight depth & scale | ✅ Mostly done · ✅ First Response Time / SLA metrics (v4.46.0) · ✅ current-state awaiting, mark-handled/snooze, KPI micro-trends, per-account L1 drill-down, quiet hours (v4.51–4.53) · ☐ Sidebar-rail density at very large counts |
| 4 — Google Business embed + metrics | ◑ Embed done · ✅ Review-health scraper + which-reviews-need-a-reply + click-through (v4.42.0/v4.49.0); no rating/total (Google doesn't expose them) |
| 5 — Telegram + Meta embed + metrics | ◑ Embed done · ☐ DOM metric scrapers pending (#24 — need live accounts) |

**Shipped v4.46.0 → v4.53.0:** forward-tracked **First Response Time** + SLA met % + answered-today
(v4.46.0); **redesigned account cards** with live detail chips (v4.47.0); a **data-accuracy audit**
(customer-only counts, local-day keying, range-aware hour chart — v4.48.0); **notifications by account**,
**per-account stacked activity colours**, **actionable reviews** (v4.49.0); the **weekly business report**
with anomaly detection + export (v4.50.0); and the full **command-center improvement set #1–#7**
(v4.51.0–v4.53.0): current-state awaiting, card→needs-reply filter, mark-handled/snooze, KPI micro-trend
sparklines, response-time trend in the report, per-account L1 drill-down, and quiet hours.

Key services added this stream: `ResponseTimeTracker`, `AwaitingOverrideStore`, `KpiTrendStore`,
`QuietHours`, `BusinessReport`/`DashboardReportHelper`, `ChartPalette`, `AccountDetailDialog`,
`WeeklyReportDialog`, `MiniSparkline`.

Remaining work is **gated on external dependencies** (task #s in the running list):
1. #24 Telegram / Messenger / Instagram DOM scrapers — need live logged-in accounts (Meta read-only only)
2. P3-D full multi-channel L1 view — the WhatsApp per-account drill-down ships (v4.53.0); tabs depend on #24
3. Tier-1 ONNX — needs a chosen, downloaded model + runtime packaging (can't be built blind)
4. Icon import-from-account robustness · brand-logo import for other channels — live per-platform DOM tuning

> Optional follow-ups (feasible now, not blocked): business-hours-aware FRT, AI-narrated report headline,
> OS-scheduled report, PNG/PDF export, a dedicated empty-state sweep. P2-C (outbound tone scoring) was dropped.
