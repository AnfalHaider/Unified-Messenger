# Unified Messenger — AGENTS.md

## What this project is

A **free, fully-local Windows oversight app** for a multi-location business owner to monitor customer conversations (WhatsApp first, then Telegram / Messenger / Instagram) **plus Google Business reviews** — Google is a *reviews* channel, not a conversation channel, because Google Business Messages was shut down in 2024. The app passively scrapes connected web clients and surfaces health metrics — on-time %, awaiting reply, stale accounts — in a command-center dashboard.

**Hard constraints (never violate):**
- Nothing on cloud. No APIs. No recurring cost.
- **Zero *oversight* data leaves the machine.** The app must never transmit metrics, message
  content, customer identities, or AI prompts off-box — no telemetry, no analytics, no crash
  upload, ever. This governs data *the app derives*. It does **not** prohibit the user's own
  browsing traffic in a browse tab: a page the owner deliberately opens is their own request,
  isolated in a separate WebView2 profile, and is not app-originated exfiltration. Never
  conflate the two — and never let oversight data reach a browse tab.
- App never auto-sends. Automation is read-only scraping only.
- All AI is fully on-device via Ollama. No cloud LLM.
- No roles/permissions. Anyone with access to the installed machine sees the same data.
- No unofficial protocol libraries (Baileys, whatsmeow, etc.) — ban risk. Use real web clients in WebView2.
  Such projects may be **read** for DOM/protocol knowledge; their code may not be vendored or copied.
  GPL/AGPL sources (Telegram Web A/K, mautrix) are reference-only. MIT sources (Ferdium recipes) may be
  adapted with attribution in `THIRD-PARTY-NOTICES.md`.

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
# ISCC is NOT on PATH, and on this machine it is a per-user install — not Program Files.
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" "D:\Projects\Unified Messenger\installer.iss"
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
| `README.md` | `**Current release:**` line only — the README is a *product* README, not a changelog |
| `CHANGELOG.md` | new `## vX.Y.Z` section at the top (this is where release notes live now) |

Also update `docs/phase-status.md` header date + baseline version.

---

## Running tests

**Always use targeted class-name filters** — many tests spin up WebView2, registry fixtures, or real async pipelines that hang in headless CI. Never run the full suite unfiltered.

```powershell
# Targeted (safe, fast):
dotnet test UnifiedMessenger.Tests/UnifiedMessenger.Tests.csproj -c Release --nologo -v quiet `
  --filter "FullyQualifiedName~PlatformDefinitionTests|FullyQualifiedName~PlatformAdapterFactoryTests"

# Avoid filters like "~PlatformAdapter" — grabs extra classes that hang headless.
# Use exact class names, not substrings of substrings.
```

**Always test the Release (live/shipping) build — never Debug.** `Directory.Build.props` defaults the repo
to Release when no `-c` is passed, so a bare `dotnet test`/`dotnet build` no longer produces a stale Debug
`UnifiedMessenger.exe`. The `-c Release` above is explicit belt-and-suspenders; don't pass `-p:Platform` to
`dotnet test` (it breaks the test-dll path resolution — see `.github/workflows/build.yml`).

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
- **`PlatformDefinition.All`** — registry of **nine** registered platforms: `whatsapp`, `whatsappbusiness`,
  `googlebusiness`, `telegram`, `messenger`, `discord`, `metabusinesssuite`, `instagram`, `generic`.
  Add new platforms here.
- **Registered ≠ offered.** `PlatformModuleSettingsHelper.HiddenFromPicker` hides `telegram`,
  `metabusinesssuite`, and `instagram` from "Add account"; they stay in `All` so existing accounts still
  resolve and the nav-guard allowlist keeps their hosts. The picker therefore offers six:
  whatsapp, whatsappbusiness, googlebusiness, messenger, discord, generic. Check `HiddenFromPicker`
  before concluding a channel is user-reachable.
- **Only whatsapp/whatsappbusiness produce conversation metrics.** `googlebusiness` contributes *review*
  metrics on a separate surface (`GoogleReviewSnapshotService`). `messenger`, `discord`, and `generic` are
  embed-only and produce nothing — that is intended, not a bug.
- **`PlatformDefinition.Description` is rendered in the Add-account picker** (since v4.99.2) and is
  covered by `PlatformDescriptionTests`. It must state what the channel does **today** — the tests fail
  on roadmap words ("planned", "coming soon", …) and require unmeasured channels to say
  "No oversight metrics". Do not write aspirational copy here; it is shown to a paying customer.
- **`PlatformDefinition.NormalizePlatformId(id)`** — returns the registered `Id` or falls back to `"whatsapp"` for truly unknown ids.
- **`PlatformAdapterInternals.ResolveEnabledAdapter(platformId)`** — switch on normalized ID; unknown/new platforms fall through to `NullPlatformAdapter` (`PlatformId = "generic"`). Add a case here when building a real scraping adapter.
- **`AddInstanceDialogHelper`** — drives the "Add account" dialog; reads `PlatformDefinition.All`.

### Oversight engine (L0 command center)
- **`OversightChatSnapshotService`** — reads WhatsApp Web's local `model-storage` IndexedDB `chat` store via `ExecuteScriptAsync`. Returns `ChatEntry` list. **Gotchas:** `ExecuteScriptAsync` doesn't await JS promises — use start/poll pattern. Long `message`-store cursors hang; use bounded `chat` `getAll`. Focus by sidebar `data-id` JID not title text.
- **`OversightService`** — wires `OversightRollupBuilder` to live instances; builds `OversightEntityHealth` snapshots.
- **`OversightEntityHealth`** — per-account/location health: `OnTimePercent`, `AwaitingCount`, `MeasuredCount`, `HasChatData`, `IsStale`, `TrendCounts`, `DisplayName`, `Key`, `MemberInstanceIds`.
- **`OversightRollupBuilder`** — pure rollup logic; produces worst-first sorted health entries.
- **`OversightAlertMonitor`** — edge-triggered threshold toasts when awaiting > X.
- **`OversightInsightService`** — per-account AI insight cache: keyed by `(entityKey, signature)`, background Ollama generation, heuristic fallback. Its prompts send **only aggregate counts** — never customer names or message text.
- ⚠️ **That aggregate-only rule is specific to `OversightInsightService`. It is NOT true of the AI layer as a whole.**
  There is a second path: `AiInferenceQueue` → **`TranscriptBuilder.Build`**, which puts the **customer name**
  and up to **800 characters of the message body** into the prompt. This is on-box and permitted (Ollama is
  localhost, and the Settings copy correctly says "Message text is sent to your local Ollama instance only"),
  but any privacy analysis that assumes "aggregates only" is wrong. This sentence previously said the
  opposite and misled an audit — do not simplify it back.

### Unsaved-contact phone + message preview (P2-A, shipped v4.39.10) — VERIFIED FACTS

These were confirmed by reading the live WhatsApp Web IndexedDB via F12 DevTools (DevTools is enabled). Don't re-derive or re-guess them — three earlier guesses were wrong.

- **WhatsApp Web stores unsaved contacts under `@lid` privacy JIDs**, not phone numbers. The `chat` store's conversation key is the `@lid` for these.
- **`@lid` → phone lives in the `contact` store.** Each record is keyed by its `id` (which *is* the `@lid` for unsaved contacts) and carries `phoneNumber` as a `@c.us` JID, e.g. `{ id: "…@lid", phoneNumber: "923105325598@c.us", pushname: "…" }`. The dedicated `lid-pn-mapping` store exists but is **empty** — ignore it. `whatsapp-adapter.js` `buildLidPhoneMap` builds `contact.id → digits(contact.phoneNumber)`; the scan sets `contactPhone = lidPhoneMap[jid] || umExtractDigits(jid)`.
- **Message bodies are ENCRYPTED at rest** in the `message` store's `msgRowOpaqueData` blob (`iv`/`_keyId`/`_scheme`). The `chat` store has no body. So **no readable preview exists in IndexedDB**; decryption is out of scope. The only plaintext preview source is the **live sidebar DOM**.
- **Sidebar row DOM:** each `[role="row"]` exposes two `span[title]` — `[0]` = name/phone, `[1]` = last-message text — and carries **no `data-id`**. `window.__umStartPreviewHarvest()` does a **synchronous** single pass over the ~60 rendered rows (background webviews throttle `setTimeout` to ~1/sec, so scrolling never finishes), keying previews by the title's phone digits into `window.__umHarvestedPreviews`; the scan joins by resolved phone.
- **Two C# parse paths build `ChatEntry` from the scan JSON — keep both in sync:** `WhatsAppBackfillProvider.ProcessIndexedDbConversationsAsync` and `OversightSnapshotReader.ParseChatEntries`. Both must read `contactPhone`. `OversightThreadEnricher.Enrich` prefers `chat.ContactPhone` → `+<digits>`. Tests: `OversightThreadEnricherTests` (7, green).
- **`OnResyncHistory` reloads each account's WebView before probing** so freshly-installed scraper JS takes effect (the adapter script is injected only on document creation). `HarvestPreviewsAsync` waits ~25s for the chat list to re-render before harvesting. Preview harvest runs on the manual Re-sync path only — never the background `OversightAlertMonitor` (so it never scrolls the visible list passively).
- Known limits (accepted): previews only for chats among the ~60 rendered rows (awaiting chats are near the top) and only when the last message has text. Re-sync is slower because it reloads each account first.

### Google Business channel — VERIFIED FACTS

- **Google Business Messages is permanently dead.** Customers could no longer start new chats from
  **2024-07-15**; the chat feature was removed from Google Business Profile on **2024-07-31**; historic
  chat data was deleted; the Google Takeout export window closed **2024-08-30**. There is no Google
  message channel to build, now or later. The Google channel is **reviews + Q&A only, forever** — do
  not add awaiting-reply/FRT/message-count plumbing for it, and do not reintroduce roadmap language
  implying one. (`docs/MASTER-PLAN.md` §channel table already records this.)
- **Rating and lifetime review total ARE obtainable** — `GoogleReviewSnapshotService.ProfileRating`
  ships them. They are *not* on the `business.google.com/reviews` manager page; they live only on the
  Google **Search merchant view** (reached via business.google.com's own redirect). Rating comes from
  an `aria-label` reading `"Rated 4.6 out of 5,"`; the total is anchored off the rating because
  `innerText` renders them run together (`"4.6239 Google reviews"` → a naive `([\d,]+)` yields 6239).
  Throttled by `RatingRefreshInterval` (6h) because each scrape costs a visible round-trip.
- The **Business Profile API** would give both cleanly and is free, but it is excluded by the no-cloud/
  no-API rule *and* gated behind a manual Google approval (new GCP projects start at zero quota).

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
| CI `package` job fails; `release` skipped; Releases page stuck on an old version | Same path trap, CI edition. `installer.iss` reads `bin\x64\Release\...\win-x64\publish` and `installer-arm64.iss` reads `bin\ARM64\Release\...\win-arm64\publish`. If the workflow's `dotnet publish -o` (or the artifact upload `path:`) omits the `<Platform>` segment, those dirs stay empty and ISCC aborts with `No files found matching ...\publish\*` (exit 2). The publish `-o`, the artifact `path:`, and the installer's `PublishDir` must stay in lockstep — the workflow now has a "Verify publish output is where the installer expects it" step that fails with an actionable message instead. |
| WinUI publish omits `.xbf` and `.pri` files | `CopyWinUIResourcesToPublish` MSBuild target handles this — don't work around it |
| STJ 10 conflict with self-contained .NET 8 | `EnsureSystemTextJson10InPublish` MSBuild target copies STJ 10 dlls post-publish |
| `ReadyToRun` breaks self-contained WinUI publish | Intentionally disabled (`PublishReadyToRun=false`) — don't re-enable |
| Native AOT / trimming disabled | WinUI 3 + WebView2 require full runtime — don't enable |
| `ExecuteScriptAsync` doesn't await JS promises | Use start/poll pattern with a watchdog; never `.Result` a promise bridge |
| Long `message`-store IndexedDB cursor hangs read transaction | Use bounded `chat` `getAll` instead of per-message cursors |
| Test filter too broad → hangs headless | Use exact class names in `--filter`, not loose substrings |
| `NullPlatformAdapter.PlatformId` is `"generic"` not `"whatsapp"` | Adapter factory tests for new platforms expect `"generic"` |
| Unsaved contacts show `@lid` JIDs, not phone numbers | Phone is in the `contact` store's `phoneNumber` field, keyed by `@lid` id. The `lid-pn-mapping` store is empty — don't use it. (See P2-A section.) |
| Message preview is blank | **Preferred:** the store bridge (`whatsapp-store-bridge.js`) reads the decrypted in-memory models — previews for every chat. **Fallback only:** bodies are encrypted at rest (`msgRowOpaqueData`), so the IndexedDB path harvests preview from the live sidebar DOM (`__umStartPreviewHarvest`). |
| Scraper JS change doesn't take effect after install | Adapter is injected on document creation only. Reload the page (Re-sync now does this automatically) or right-click account → Refresh WebView. |
| Background webview throttles `setTimeout` (~1/sec) | Don't rely on timed loops (e.g. scroll harvest) in non-visible webviews; do synchronous single-pass DOM reads. |
| `ChatEntry` field added but not populated | All readers now funnel through `ChatEntryParser.ParseConversations` (backfill, oversight, and the store bridge). Add the field there once — but make sure **both producers** emit it: `whatsapp-adapter.js` (IndexedDB scan) and `whatsapp-store-bridge.js` (in-memory scan). |
| Tempted to add messaging metrics to the Google channel | Google Business Messages was shut down in July 2024 and the data deleted. Reviews + Q&A only, permanently. |
| Google rating/total "isn't available" | It is — `GoogleReviewSnapshotService.ProfileRating`. It's on the Search merchant view, not the reviews manager page. Don't re-derive; see the Google Business verified-facts section. |
| Custom-URL (generic) account shows a blank page | Not the start URL, the guard. Never key per-WebView state on a `CoreWebView2` in a `ConditionalWeakTable`/dictionary — it is a CsWinRT projection, so the managed wrapper can be collected and re-created for the same native object and the entry silently vanishes. `WebViewNavigationGuard` now captures each allowlist in its handler closure. The failure was invisible for months because the fallback (`DefaultAllowedHosts`) contains every built-in platform host, so only Custom URL broke. Diagnose via `app.log`: `[WebView.Nav] Navigation guard attached: allowAllHosts=… hosts=…` on attach, `Blocked navigation to disallowed URI: …` on a cancel. |
| A "needs reply" chat that genuinely needs no reply | `AwaitingOverrideStore` (mark-handled / snooze) has existed since v4.51.0 — if it looks missing, it's a *surfacing* bug, not a missing feature. `Controls/Shared/AwaitingChatActions.Build` is the one control; use it on every awaiting row so a surface can't ship without it (that's how the per-account drill-down went a release with no way to close a chat). |
| Browsing traffic looks like it breaks "zero data leaves the machine" | It doesn't. That rule governs *oversight* data the app derives. User-initiated browse tabs are the owner's own traffic, on a separate WebView2 profile. Oversight data must never reach a browse tab. |
| Store bridge silently stops working after a WhatsApp Web update | By design it fails soft to the IndexedDB scan, so metrics keep flowing and the regression is invisible. Check Settings → Data → "Store bridge" health line, or run `window.__umStoreBridgeProbe()` in DevTools on the account's page — it reports which discovery strategy matched and which collections resolved. |
| Store-bridge scan returns `stage:"empty"` with 1–8 "chats" | You are reading module **descriptors** instead of exports. `require('__debug').modulesMap` values are `{id, refcount, exports, defaultExport, factory, factoryFinished…}` — `entry.exports` is empty until materialized. Pull every module through `require(name)`. (Verified live: the real collections are `WAWebChatCollection.ChatCollection` ≈850 chats and `WAWebContactCollection.ContactCollection` ≈11.5k contacts, out of ~16,400 modules.) |
| Store-bridge model field reads as `undefined` | Fields are prototype getters over mangled storage: read `unreadCount`, never `__x_unreadCount` (that's an object). Verified-live accessors: `chat.formattedTitle` (NOT `chat.name`), `chat.t`, `chat.unreadCount`, `chat.msgs.last()` (NOT `chat.lastMessage`), `message.id.fromMe` (NOT `message.fromMe`), `contact.phoneNumber`. `chat.isGroup` doesn't exist — filter groups by the JID suffix. |
| Store-bridge preview count is low right after an account loads | Expected. `chat.msgs` fills in lazily as WhatsApp syncs — measured 2% previews at load, 82% a minute later. Phone/name/awaiting are correct immediately; only preview text lags. |
| Need to run JS inside the app's WebView2 from outside | Launch with `$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=9333"` (no code change needed), then drive CDP over the WebSocket at `http://127.0.0.1:9333/json/list`. Kill the app first — the installer auto-launches it and the single-instance mutex will silently swallow your launch, leaving a port-less instance. Relaunch without the variable when done. |

---

## Commit convention

```
vX.Y.Z: short description (Phase N — what slice) (Increment NN)

Body: what changed and why. What's deferred and why.
```

Use Bash `git commit -m "..."` (not PowerShell here-strings — they break on multi-line with special chars).
Do **not** add `Co-Authored-By` / tool-attribution trailers to commits in this repo.

---

## Phase roadmap (current as of v4.99.12)

> ⚠️ The per-phase table and the "Shipped" paragraph below were last revised at **v4.53.0** and were not
> maintained through v4.99.x. Treat them as a historical snapshot, not as current status — several items
> they list as pending have shipped (business-hours-aware FRT is the clearest example: it is listed below
> as an unshipped "optional follow-up", but `Services/Oversight/BusinessHoursCalculator.cs` ships and the
> README advertises it). **Verify against the code before relying on any line in this section.**
> `CHANGELOG.md` is the accurate record of what shipped.

See `docs/remaining-work.md` for the detailed backlog. Summary:

| Phase | Status |
|---|---|
| 1 — WhatsApp oversight foundation | ✅ Complete |
| 2 — AI tiers (insight strips + Ollama) | ✅ Core done · ✅ P2-A unsaved-contact phone + preview · ✅ Tier-2 narration suite · ✅ weekly report + anomaly detection (v4.50.0) · ☐ Tier-1 ONNX (needs a model) |
| 3 — Oversight depth & scale | ✅ Mostly done · ✅ First Response Time / SLA metrics (v4.46.0) · ✅ current-state awaiting, mark-handled/snooze, KPI micro-trends, per-account L1 drill-down, quiet hours (v4.51–4.53) · ☐ Sidebar-rail density at very large counts |
| 4 — Google Business embed + metrics | ✅ Embed · ✅ Review-health scraper + which-reviews-need-a-reply + click-through (v4.42.0/v4.49.0) · ✅ **official rating + lifetime review total** (`GoogleReviewSnapshotService.ProfileRating`, scraped from the Search merchant view, 6-hour throttle). **Reviews + Q&A only — Google has no message channel** (see below) |
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
