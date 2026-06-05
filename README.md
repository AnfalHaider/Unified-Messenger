# Unified Messenger

Native WinUI 3 desktop hub for multiple web messaging accounts (WhatsApp, Telegram, Messenger, Slack, Discord, Google Business Profile, and custom URLs) with unified notifications and Professional/Personal workspace split.

**Current release:** [v1.0.15](https://github.com/AnfalHaider/Unified-Messenger/releases/tag/v1.0.15)

## Download (Windows)

| Platform | Installer |
|----------|-----------|
| **x64** | [UnifiedMessengerSetup.exe](https://github.com/AnfalHaider/Unified-Messenger/releases/latest/download/UnifiedMessengerSetup.exe) |
| **ARM64** | [UnifiedMessengerSetup-arm64.exe](https://github.com/AnfalHaider/Unified-Messenger/releases/latest/download/UnifiedMessengerSetup-arm64.exe) |

All releases: [github.com/AnfalHaider/Unified-Messenger/releases](https://github.com/AnfalHaider/Unified-Messenger/releases)

Requires Windows 10 1809+ or Windows 11 and the WebView2 Runtime (usually preinstalled on Windows 11).

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

### Visual Studio

Open `UnifiedMessenger.sln`, set **Platform** to **x64**, and run the **UnifiedMessenger** profile.

### Tests

```powershell
cd "d:\Projects\Unified Messenger"
dotnet test UnifiedMessenger.Tests\UnifiedMessenger.Tests.csproj -c Release -p:Platform=x64
```

534 unit tests cover services, adapters (including HTML fixture selector checks), backfill orchestration, dashboard card helpers, and dialog helpers.

## Connect Google Business Profile

1. Sidebar → **Add Instance**
2. **Platform:** Google Business Profile
3. **Workspace:** Professional (enables dashboard review widgets)
4. Sign in at `https://business.google.com/locations` in the embedded browser
5. Open **Dashboard** for review alerts and response-time analytics

## Version numbers (keep in sync)

Before every public release, align these three sources to the same **semver** (e.g. `1.0.9`):

| File | Field |
|------|--------|
| `UnifiedMessenger/UnifiedMessenger.csproj` | `<Version>`, `<AssemblyVersion>`, `<FileVersion>`, `<ApplicationDisplayVersion>` |
| `UnifiedMessenger/app.manifest` | `<assemblyIdentity version="…">` (four-part: `1.0.9.0`) |
| `installer-shared.iss` | `#define MyAppVersion "1.0.9"` |

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

Commit updated `dist\*.exe` when shipping from a maintainer machine (this repo tracks installers in `dist/` for convenience).

### 3. Run tests

```powershell
dotnet test UnifiedMessenger.Tests\UnifiedMessenger.Tests.csproj -p:Platform=x64
```

## GitHub Releases (how they appear on Git)

**Important:** Pushing commits to `main` does **not** create or update the release shown on GitHub. The [Releases](https://github.com/AnfalHaider/Unified-Messenger/releases) page and the **latest** download links are driven by **annotated version tags** (`v1.0.7`, etc.).

| Action | Effect on GitHub |
|--------|------------------|
| Push to `main` only | Source updates; **Releases** unchanged unless a new tag exists |
| Push tag `v1.0.7` | Triggers CI **release** job; creates/updates GitHub Release with both installers |
| Commit `dist/*.exe` without a tag | Installers in repo tree only; **not** attached to Releases |

### Maintainer release checklist

1. Bump version in **csproj**, **app.manifest**, and **installer-shared.iss**.
2. Implement and merge features on `main`.
3. `dotnet test` (x64).
4. `dotnet publish` (x64 + ARM64) and compile Inno Setup → update `dist\`.
5. Commit (e.g. `release: v1.0.7 with rebuilt x64 and ARM64 installers`).
6. Create and push the tag (must match semver, with `v` prefix):

```powershell
git tag v1.0.7
git push origin main
git push origin v1.0.7
```

7. Wait for [GitHub Actions](https://github.com/AnfalHaider/Unified-Messenger/actions) **build** workflow: **verify** → **package** → **release**.
8. Confirm [releases/latest](https://github.com/AnfalHaider/Unified-Messenger/releases/latest) serves the new installers.

`GitHubUpdateService` compares the running app version to the newest **GitHub Release** tag; users only auto-update after step 7 succeeds.

### Re-tagging an existing version

Do **not** move `v1.0.6` (or any published tag) to a new commit if users may have already downloaded that release. Ship fixes as **v1.0.9**, etc., instead.

## CI/CD

GitHub Actions (`.github/workflows/build.yml`):

1. **verify** — build + test (Release, x64)
2. **package** — publish win-x64 and win-arm64, compile Inno Setup, upload artifacts (runs on `main` and on tags)
3. **release** — runs only when the ref is `refs/tags/v*`; downloads CI-built installers and runs `gh release create` with both `.exe` files

Workflow triggers:

- **push** to `main` → verify + package artifacts (no GitHub Release)
- **push** tag `v*` → verify + package + **GitHub Release**

## Auto-update

`GitHubUpdateService` runs on startup (non-blocking). When a newer GitHub release is detected, it downloads `UnifiedMessengerSetup.exe` and runs it with Inno silent flags, then exits so files can be replaced. Disable or prompt before update in **Settings → Updates**.

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
