# Unified Messenger

Native WinUI 3 desktop hub for multiple web messaging accounts (WhatsApp, Telegram, Messenger, Slack, Discord, Google Business Profile, and custom URLs) with unified notifications and Professional/Personal workspace split.

**Current release:** [v2.0.7](https://github.com/AnfalHaider/Unified-Messenger/releases/tag/v2.0.7)

## Download (Windows)

| Platform | Installer |
|----------|-----------|
| **x64** | [UnifiedMessengerSetup.exe](https://github.com/AnfalHaider/Unified-Messenger/releases/latest/download/UnifiedMessengerSetup.exe) |
| **ARM64** | [UnifiedMessengerSetup-arm64.exe](https://github.com/AnfalHaider/Unified-Messenger/releases/latest/download/UnifiedMessengerSetup-arm64.exe) |

All releases: [github.com/AnfalHaider/Unified-Messenger/releases](https://github.com/AnfalHaider/Unified-Messenger/releases)

Requires Windows 10 1809+ or Windows 11 and the WebView2 Runtime (usually preinstalled on Windows 11).

### What's in v2.0.7

- **OCC filter pipeline:** Branch workspace scope uses `OccFilterState` as the single source of truth (removed duplicate `_workspaceBranchKey`).
- **UiSmoke / a11y:** `AutomationProperties.AutomationId` on OCC layout, kanban, platform intelligence, and dashboard tabs; harness uses AutomationId fallbacks and expand/collapse patterns.
- **966** unit tests (x64, Release); installer validated from `%LocalAppData%\Programs\UnifiedMessenger`.

### What's in v2.0.6

- **OCC:** Unified `OccFilterState`, role presets (Front desk / Manager / After hours), branch pulse stale badge, CSV export via command palette.
- **Shell UX:** Notification hub grouped by platform, conversation “Back to dashboard” link, keyboard shortcuts settings section.
- **CI:** Dependabot, benchmark regression gate, nightly UiSmoke workflow.
- **966** unit tests (x64, Release).

### What's in v2.0.5

- **Lifecycle DI:** `ApplicationLifecycleService` resolves workers via `ApplicationServiceProvider` when initialized.
- **A11y:** Command palette + Personal Overview list names; Personal Overview section cards on `SurfaceCard`.
- **OCC layout:** Undo logic extracted to `LayoutUndo.partial.cs`; adapter health test stabilized.
- **959** unit tests (x64, Release); **38%** line-coverage CI gate.

### What's in v2.0.4

- **Composition root (A-01):** `ApplicationServiceProvider` wires 39 services; Shell, App, and MainWindow off direct `.Instance`; expanded CI DI gate.
- **Settings refactor (A-04/A-07):** Eight domain partial classes; section cards migrated to `SurfaceCard` + `UmSurfaceCardStyle`.
- **OCC layout split (A-05):** Five focused layout partials (edit, apply, drag-drop, keyboard, shared state).
- **CI:** 37% line-coverage gate, informational BenchmarkDotNet job, UiSmoke duplicate unit run removed.
- **959** unit tests (x64, Release).

### What's in v2.0.3

- **Audit remediation:** WebView navigation allowlist (OAuth + per-instance start URLs), adapter reinject guard cleanup, idempotent lifecycle shutdown, notification bridge tests.
- **Headless adapter coverage:** All 7 consumer platforms (WhatsApp, Telegram, Messenger, Signal, Slack, Discord, Teams) validated via WebView2 fixtures.
- **UiSmoke:** Duplicate full unit-suite run removed; WARN tier passes harness (exit 3 only on Fail).
- **958** unit tests (x64, Release).

### What's in v2.0.2

- **ENHANCEMENT_ROADMAP (36/36):** Sidebar rail, command palette, adapter hardening, memory tiers, taskbar overlay, headless WebView2 tests, analytics export, and remaining platform/devops gaps closed.
- **WhatsApp supercharge W1–W6:** WhatsApp-focus OCC preset, enriched triage/copilot prompts, voice-note pipeline (DOM → Whisper → triage), and **Branch pulse** batch LLM summaries per branch in OCC.
- **Platform modularization:** Per-platform module toggles with ingress/UI filtering across dashboard, OCC, and sidebar.
- **952** unit tests (x64, Release).

### What's in v2.0.0

- **Shell architecture:** `ShellController` and coordinators extracted from `MainWindow`; DI migration gate for the shell layer; OCC layout interaction service and grid-only persist.
- **Professional ops UX:** Branch filter chip in KPI strip; SLA countdown on thread cards; conversation-focus loading feedback; OCC command-palette actions (refresh, branch filter, immediate queue).
- **Security:** WebMessage `instanceId` required; `WebViewNavigationGuard` (http/https only); centralized `WebViewScriptGateway`.
- **Quality:** Coverlet coverage; BenchmarkDotNet suite (`UnifiedMessenger.Benchmarks`); UiSmoke WARN tier (warnings pass, exit 3 only on Fail); CI vulnerable-package scan.
- **Design system:** Personal summary cards on `MetricCardView`; OCC branch/insight list cards as shared views; palette category icons; Settings keyboard shortcut reference.
- **811** unit tests (x64, Release).

### What's in v1.1.1

- **OCC dashboard builder (complete):** KPI strip in grid; `OccPanelChrome` hide/+/− resize; Ctrl+Z undo; drop-target preview; responsive reflow at 960/600/400px.
- **Design system adoption:** `MetricCardView`, `SectionHeaderView`, `SurfaceCard`, `AccessibleChartHost` wired across OCC, Settings, and charts.
- **Accessibility & shell:** Full Settings control names; tab order on Dashboard/OCC/Personal; focus traps on dialogs and command palette; contrast audit doc.
- **Settings IA:** Local AI / About reached via sidebar section nav only (single canonical path).
- **799** unit tests (x64, Release).

### What's in v1.1.0

- **OCC dashboard builder:** 12-column grid layout with drag reposition, resize (keyboard Shift+arrow), hide/show panels, layout presets (Operations / Analytics / Compact), and undo.
- **Shell UX:** Sidebar selection survives registry refresh; notification hub shows active state; adaptive conversation focus; OCC light refresh on Personal tab.
- **Design system:** Split theme resources (`Themes/`), shared empty/loading components adopted across OCC, Personal, and notifications.
- **Personal layout:** Customize section order (search, summary, toolbar, content).
- **Accessibility:** Settings control names, chart summaries for screen readers, extended keyboard paths.

### What's in v1.0.25

- **Kanban visual reorder:** Within-column drag-and-drop (edit mode) persists display priority in `triage_v2.json` without changing thread status flags; cross-column drops are rejected with teaching copy.
- **Dashboard layout edit mode:** Reorder OCC sections, KPI cards, and immediate lane; preferences persist in `settings.json` (AppSettings v4) with restore-default and single-level undo.
- **UX polish:** Thread card context menu (open, copy summary, move to top), keyboard reorder (Alt+Up/Down), empty-column drop zones, teaching tips.
- **693** unit tests (x64, Release).

### What's in v1.0.24

- **OCC scroll fix:** Single vertical scroll owner with wheel bubbling; nested ListView scroll disabled so the command center scrolls reliably.
- **Branch workspace pills:** Replaced TabView branch tabs with a horizontal pill bar (fixes dead nav arrows and header text overlap).
- **Dashboard modernization:** Operations-only KPI strip, analytics KPIs moved into the trends expander summary, command-strip refresh, cleaner kanban cards with readable SLA durations.
- **684** unit tests (x64, Release).

### What's in v1.0.23

- **Meta/Google scraper hardening:** Conservative unread counting scoped to inbox/main content; removed broad DOM walks and aggregate-rating false positives; gated inbound signals with cooldown.
- **OCC expander intelligence:** Platform intelligence and analytics trends auto-expand on actionable alerts; user collapse preference persists across sessions via `settings.json`.
- **FlaUI smoke coverage:** Branch workspace tab switching and platform intelligence expander validation in `UnifiedMessenger.UiSmokeTests`.
- **Test stability:** Thread-registry isolation fixes across OCC, phase-8, and dashboard test fixtures.
- **677** unit tests (x64, Release).

### What's in v1.0.22

- **Single OCC pipeline:** One workspace branch scope drives KPIs, kanban, immediate lane, highlights, and analytics — no dual dashboard branch filter.
- **UI/UX milestones A–D:** Operations vs analytics KPI rows, tab badges, loading overlay on branch switch, kanban SLA/revenue bindings, platform health header, intelligence expander badge, highlight deep-links, and KPI tooltips.
- **Personal overview panel:** Extracted personal dashboard into a dedicated control with empty-state reasons and connection status on tiles.
- **648** unit tests (x64, Release).

### What’s in v1.0.21

- **Meta conversation identity:** Generic inbox labels blocked; Meta Business row scraping resolves stable thread keys for triage and navigation.
- **Thread registry fixes:** Revenue-leakage flags no longer downgrade on refresh; upsert honors triage leakage signals.
- **OCC navigation:** Thread cards and AI insight feed deep-link to the correct conversation; immediate cards show inbox label + branch.
- **Branch-scoped kanban:** Immediate lane, counts, and highlights filter consistently when a branch tab is selected.
- **Metrics clarity:** KPI labels distinguish open conversations vs inbound events; SLA subtext shows active waiting threads.
- **Operational data reset:** Settings clears analytics, triage store, thread registry, and backfill dedupe together.
- **UI/UX polish:** Loading states on refresh actions, confirmation dialogs for destructive clears, empty-state add-instance CTA, review-draft validation, pinned-sidebar hamburger toggle.
- **624** unit tests (x64, Release).

### What’s in v1.0.20

- **Branch workspace aggregation:** Operations Command Center groups all professional inboxes (WhatsApp, Meta, Google) by branch key; branch filter, KPI strip, platform intelligence, and kanban share one branch scope.
- **Branch metric cards:** Per-branch open count, inbox count, SLA/revenue summary, and platform breakdown (WA · Meta · Google); click a card to jump to that branch’s kanban tab.
- **Kanban fixes:** Branch tab switching works reliably; cards show platform icon and source inbox.
- **Optional branch key:** Set an explicit branch key on any instance in Edit metadata (e.g. map an “Inbox” account to `DHA-2`).
- **615** unit tests (x64, Release).

### What’s in v1.0.19

- **Test stability:** Triage ingress channel reset and serial dedupe isolation prevent flaky duplicate-ingest under parallel test runs (no user-facing behavior change).
- **609** unit tests (x64, Release).

### What’s in v1.0.18

- **Release packaging:** Rebuilt x64 and ARM64 installers for v1.0.17 OCC hardening line; committed `dist/*.exe` so CI release jobs attach installers correctly.
- **609** unit tests (x64, Release).

### What’s in v1.0.17

- **Operations Command Center hardening (Phases 1–8):** Canonical conversation keys across WhatsApp/Meta/Google; unified SLA and immediate-queue metrics; single LLM inference path via the insights engine; load-gated ingress; rich triage store v3 migration with joint prune and corrupt-file recovery; graceful worker shutdown on exit.
- **OCC UI bindings:** Peak hour, daily trend, immediate-action count, Meta sample/inbound/reply telemetry, and AI feed intent/urgency labels; dashboard refreshes on thread registry and backfill progress.
- **Adapter recovery:** `ReinjectAsync` restores inbound monitor, draft inject, and thread-status-auditor scripts after stale adapter recovery.
- **Breaking changes:** Rich triage store upgrades to **version 3** (`FirstInboundAtUtc` repair); heuristic executive insight cards default **off** (`ShowHeuristicExecutiveInsights=false`); Meta/Google backfill is **scrape-only** (no synthetic reply metrics).
- **609** unit tests (x64).

### What’s in v1.0.16

- **AI pipeline overhaul:** Aggressive DOM noise filtering before LLM triage; strict camelCase JSON schema (`isSpamOrPromo`, `intentCategory`, `urgencyScore`, `actionableSummary`, `suggestedAction`) with parse retry.
- **Dashboard intelligence routing:** Spam/promo threads excluded from Immediate Action Lane, SLA, and branch latency averages; urgency ≥4 routes to immediate queue with actionable summaries (not raw message text).
- **SLA math fix:** Reply latency locks from first inbound to manager reply; branch metrics no longer inflate from unreplied stale threads.
- **551** unit tests (x64).

### What’s in v1.0.15

- **Version bump** with rebuilt x64 and ARM64 installers for the Operations Command Center release line.
- **540** unit tests (x64); FlaUI live validation harness in `UnifiedMessenger.UiSmokeTests`.

### What’s in v1.0.14

- **Operations Command Center:** Professional Operations and Unified Messenger Control Center merged into one dashboard with action-left / context-right layout.
- **Progressive disclosure:** Platform intelligence (Google/Meta) and analytics charts collapse by default; unified empty states across kanban, insights, and health chips.
- **540** unit tests (x64).

### What’s in v1.0.13

- **Startup threading fix:** WebView2 session warm-up re-marshals to the UI thread after WinRT awaits, fixing *"Could not start instances"* on launch.
- **UI thread hardening:** reload, suspend/resume, adapter reinject, chrome inject, and profile cleanup paths use consistent dispatcher marshaling.
- **534** unit tests (x64).

Earlier highlights: v1.0.12 (control center + AI insights), v1.0.11 (installer launch fix), on the [releases](https://github.com/AnfalHaider/Unified-Messenger/releases) page.

## Requirements

- Windows 10 1809+ / Windows 11 (x64 or ARM64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (development)
- WebView2 Runtime (usually preinstalled on Windows 11)

## Quick start

### Run from source

```powershell
cd "d:\Projects\Unified Messenger\UnifiedMessenger"
dotnet build -c Release -p:Platform=x64
dotnet run -c Release -p:Platform=x64
```

#### If `dotnet run` fails

On a fresh clone, unpackaged WinUI apps can fail with package-identity, platform, or Windows App SDK loader errors. Publish x64 output and launch the executable directly:

```powershell
cd "d:\Projects\Unified Messenger\UnifiedMessenger"
dotnet publish -c Release -p:Platform=x64
.\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\UnifiedMessenger.exe
```

If you only need a local framework-dependent build (no `publish` folder), this also works after `dotnet publish -c Release -p:Platform=x64`:

```powershell
.\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\UnifiedMessenger.exe
```

Always pass **`-p:Platform=x64`** (or **ARM64** on Arm hardware) — **Any CPU** is not supported for this WinUI project.

### Visual Studio

Open `UnifiedMessenger.sln`, set **Platform** to **x64** (not Any CPU), and run the **UnifiedMessenger (x64)** profile.

### Tests

```powershell
cd "d:\Projects\Unified Messenger"
dotnet test UnifiedMessenger.Tests\UnifiedMessenger.Tests.csproj -c Release -p:Platform=x64
```

624 unit tests cover services, adapters (including HTML fixture selector checks), backfill orchestration, dashboard card helpers, and dialog helpers.

## Connect Google Business Profile

1. Sidebar → **Add Instance**
2. **Platform:** Google Business Profile
3. **Workspace:** Professional (enables dashboard review widgets)
4. Sign in at `https://business.google.com/locations` in the embedded browser
5. Open **Dashboard** for review alerts and response-time analytics

## Version numbers (keep in sync)

Before every public release, align these three sources to the same **semver** (e.g. `2.0.0`):

| File | Field |
|------|--------|
| `UnifiedMessenger/UnifiedMessenger.csproj` | `<Version>`, `<AssemblyVersion>`, `<FileVersion>`, `<ApplicationDisplayVersion>` |
| `UnifiedMessenger/app.manifest` | `<assemblyIdentity version="…">` (four-part: `2.0.0.0`) |
| `installer-shared.iss` | `#define MyAppVersion "2.0.0"` |

`installer.iss` and `installer-arm64.iss` include `installer-shared.iss` and do not need a separate version line.

## Build a release installer (local)

**Install location:** per-user `%LocalAppData%\Programs\UnifiedMessenger` (no admin). User settings, instances, WebView2 profiles, and analytics live in `%LocalAppData%\UnifiedMessenger`. Upgrades use Restart Manager + `AppMutex` to close a running `UnifiedMessenger.exe`, remove stale binaries, then copy fresh publish output.

If you previously installed when binaries lived under `%LocalAppData%\UnifiedMessenger`, run the latest installer once — it cleans legacy binaries in that folder and installs the app under `Programs\UnifiedMessenger`.

### 1. Publish self-contained binaries

**x64:**

```powershell
cd "d:\Projects\Unified Messenger\UnifiedMessenger"

dotnet publish `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:Platform=x64 `
  -p:PublishSingleFile=false `
  -p:WindowsAppSDKSelfContained=true `
  -p:PublishReadyToRun=false `
  -o "bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"
```

**ARM64:**

```powershell
dotnet publish `
  -c Release `
  -r win-arm64 `
  --self-contained true `
  -p:Platform=ARM64 `
  -p:PublishSingleFile=false `
  -p:WindowsAppSDKSelfContained=true `
  -p:PublishReadyToRun=false `
  -o "bin\Release\net8.0-windows10.0.19041.0\win-arm64\publish"
```

### 2. Compile Inno Setup installers

1. Install [Inno Setup 6](https://jrsoftware.org/isinfo.php).
2. Confirm version sync (table above).
3. Compile:

```powershell
cd "d:\Projects\Unified Messenger"
& "$env:LOCALAPPDATA\InnoSetup6\ISCC.exe" installer.iss
& "$env:LOCALAPPDATA\InnoSetup6\ISCC.exe" installer-arm64.iss
```

Output:

- `dist\UnifiedMessengerSetup.exe` (x64)
- `dist\UnifiedMessengerSetup-arm64.exe` (ARM64)

Optional: commit updated `dist\*.exe` for offline convenience. **Release tags no longer require committed installers** — CI builds them in the **package** job.

### 3. Run tests

```powershell
dotnet test UnifiedMessenger.Tests\UnifiedMessenger.Tests.csproj -p:Platform=x64 -c Release
```

### 4. UI smoke validation (optional local)

```powershell
dotnet publish UnifiedMessenger\UnifiedMessenger.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o UnifiedMessenger\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish
dotnet run --project UnifiedMessenger.UiSmokeTests\UnifiedMessenger.UiSmokeTests.csproj -c Release -- "UnifiedMessenger\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\UnifiedMessenger.exe"
```

## GitHub Releases (how they appear on Git)

**Important:** Pushing commits to `main` does **not** create or update the release shown on GitHub. The [Releases](https://github.com/AnfalHaider/Unified-Messenger/releases) page and the **latest** download links are driven by **annotated version tags** (`v1.0.7`, etc.).

| Action | Effect on GitHub |
|--------|------------------|
| Push to `main` only | Source updates; **Releases** unchanged unless a new tag exists |
| Push tag `v1.0.7` | CI **package** builds installers → **release** attaches them (+ SHA-256 sidecars) to GitHub Releases |
| Commit `dist/*.exe` without a tag | Optional convenience copy in repo; **not** used for GitHub Releases |

### Maintainer release checklist

1. Bump version in **csproj**, **app.manifest**, and **installer-shared.iss**.
2. Implement and merge features on `main`.
3. `dotnet test` (x64 Release) and optional local UI smoke harness.
4. Commit version bumps on `main`.
5. Create and push the tag (must match semver, with `v` prefix):

```powershell
git tag v1.0.7
git push origin main
git push origin v1.0.7
```

6. Wait for [GitHub Actions](https://github.com/AnfalHaider/Unified-Messenger/actions) **build** workflow: **verify** → **package** → **ui-smoke** → **release** (release runs only on tags).
7. Confirm [releases/latest](https://github.com/AnfalHaider/Unified-Messenger/releases/latest) serves CI-built installers and `.sha256` sidecars.

`GitHubUpdateService` compares the running app version to the newest **GitHub Release** tag; users only auto-update after step 6 succeeds.

### Re-tagging an existing version

Do **not** move `v1.0.6` (or any published tag) to a new commit if users may have already downloaded that release. Ship fixes as **v1.0.9**, etc., instead.

## CI/CD

GitHub Actions (`.github/workflows/build.yml`):

1. **verify** — build + unit test (Release, x64)
2. **package** — publish win-x64 and win-arm64, compile Inno Setup, write `.sha256` sidecars, upload publish + installer artifacts
3. **ui-smoke** — FlaUI harness against CI-built x64 publish output (main, PRs, and tags)
4. **release** — tag `v*` only; downloads CI installer artifacts and runs `gh release create` with `.exe` + `.sha256` files

Workflow triggers:

- **push** to `main` / **pull_request** → verify + package + ui-smoke (no GitHub Release)
- **push** tag `v*` → verify + package + ui-smoke + **GitHub Release** (installers from CI artifacts, not committed `dist/`)

## Auto-update

`GitHubUpdateService` runs on startup (non-blocking). When a newer GitHub release is detected, it downloads the architecture-appropriate installer (`UnifiedMessengerSetup.exe` or `UnifiedMessengerSetup-arm64.exe`), verifies Authenticode (and SHA-256 when a `.sha256` sidecar is published), and runs Inno silent install. Disable or prompt before update in **Settings → Updates**.

## Keyboard shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+D | Dashboard |
| Ctrl+, (comma) | Settings |
| Ctrl+Shift+N | Toggle notification panel |
| Ctrl+K | Command palette |
| Ctrl+1–9 | Switch to instance (sidebar order) |
| Ctrl+Space | Global copilot (when Local AI enabled) |

## User data

| Data | Location |
|------|----------|
| Instance registry | `%LocalAppData%\UnifiedMessenger\instances.json` |
| Analytics | `%LocalAppData%\UnifiedMessenger\analytics.json` |
| Triage (rich) | `%LocalAppData%\UnifiedMessenger\triage_v2.json` |
| Settings | `%LocalAppData%\UnifiedMessenger\settings.json` |
| WebView profiles | `%LocalAppData%\UnifiedMessenger\WebView2\` |

## Enhancement roadmap

See [ENHANCEMENT_ROADMAP.md](ENHANCEMENT_ROADMAP.md) for planned features. Tier 0–9 shipping baseline is complete; v1.0.9 improves reply detection and Meta unread sync on the Professional Operations dashboard.
