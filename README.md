# Unified Messenger

Native WinUI 3 desktop hub for multiple web messaging accounts (WhatsApp, Telegram, Messenger, Slack, Discord, Google Business Profile, and custom URLs) with unified notifications and Professional/Personal workspace split.

## Download (Windows)

| Platform | Installer |
|----------|-----------|
| **x64** | [UnifiedMessengerSetup.exe](https://github.com/AnfalHaider/Unified-Messenger/releases/latest/download/UnifiedMessengerSetup.exe) |
| **ARM64** | [UnifiedMessengerSetup-arm64.exe](https://github.com/AnfalHaider/Unified-Messenger/releases/latest/download/UnifiedMessengerSetup-arm64.exe) |

All releases: [github.com/AnfalHaider/Unified-Messenger/releases](https://github.com/AnfalHaider/Unified-Messenger/releases)

Requires Windows 10 1809+ or Windows 11 and the WebView2 Runtime (usually preinstalled on Windows 11).

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
dotnet test UnifiedMessenger.sln -c Release -p:Platform=x64
```

420+ unit tests cover services, adapters (including HTML fixture selector checks), and dialog helpers.

## Connect Google Business Profile

1. Sidebar → **Add Instance**
2. **Platform:** Google Business Profile
3. **Workspace:** Professional (enables dashboard review widgets)
4. Sign in at `https://business.google.com/locations` in the embedded browser
5. Open **Dashboard** for review alerts and response-time analytics

## Build a release installer

**Install location:** per-user `%LocalAppData%\UnifiedMessenger` (no admin). User settings, instances, WebView2 profiles, and analytics live in the same folder tree. Upgrades use Restart Manager + `AppMutex` to close a running `UnifiedMessenger.exe` before copying files.

If you previously installed to `Program Files`, uninstall the old build and reinstall so binaries and data share one root.

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
2. Keep `#define MyAppVersion` in `installer-shared.iss` and `<Version>` in `UnifiedMessenger.csproj` in sync (currently **1.0.5**).
3. Compile:

```powershell
cd "d:\Projects\Unified Messenger"
& "$env:LOCALAPPDATA\InnoSetup6\ISCC.exe" installer.iss
& "$env:LOCALAPPDATA\InnoSetup6\ISCC.exe" installer-arm64.iss
```

Output:

- `dist\UnifiedMessengerSetup.exe` (x64)
- `dist\UnifiedMessengerSetup-arm64.exe` (ARM64)

### 3. GitHub Release

Tag with `v1.0.5` (or newer). CI uploads **publish** and **installer** artifacts on every build; pushing a `v*` tag creates a GitHub Release with both installers attached.

## CI/CD

GitHub Actions (`.github/workflows/build.yml`):

1. **verify** — build + test (Release, x64)
2. **package** — publish win-x64 and win-arm64, compile Inno Setup, upload artifacts
3. **release** — on version tags, attach installers to GitHub Releases

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

## User data

| Data | Location |
|------|----------|
| Instance registry | `%LocalAppData%\UnifiedMessenger\instances.json` |
| Analytics | `%LocalAppData%\UnifiedMessenger\analytics.json` |
| Settings | `%LocalAppData%\UnifiedMessenger\settings.json` |
| WebView profiles | `%LocalAppData%\UnifiedMessenger\WebView2\` |

## Enhancement roadmap

See [ENHANCEMENT_ROADMAP.md](ENHANCEMENT_ROADMAP.md) for planned features. Tier 0–8 code audit and Tier 9 shipping tasks (CI artifacts, ARM64 installer, fixture tests) are complete; v1.0.5 adds local AI, auto-draft, and dashboard triage.
