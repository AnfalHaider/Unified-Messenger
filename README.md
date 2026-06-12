# Unified Messenger

Native WinUI 3 desktop client for running **multiple isolated WhatsApp / WhatsApp Business Web sessions** in one window, with a unified notification hub and lightweight operations dashboards.

**Current release:** v3.1.2 (WhatsApp Core “Lite” line)

## Scope

This baseline is intentionally narrow:

| In scope | Out of scope (removed in v3.0) |
|----------|--------------------------------|
| Multi-instance WhatsApp & WhatsApp Business (WebView2 profiles) | Telegram, Slack, Discord, Meta, Google Business, custom URLs |
| Unified desktop notification feed + taskbar badge | Local AI / Ollama, auto-draft, copilot hotkeys |
| Fixed-layout **Operations Command Center** (heuristic triage, branch pills, kanban) | OCC layout builder, platform intelligence panels, CSV export |
| **Personal Overview** panel | Voice-note pipeline, branch pulse LLM summaries |
| Heuristic message triage (no cloud LLM) | Backfill, benchmarks suite, multi-platform adapters |
| Zero-dependency local footprint (self-contained installer) | Per-platform module toggles |

Requires **Windows 10 1809+** or **Windows 11** and the **WebView2 Runtime** (preinstalled on most Windows 11 systems).

## Download (Windows)

| Platform | Installer |
|----------|-----------|
| **x64** | [UnifiedMessengerSetup.exe](https://github.com/AnfalHaider/Unified-Messenger/releases/latest/download/UnifiedMessengerSetup.exe) |
| **ARM64** | [UnifiedMessengerSetup-arm64.exe](https://github.com/AnfalHaider/Unified-Messenger/releases/latest/download/UnifiedMessengerSetup-arm64.exe) |

All releases: [github.com/AnfalHaider/Unified-Messenger/releases](https://github.com/AnfalHaider/Unified-Messenger/releases)

### What’s in v3.1.2

- **Updated branding:** Gradient four-bubble app icon applied across shell, tray, toasts, About page, and installers.

### What’s in v3.1.1

- **Startup fix:** Light/Dark theme no longer crashes launch when applied before the main window is created.

### What’s in v3.1.0

- **Dashboard overhaul:** OCC date-range filtering, message-volume trend chart, deeper WhatsApp telemetry ingress.
- **Sidebar UX:** Compact status labels, WhatsApp-focused instance list, improved truncation and tooltips.
- **Workspace purge:** Removed legacy multi-platform adapters, Ollama/AI stack, and obsolete tests/docs.
- **437** unit tests (x64, Release); trimmed UiSmoke harness (sidebar, OCC, Personal, settings, notifications).

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Shell (MainWindow + WorkspaceSidebar + Notification Hub) │
├──────────────┬──────────────────────────┬─────────────────┤
│ WhatsApp     │ Dashboard                │ Settings / About│
│ instances    │  ├─ Operations (OCC)     │                 │
│ (WebView2    │  └─ Personal Overview    │                 │
│  profiles)   │                          │                 │
└──────────────┴──────────────────────────┴─────────────────┘
         ▲ DOM scripts (adapter-core, whatsapp-adapter)
         │ WebMessage bridge
         ▼
   Notification + triage services (heuristic, local JSON stores)
```

- **WebView2:** One shared environment / user-data folder; isolated `ProfileName` per account.
- **OCC:** Fixed panels — KPI strip, branch workspace pills, immediate queue, kanban columns — fed by heuristic triage and thread registry.
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
dotnet publish UnifiedMessenger\UnifiedMessenger.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o UnifiedMessenger\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```

ARM64: repeat with `-r win-arm64` and `installer-arm64.iss`.

Outputs land in `dist\`:

- `dist\UnifiedMessengerSetup.exe` (x64)
- `dist\UnifiedMessengerSetup-arm64.exe` (ARM64)

### 3. Run tests

```powershell
dotnet test UnifiedMessenger.Tests\UnifiedMessenger.Tests.csproj -p:Platform=x64 -c Release
```

### 4. UI smoke validation (optional)

```powershell
dotnet publish UnifiedMessenger\UnifiedMessenger.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o UnifiedMessenger\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish
dotnet run --project UnifiedMessenger.UiSmokeTests\UnifiedMessenger.UiSmokeTests.csproj -c Release -- "UnifiedMessenger\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\UnifiedMessenger.exe"
```

## CI/CD

GitHub Actions (`.github/workflows/build.yml`):

1. **verify** — build + unit tests (Release, x64)
2. **package** — publish win-x64 and win-arm64, compile Inno Setup, SHA-256 sidecars
3. **ui-smoke** — FlaUI harness against published x64 binary
4. **release** — tag `v*` only; attaches CI-built installers to GitHub Releases

Push tag `v3.1.2` to publish a release. Pushing to `main` alone updates source but not the Releases page.

## Auto-update

`GitHubUpdateService` checks GitHub Releases on startup. When a newer tag is available, it downloads the matching installer, verifies Authenticode (and SHA-256 when published), and runs a silent Inno install. Control behavior under **Settings → Updates**.

## Keyboard shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+D | Dashboard |
| Ctrl+, (comma) | Settings |
| Ctrl+Shift+N | Toggle notification panel |
| Ctrl+K | Command palette |
| Ctrl+1–9 | Switch to instance (sidebar order) |

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
